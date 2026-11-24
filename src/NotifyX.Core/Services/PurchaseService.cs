using Microsoft.EntityFrameworkCore;
using CodexBlaze.PaymentReminder.Core.Data;
using CodexBlaze.PaymentReminder.Core.Models;
using Serilog;

namespace CodexBlaze.PaymentReminder.Core.Services;

public interface IPurchaseService
{
    Task<List<Purchase>> GetAllPurchasesAsync(string? searchQuery = null, int? providerId = null, PurchaseStatus? status = null);
    Task<Purchase?> GetPurchaseByIdAsync(int id);
    Task<Purchase> CreatePurchaseAsync(Purchase purchase, List<Installment> installments);
    Task<Purchase> UpdatePurchaseAsync(Purchase purchase);
    Task<bool> DeletePurchaseAsync(int purchaseId);
    Task<List<Provider>> GetProvidersAsync();
    Task<Provider> AddProviderAsync(Provider provider);
}

public class PurchaseService : IPurchaseService
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;
    private readonly InstallmentCalculationService _calculationService;

    public PurchaseService(IDbContextFactory<AppDbContext> contextFactory, InstallmentCalculationService calculationService)
    {
        _contextFactory = contextFactory;
        _calculationService = calculationService;
    }

    public async Task<List<Purchase>> GetAllPurchasesAsync(string? searchQuery = null, int? providerId = null, PurchaseStatus? status = null)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var query = context.Purchases
            .Include(p => p.Provider)
            .Include(p => p.Installments.OrderBy(i => i.InstallmentNumber))
            .Include(p => p.Payments.OrderByDescending(pm => pm.PaymentDate))
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(searchQuery))
        {
            string term = searchQuery.Trim().ToLower();
            query = query.Where(p => p.ItemName.ToLower().Contains(term) ||
                                     p.Description.ToLower().Contains(term) ||
                                     p.Category.ToLower().Contains(term) ||
                                     (p.Provider != null && p.Provider.Name.ToLower().Contains(term)));
        }

        if (providerId.HasValue && providerId.Value > 0)
        {
            query = query.Where(p => p.ProviderId == providerId.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(p => p.Status == status.Value);
        }

        return await query.OrderByDescending(p => p.PurchaseDate).ToListAsync();
    }

    public async Task<Purchase?> GetPurchaseByIdAsync(int id)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Purchases
            .Include(p => p.Provider)
            .Include(p => p.Installments.OrderBy(i => i.InstallmentNumber))
            .Include(p => p.Payments.OrderByDescending(pm => pm.PaymentDate))
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<Purchase> CreatePurchaseAsync(Purchase purchase, List<Installment> installments)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        await using var transaction = await context.Database.BeginTransactionAsync();

        try
        {
            purchase.CreatedAt = DateTime.UtcNow;
            decimal totalDue = installments.Sum(i => i.AmountDue);
            purchase.RemainingAmount = Math.Max(0m, purchase.TotalAmount - purchase.InitialPayment);
            purchase.NumberOfInstallments = installments.Count;
            purchase.Status = purchase.RemainingAmount <= 0 ? PurchaseStatus.Completed : PurchaseStatus.Active;

            await context.Purchases.AddAsync(purchase);
            await context.SaveChangesAsync();

            // Link and save installments
            foreach (var inst in installments)
            {
                inst.PurchaseId = purchase.Id;
                inst.AmountPaid = 0m;
                inst.RemainingAmount = inst.AmountDue;
                inst.Status = _calculationService.DetermineStatus(inst.AmountDue, 0m, inst.DueDate, DateTime.Today);
                await context.Installments.AddAsync(inst);
            }

            // If an initial payment was made, record it as a Payment
            if (purchase.InitialPayment > 0)
            {
                var provider = await context.Providers.FindAsync(purchase.ProviderId);
                var payment = new Payment
                {
                    PurchaseId = purchase.Id,
                    AmountPaid = purchase.InitialPayment,
                    PaymentDate = purchase.PurchaseDate,
                    PaymentMethod = "Initial Downpayment",
                    Notes = "Downpayment at purchase",
                    ProviderName = provider?.Name ?? "Provider",
                    CreatedAt = DateTime.UtcNow
                };
                await context.Payments.AddAsync(payment);
            }

            await context.SaveChangesAsync();
            await transaction.CommitAsync();

            Log.Information("Created purchase {PurchaseId}: {ItemName} with {Count} installments.", purchase.Id, purchase.ItemName, installments.Count);
            return purchase;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            Log.Error(ex, "Failed to create purchase {ItemName}.", purchase.ItemName);
            throw;
        }
    }

    public async Task<Purchase> UpdatePurchaseAsync(Purchase purchase)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var existing = await context.Purchases.FindAsync(purchase.Id);
        if (existing == null)
            throw new KeyNotFoundException($"Purchase with ID {purchase.Id} not found.");

        existing.ItemName = purchase.ItemName;
        existing.Description = purchase.Description;
        existing.Category = purchase.Category;
        existing.ProviderId = purchase.ProviderId;
        existing.PurchaseDate = purchase.PurchaseDate;
        existing.Notes = purchase.Notes;
        existing.ImagePath = purchase.ImagePath;
        existing.ReceiptAttachmentPath = purchase.ReceiptAttachmentPath;
        existing.Status = purchase.Status;
        existing.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync();
        return existing;
    }

    public async Task<bool> DeletePurchaseAsync(int purchaseId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        await using var transaction = await context.Database.BeginTransactionAsync();
        try
        {
            var purchase = await context.Purchases
                .Include(p => p.Installments)
                .Include(p => p.Payments)
                .FirstOrDefaultAsync(p => p.Id == purchaseId);

            if (purchase == null) return false;

            // Safe cascade removal within explicit transaction
            context.Payments.RemoveRange(purchase.Payments);
            context.Installments.RemoveRange(purchase.Installments);
            context.Purchases.Remove(purchase);

            await context.SaveChangesAsync();
            await transaction.CommitAsync();

            Log.Information("Safely deleted purchase {PurchaseId} and its associated records.", purchaseId);
            return true;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            Log.Error(ex, "Failed to delete purchase {PurchaseId}.", purchaseId);
            throw;
        }
    }

    public async Task<List<Provider>> GetProvidersAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Providers.AsNoTracking().Where(p => p.IsActive).ToListAsync();
    }

    public async Task<Provider> AddProviderAsync(Provider provider)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        await context.Providers.AddAsync(provider);
        await context.SaveChangesAsync();
        return provider;
    }
}
