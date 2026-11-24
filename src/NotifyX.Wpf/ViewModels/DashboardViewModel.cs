using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CodexBlaze.PaymentReminder.Core.Models;
using CodexBlaze.PaymentReminder.Core.Services;
using System.Collections.ObjectModel;

namespace CodexBlaze.PaymentReminder.Wpf.ViewModels;

public partial class DashboardViewModel : ObservableObject
{
    private readonly IDashboardService _dashboardService;
    private readonly IPaymentService _paymentService;
    private readonly IPurchaseService _purchaseService;

    [ObservableProperty]
    private string _activeDashboardTab = "Overview"; // "Overview", "Mintpay", "Koko", "Payzy"

    [ObservableProperty]
    private decimal _totalPurchaseValue;

    [ObservableProperty]
    private decimal _totalPaidAmount;

    [ObservableProperty]
    private decimal _totalOutstandingBalance;

    [ObservableProperty]
    private int _upcomingInstallmentsCount;

    [ObservableProperty]
    private int _overdueInstallmentsCount;

    [ObservableProperty]
    private int _completedPurchasesCount;

    [ObservableProperty]
    private int _activePurchasesCount;

    [ObservableProperty]
    private string _selectedFilter = "All Time";

    [ObservableProperty]
    private bool _isLoading;

    public ObservableCollection<ProviderSpending> ProviderSpendings { get; } = new();
    public ObservableCollection<MonthlyOverview> MonthlyOverviews { get; } = new();
    public ObservableCollection<Installment> UpcomingInstallments { get; } = new();
    public ObservableCollection<Purchase> RecentPurchases { get; } = new();
    public ObservableCollection<Payment> RecentPayments { get; } = new();
    public ObservableCollection<Provider> AvailableProviders { get; } = new();

    public DashboardViewModel(IDashboardService dashboardService, IPaymentService paymentService, IPurchaseService purchaseService)
    {
        _dashboardService = dashboardService;
        _paymentService = paymentService;
        _purchaseService = purchaseService;
    }

    [RelayCommand]
    public async Task SwitchDashboardTabAsync(string tabName)
    {
        ActiveDashboardTab = tabName;
        await LoadDashboardAsync();
    }

    [RelayCommand]
    public async Task LoadDashboardAsync()
    {
        IsLoading = true;
        try
        {
            await _paymentService.RefreshInstallmentStatusesAsync();

            if (AvailableProviders.Count == 0)
            {
                var provs = await _purchaseService.GetProvidersAsync();
                foreach (var p in provs) AvailableProviders.Add(p);
            }

            // Find providerId if a specific provider tab is active
            int? providerId = null;
            if (ActiveDashboardTab != "Overview")
            {
                var match = AvailableProviders.FirstOrDefault(p => p.Name.Equals(ActiveDashboardTab, StringComparison.OrdinalIgnoreCase));
                if (match != null)
                {
                    providerId = match.Id;
                }
            }

            DateTime? fromDate = null;
            DateTime? toDate = DateTime.Today;

            if (SelectedFilter == "This Month")
            {
                fromDate = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            }
            else if (SelectedFilter == "Last Month")
            {
                var prev = DateTime.Today.AddMonths(-1);
                fromDate = new DateTime(prev.Year, prev.Month, 1);
                toDate = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddDays(-1);
            }
            else if (SelectedFilter == "Last 3 Months")
            {
                fromDate = DateTime.Today.AddMonths(-3);
            }
            else if (SelectedFilter == "This Year")
            {
                fromDate = new DateTime(DateTime.Today.Year, 1, 1);
            }
            else
            {
                toDate = null;
            }

            var stats = await _dashboardService.GetDashboardStatsAsync(fromDate, toDate, providerId);
            TotalPurchaseValue = stats.TotalPurchaseValue;
            TotalPaidAmount = stats.TotalPaidAmount;
            TotalOutstandingBalance = stats.TotalOutstandingBalance;
            UpcomingInstallmentsCount = stats.UpcomingInstallmentsCount;
            OverdueInstallmentsCount = stats.OverdueInstallmentsCount;
            CompletedPurchasesCount = stats.CompletedPurchasesCount;
            ActivePurchasesCount = stats.ActivePurchasesCount;

            // Load charts & timelines
            ProviderSpendings.Clear();
            var providers = await _dashboardService.GetProviderSpendingAsync();
            foreach (var p in providers)
            {
                if (providerId.HasValue)
                {
                    if (AvailableProviders.Any(ap => ap.Id == providerId && ap.Name.Equals(p.ProviderName, StringComparison.OrdinalIgnoreCase)))
                    {
                        ProviderSpendings.Add(p);
                    }
                }
                else
                {
                    ProviderSpendings.Add(p);
                }
            }

            MonthlyOverviews.Clear();
            var monthly = await _dashboardService.GetMonthlyOverviewAsync(6, providerId);
            foreach (var m in monthly) MonthlyOverviews.Add(m);

            UpcomingInstallments.Clear();
            var upcoming = await _dashboardService.GetUpcomingInstallmentsTimelineAsync(6, providerId);
            foreach (var u in upcoming) UpcomingInstallments.Add(u);

            RecentPurchases.Clear();
            var purchases = await _dashboardService.GetRecentPurchasesAsync(6, providerId);
            foreach (var r in purchases) RecentPurchases.Add(r);

            RecentPayments.Clear();
            var payments = await _dashboardService.GetRecentPaymentsAsync(6, providerId);
            foreach (var pm in payments) RecentPayments.Add(pm);
        }
        finally
        {
            IsLoading = false;
        }
    }
}
