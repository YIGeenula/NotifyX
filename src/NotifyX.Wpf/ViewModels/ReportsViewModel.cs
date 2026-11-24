using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CodexBlaze.PaymentReminder.Core.Models;
using CodexBlaze.PaymentReminder.Core.Services;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.IO;

namespace CodexBlaze.PaymentReminder.Wpf.ViewModels;

public partial class ReportsViewModel : ObservableObject
{
    private readonly IDashboardService _dashboardService;
    private readonly IPurchaseService _purchaseService;
    private readonly IReportExportService _reportExportService;

    [ObservableProperty]
    private string _selectedPeriod = "All Time"; // "All Time", "This Month", "Last Month", "Last 3 Months", "This Year", "Custom Range"

    [ObservableProperty]
    private DateTime _startDate = DateTime.Today.AddMonths(-1);

    [ObservableProperty]
    private DateTime _endDate = DateTime.Today;

    [ObservableProperty]
    private Provider? _selectedProvider;

    [ObservableProperty]
    private bool _isCustomDateRange;

    [ObservableProperty]
    private decimal _totalSpending;

    [ObservableProperty]
    private decimal _totalPaid;

    [ObservableProperty]
    private decimal _totalOutstanding;

    [ObservableProperty]
    private int _completedPurchases;

    [ObservableProperty]
    private int _overdueInstallments;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public ObservableCollection<Provider> Providers { get; } = new();
    public ObservableCollection<ProviderSpending> ProviderBreakdown { get; } = new();
    public ObservableCollection<MonthlyOverview> MonthlyTrends { get; } = new();

    public ReportsViewModel(
        IDashboardService dashboardService,
        IPurchaseService purchaseService,
        IReportExportService reportExportService)
    {
        _dashboardService = dashboardService;
        _purchaseService = purchaseService;
        _reportExportService = reportExportService;
    }

    partial void OnSelectedPeriodChanged(string value)
    {
        IsCustomDateRange = (value == "Custom Range");
        _ = LoadReportsAsync();
    }

    partial void OnSelectedProviderChanged(Provider? value)
    {
        _ = LoadReportsAsync();
    }

    [RelayCommand]
    public async Task LoadReportsAsync()
    {
        if (Providers.Count == 0)
        {
            var provs = await _purchaseService.GetProvidersAsync();
            foreach (var p in provs) Providers.Add(p);
        }

        DateTime? fromDate = null;
        DateTime? toDate = DateTime.Today;

        if (SelectedPeriod == "This Month")
        {
            fromDate = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        }
        else if (SelectedPeriod == "Last Month")
        {
            var prev = DateTime.Today.AddMonths(-1);
            fromDate = new DateTime(prev.Year, prev.Month, 1);
            toDate = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddDays(-1);
        }
        else if (SelectedPeriod == "Last 3 Months")
        {
            fromDate = DateTime.Today.AddMonths(-3);
        }
        else if (SelectedPeriod == "This Year")
        {
            fromDate = new DateTime(DateTime.Today.Year, 1, 1);
        }
        else if (SelectedPeriod == "Custom Range")
        {
            fromDate = StartDate;
            toDate = EndDate;
        }
        else
        {
            toDate = null;
        }

        int? providerId = SelectedProvider?.Id;

        var stats = await _dashboardService.GetDashboardStatsAsync(fromDate, toDate, providerId);
        TotalSpending = stats.TotalPurchaseValue;
        TotalPaid = stats.TotalPaidAmount;
        TotalOutstanding = stats.TotalOutstandingBalance;
        CompletedPurchases = stats.CompletedPurchasesCount;
        OverdueInstallments = stats.OverdueInstallmentsCount;

        ProviderBreakdown.Clear();
        var providers = await _dashboardService.GetProviderSpendingAsync();
        foreach (var p in providers)
        {
            if (providerId.HasValue)
            {
                if (SelectedProvider != null && SelectedProvider.Name.Equals(p.ProviderName, StringComparison.OrdinalIgnoreCase))
                {
                    ProviderBreakdown.Add(p);
                }
            }
            else
            {
                ProviderBreakdown.Add(p);
            }
        }

        MonthlyTrends.Clear();
        var monthly = await _dashboardService.GetMonthlyOverviewAsync(12, providerId);
        foreach (var m in monthly) MonthlyTrends.Add(m);
    }

    [RelayCommand]
    public async Task DownloadExcelReportAsync()
    {
        var (fromDate, toDate) = GetDateBounds();
        string periodLabel = GetPeriodLabel();

        var sfd = new SaveFileDialog
        {
            Filter = "Excel Workbook (*.xlsx)|*.xlsx",
            FileName = $"NotifyX_Financial_Report_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx"
        };

        if (sfd.ShowDialog() == true)
        {
            StatusMessage = "Exporting Excel report...";
            await _reportExportService.ExportFilteredExcelReportAsync(sfd.FileName, fromDate, toDate, SelectedProvider?.Id, periodLabel);
            StatusMessage = $"Excel Report saved successfully: {Path.GetFileName(sfd.FileName)}";
        }
    }

    [RelayCommand]
    public async Task DownloadPdfReportAsync()
    {
        var (fromDate, toDate) = GetDateBounds();
        string periodLabel = GetPeriodLabel();

        var sfd = new SaveFileDialog
        {
            Filter = "PDF Document (*.pdf)|*.pdf",
            FileName = $"NotifyX_Financial_Statement_{DateTime.Now:yyyyMMdd_HHmmss}.pdf"
        };

        if (sfd.ShowDialog() == true)
        {
            StatusMessage = "Generating branded PDF statement...";
            await _reportExportService.ExportFilteredPdfReportAsync(sfd.FileName, fromDate, toDate, SelectedProvider?.Id, periodLabel);
            StatusMessage = $"PDF Statement saved successfully: {Path.GetFileName(sfd.FileName)}";
        }
    }

    private (DateTime? fromDate, DateTime? toDate) GetDateBounds()
    {
        DateTime? fromDate = null;
        DateTime? toDate = DateTime.Today;

        if (SelectedPeriod == "This Month")
        {
            fromDate = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        }
        else if (SelectedPeriod == "Last Month")
        {
            var prev = DateTime.Today.AddMonths(-1);
            fromDate = new DateTime(prev.Year, prev.Month, 1);
            toDate = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddDays(-1);
        }
        else if (SelectedPeriod == "Last 3 Months")
        {
            fromDate = DateTime.Today.AddMonths(-3);
        }
        else if (SelectedPeriod == "This Year")
        {
            fromDate = new DateTime(DateTime.Today.Year, 1, 1);
        }
        else if (SelectedPeriod == "Custom Range")
        {
            fromDate = StartDate;
            toDate = EndDate;
        }
        else
        {
            toDate = null;
        }

        return (fromDate, toDate);
    }

    private string GetPeriodLabel()
    {
        string prov = SelectedProvider != null ? $" ({SelectedProvider.Name})" : "";
        if (SelectedPeriod == "Custom Range")
        {
            return $"{StartDate:yyyy-MM-dd} to {EndDate:yyyy-MM-dd}{prov}";
        }
        return $"{SelectedPeriod}{prov}";
    }
}

