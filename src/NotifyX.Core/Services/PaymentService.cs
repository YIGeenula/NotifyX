using Microsoft.EntityFrameworkCore;
using CodexBlaze.PaymentReminder.Core.Data;
using CodexBlaze.PaymentReminder.Core.Models;
using Serilog;

namespace CodexBlaze.PaymentReminder.Core.Services;

public interface IPaymentService
{
    Task<List<Payment>> GetAllPaymentsAsync(string? searchQuery = null, int? providerId = null, DateTime? fromDate = null, DateTime? toDate = null);
    Task<List<Installment>> GetAllInstallmentsAsync(PaymentStatus? status = null, DateTime? fromDate = null, DateTime? toDate = null);
    Task<Payment> RecordPaymentAsync(int installmentId, decimal amount, DateTime paymentDate, string paymentMethod, string notes);
    Task<bool> DeletePaymentAsync(int paymentId);
    Task RefreshInstallmentStatusesAsync();
}

public class PaymentService : IPaymentService
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;
    private readonly InstallmentCalculationService _calculationService;

    public PaymentService(IDbContextFactory<AppDbContext> contextFactory, InstallmentCalculationService calculationService)
    {
        _contextFactory = contextFactory;
        _calculationService = calculationService;
    }

    public async Task<List<Payment>> GetAllPaymentsAsync(string? searchQuery = null, int? providerId = null, DateTime? fromDate = null, DateTime? toDate = null)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var query = context.Payments
            .Include(p => p.Purchase)
                .ThenInclude(pur => pur!.Provider)
            .Include(p => p.Installment)
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(searchQuery))
        {
            string term = searchQuery.Trim().ToLower();
            query = query.Where(p => (p.Purchase != null && p.Purchase.ItemName.ToLower().Contains(term)) ||
                                     p.ProviderName.ToLower().Contains(term) ||
                                     p.PaymentMethod.ToLower().Contains(term) ||
                                     p.Notes.ToLower().Contains(term));
        }

        if (providerId.HasValue && providerId.Value > 0)
        {
            query = query.Where(p => p.Purchase != null && p.Purchase.ProviderId == providerId.Value);
        }

        if (fromDate.HasValue)
        {
            query = query.Where(p => p.PaymentDate >= fromDate.Value.Date);
        }

        if (toDate.HasValue)
        {
            query = query.Where(p => p.PaymentDate <= toDate.Value.Date.AddDays(1).AddTicks(-1));
        }

        return await query.OrderByDescending(p => p.PaymentDate).ThenByDescending(p => p.Id).ToListAsync();
    }

    public async Task<List<Installment>> GetAllInstallmentsAsync(PaymentStatus? status = null, DateTime? fromDate = null, DateTime? toDate = null)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var query = context.Installments
            .Include(i => i.Purchase)
                .ThenInclude(p => p!.Provider)
            .Include(i => i.Payments)
            .AsNoTracking()
            .AsQueryable();

        if (status.HasValue)
        {
            query = query.Where(i => i.Status == status.Value);
        }

        if (fromDate.HasValue)
        {
            query = query.Where(i => i.DueDate >= fromDate.Value.Date);
        }

        if (toDate.HasValue)
        {
            query = query.Where(i => i.DueDate <= toDate.Value.Date.AddDays(1).AddTicks(-1));
        }

        return await query.OrderBy(i => i.DueDate).ToListAsync();
    }

    public async Task<Payment> RecordPaymentAsync(int installmentId, decimal amount, DateTime paymentDate, string paymentMethod, string notes)
    {
        if (amount <= 0)
            throw new ArgumentException("Payment amount must be greater than zero.", nameof(amount));

        await using var context = await _contextFactory.CreateDbContextAsync();
        await using var transaction = await context.Database.BeginTransactionAsync();

        try
        {
            var installment = await context.Installments
                .Include(i => i.Purchase)
                    .ThenInclude(p => p!.Provider)
                .FirstOrDefaultAsync(i => i.Id == installmentId);

            if (installment == null)
                throw new KeyNotFoundException($"Installment {installmentId} not found.");

            var purchase = installment.Purchase;
            if (purchase == null)
                throw new InvalidOperationException("Installment has no associated purchase.");

            // Update installment balance and status
            installment.AmountPaid += amount;
            installment.RemainingAmount = Math.Max(0m, installment.AmountDue - installment.AmountPaid);
            installment.PaidDate = installment.RemainingAmount == 0 ? paymentDate : installment.PaidDate;
            installment.Status = _calculationService.DetermineStatus(installment.AmountDue, installment.AmountPaid, installment.DueDate, paymentDate);

            // Create payment entity
            var payment = new Payment
            {
                PurchaseId = purchase.Id,
                InstallmentId = installment.Id,
                ProviderName = purchase.Provider?.Name ?? "Provider",
                AmountPaid = amount,
                PaymentDate = paymentDate,
                PaymentMethod = paymentMethod,
                Notes = notes,
                CreatedAt = DateTime.UtcNow
            };

            await context.Payments.AddAsync(payment);

            // Update purchase remaining amount & status
            // Recalculate remaining amount from all installments
            var allInstallments = await context.Installments.Where(i => i.PurchaseId == purchase.Id).ToListAsync();
            // apply this installment's in-memory modification
            decimal totalOutstanding = allInstallments.Sum(i => i.Id == installment.Id ? installment.RemainingAmount : i.RemainingAmount);
            purchase.RemainingAmount = totalOutstanding;

            if (purchase.RemainingAmount <= 0)
            {
                purchase.Status = PurchaseStatus.Completed;
            }
            purchase.UpdatedAt = DateTime.UtcNow;

            await context.SaveChangesAsync();
            await transaction.CommitAsync();

            Log.Information("Recorded payment {PaymentId} for installment {InstallmentId}, amount {Amount}", payment.Id, installmentId, amount);
            return payment;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            Log.Error(ex, "Failed to record payment for installment {InstallmentId}", installmentId);
            throw;
        }
    }

    public async Task<bool> DeletePaymentAsync(int paymentId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        await using var transaction = await context.Database.BeginTransactionAsync();

        try
        {
            var payment = await context.Payments
                .Include(p => p.Installment)
                .Include(p => p.Purchase)
                .FirstOrDefaultAsync(p => p.Id == paymentId);

            if (payment == null) return false;

            if (payment.Installment != null)
            {
                payment.Installment.AmountPaid = Math.Max(0m, payment.Installment.AmountPaid - payment.AmountPaid);
                payment.Installment.RemainingAmount = Math.Max(0m, payment.Installment.AmountDue - payment.Installment.AmountPaid);
                payment.Installment.Status = _calculationService.DetermineStatus(
                    payment.Installment.AmountDue,
                    payment.Installment.AmountPaid,
                    payment.Installment.DueDate,
                    DateTime.Today);

                if (payment.Installment.RemainingAmount > 0)
                {
                    payment.Installment.PaidDate = null;
                }
            }

            if (payment.Purchase != null)
            {
                payment.Purchase.RemainingAmount += payment.AmountPaid;
                if (payment.Purchase.RemainingAmount > 0 && payment.Purchase.Status == PurchaseStatus.Completed)
                {
                    payment.Purchase.Status = PurchaseStatus.Active;
                }
                payment.Purchase.UpdatedAt = DateTime.UtcNow;
            }

            context.Payments.Remove(payment);
            await context.SaveChangesAsync();
            await transaction.CommitAsync();

            Log.Information("Reverted and deleted payment {PaymentId}.", paymentId);
            return true;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            Log.Error(ex, "Failed to delete payment {PaymentId}.", paymentId);
            throw;
        }
    }

    public async Task RefreshInstallmentStatusesAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var pendingInstallments = await context.Installments
            .Where(i => i.Status != PaymentStatus.Paid)
            .ToListAsync();

        DateTime today = DateTime.Today;
        bool changed = false;

        foreach (var inst in pendingInstallments)
        {
            var newStatus = _calculationService.DetermineStatus(inst.AmountDue, inst.AmountPaid, inst.DueDate, today);
            if (inst.Status != newStatus)
            {
                inst.Status = newStatus;
                changed = true;
            }
        }

        if (changed)
        {
            await context.SaveChangesAsync();
            Log.Information("Refreshed pending installment statuses against today's date.");
        }
    }
}
