using Microsoft.EntityFrameworkCore;
using CodexBlaze.PaymentReminder.Core.Data;
using CodexBlaze.PaymentReminder.Core.Models;

namespace CodexBlaze.PaymentReminder.Core.Services;

public record DashboardStats
{
    public decimal TotalPurchaseValue { get; init; }
    public decimal TotalPaidAmount { get; init; }
    public decimal TotalOutstandingBalance { get; init; }
    public int UpcomingInstallmentsCount { get; init; }
    public int OverdueInstallmentsCount { get; init; }
    public int CompletedPurchasesCount { get; init; }
    public int ActivePurchasesCount { get; init; }
}

public record ProviderSpending(string ProviderName, string ColorHex, decimal TotalSpent, decimal TotalPaid, decimal TotalOutstanding);
public record MonthlyOverview(string MonthName, int Year, decimal TotalDue, decimal TotalPaid);

public interface IDashboardService
{
    Task<DashboardStats> GetDashboardStatsAsync(DateTime? fromDate = null, DateTime? toDate = null, int? providerId = null);
    Task<List<ProviderSpending>> GetProviderSpendingAsync();
    Task<List<MonthlyOverview>> GetMonthlyOverviewAsync(int monthsCount = 6, int? providerId = null);
    Task<List<Installment>> GetUpcomingInstallmentsTimelineAsync(int count = 5, int? providerId = null);
    Task<List<Purchase>> GetRecentPurchasesAsync(int count = 5, int? providerId = null);
    Task<List<Payment>> GetRecentPaymentsAsync(int count = 5, int? providerId = null);
}

public class DashboardService : IDashboardService
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;

    public DashboardService(IDbContextFactory<AppDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<DashboardStats> GetDashboardStatsAsync(DateTime? fromDate = null, DateTime? toDate = null, int? providerId = null)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var purchaseQuery = context.Purchases.AsNoTracking().AsQueryable();
        var installmentQuery = context.Installments.Include(i => i.Purchase).AsNoTracking().AsQueryable();
        var paymentQuery = context.Payments.Include(p => p.Purchase).AsNoTracking().AsQueryable();

        if (providerId.HasValue && providerId.Value > 0)
        {
            purchaseQuery = purchaseQuery.Where(p => p.ProviderId == providerId.Value);
            installmentQuery = installmentQuery.Where(i => i.Purchase != null && i.Purchase.ProviderId == providerId.Value);
            paymentQuery = paymentQuery.Where(p => p.Purchase != null && p.Purchase.ProviderId == providerId.Value);
        }

        if (fromDate.HasValue)
        {
            purchaseQuery = purchaseQuery.Where(p => p.PurchaseDate >= fromDate.Value.Date);
            installmentQuery = installmentQuery.Where(i => i.DueDate >= fromDate.Value.Date);
            paymentQuery = paymentQuery.Where(p => p.PaymentDate >= fromDate.Value.Date);
        }

        if (toDate.HasValue)
        {
            purchaseQuery = purchaseQuery.Where(p => p.PurchaseDate <= toDate.Value.Date.AddDays(1).AddTicks(-1));
            installmentQuery = installmentQuery.Where(i => i.DueDate <= toDate.Value.Date.AddDays(1).AddTicks(-1));
            paymentQuery = paymentQuery.Where(p => p.PaymentDate <= toDate.Value.Date.AddDays(1).AddTicks(-1));
        }

        decimal totalPurchaseValue = await purchaseQuery.SumAsync(p => (decimal?)p.TotalAmount) ?? 0m;
        decimal totalPaid = await paymentQuery.SumAsync(p => (decimal?)p.AmountPaid) ?? 0m;
        decimal totalOutstanding = await installmentQuery.SumAsync(i => (decimal?)i.RemainingAmount) ?? 0m;

        int upcomingCount = await installmentQuery.CountAsync(i => i.Status == PaymentStatus.Upcoming || i.Status == PaymentStatus.DueSoon || i.Status == PaymentStatus.DueToday);
        int overdueCount = await installmentQuery.CountAsync(i => i.Status == PaymentStatus.Overdue);
        int completedCount = await purchaseQuery.CountAsync(p => p.Status == PurchaseStatus.Completed);
        int activeCount = await purchaseQuery.CountAsync(p => p.Status == PurchaseStatus.Active);

        return new DashboardStats
        {
            TotalPurchaseValue = totalPurchaseValue,
            TotalPaidAmount = totalPaid,
            TotalOutstandingBalance = totalOutstanding,
            UpcomingInstallmentsCount = upcomingCount,
            OverdueInstallmentsCount = overdueCount,
            CompletedPurchasesCount = completedCount,
            ActivePurchasesCount = activeCount
        };
    }

    public async Task<List<ProviderSpending>> GetProviderSpendingAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var providers = await context.Providers
            .Include(p => p.Purchases)
                .ThenInclude(pur => pur.Installments)
            .Include(p => p.Purchases)
                .ThenInclude(pur => pur.Payments)
            .AsNoTracking()
            .ToListAsync();

        var result = new List<ProviderSpending>();
        foreach (var p in providers)
        {
            decimal totalSpent = p.Purchases.Sum(pur => pur.TotalAmount);
            decimal totalPaid = p.Purchases.SelectMany(pur => pur.Payments).Sum(pm => pm.AmountPaid);
            decimal outstanding = p.Purchases.SelectMany(pur => pur.Installments).Sum(i => i.RemainingAmount);

            if (totalSpent > 0 || outstanding > 0 || totalPaid > 0)
            {
                result.Add(new ProviderSpending(p.Name, p.BrandColorHex, totalSpent, totalPaid, outstanding));
            }
        }

        return result.OrderByDescending(r => r.TotalSpent).ToList();
    }

    public async Task<List<MonthlyOverview>> GetMonthlyOverviewAsync(int monthsCount = 6, int? providerId = null)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        DateTime today = DateTime.Today;
        var startMonth = new DateTime(today.Year, today.Month, 1).AddMonths(-(monthsCount - 1));

        var installmentQuery = context.Installments.Include(i => i.Purchase).Where(i => i.DueDate >= startMonth);
        var paymentQuery = context.Payments.Include(p => p.Purchase).Where(p => p.PaymentDate >= startMonth);

        if (providerId.HasValue && providerId.Value > 0)
        {
            installmentQuery = installmentQuery.Where(i => i.Purchase != null && i.Purchase.ProviderId == providerId.Value);
            paymentQuery = paymentQuery.Where(p => p.Purchase != null && p.Purchase.ProviderId == providerId.Value);
        }

        var installments = await installmentQuery.AsNoTracking().ToListAsync();
        var payments = await paymentQuery.AsNoTracking().ToListAsync();

        var list = new List<MonthlyOverview>();

        for (int i = 0; i < monthsCount; i++)
        {
            var cur = startMonth.AddMonths(i);
            var next = cur.AddMonths(1);

            decimal monthDue = installments.Where(inst => inst.DueDate >= cur && inst.DueDate < next).Sum(inst => inst.AmountDue);
            decimal monthPaid = payments.Where(pm => pm.PaymentDate >= cur && pm.PaymentDate < next).Sum(pm => pm.AmountPaid);

            list.Add(new MonthlyOverview(cur.ToString("MMM yyyy"), cur.Year, monthDue, monthPaid));
        }

        return list;
    }

    public async Task<List<Installment>> GetUpcomingInstallmentsTimelineAsync(int count = 5, int? providerId = null)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var query = context.Installments
            .Include(i => i.Purchase)
                .ThenInclude(p => p!.Provider)
            .Where(i => i.RemainingAmount > 0);

        if (providerId.HasValue && providerId.Value > 0)
        {
            query = query.Where(i => i.Purchase != null && i.Purchase.ProviderId == providerId.Value);
        }

        return await query
            .OrderBy(i => i.DueDate)
            .Take(count)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<List<Purchase>> GetRecentPurchasesAsync(int count = 5, int? providerId = null)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var query = context.Purchases
            .Include(p => p.Provider)
            .AsQueryable();

        if (providerId.HasValue && providerId.Value > 0)
        {
            query = query.Where(p => p.ProviderId == providerId.Value);
        }

        return await query
            .OrderByDescending(p => p.CreatedAt)
            .Take(count)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<List<Payment>> GetRecentPaymentsAsync(int count = 5, int? providerId = null)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var query = context.Payments
            .Include(p => p.Purchase)
            .AsQueryable();

        if (providerId.HasValue && providerId.Value > 0)
        {
            query = query.Where(p => p.Purchase != null && p.Purchase.ProviderId == providerId.Value);
        }

        return await query
            .OrderByDescending(p => p.PaymentDate)
            .ThenByDescending(p => p.Id)
            .Take(count)
            .AsNoTracking()
            .ToListAsync();
    }
}
