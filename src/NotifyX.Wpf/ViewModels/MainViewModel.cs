using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CodexBlaze.PaymentReminder.Core.Models;
using CodexBlaze.PaymentReminder.Core.Services;
using System.Collections.ObjectModel;

namespace CodexBlaze.PaymentReminder.Wpf.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly DashboardViewModel _dashboardViewModel;
    private readonly PurchasesViewModel _purchasesViewModel;
    private readonly PaymentsViewModel _paymentsViewModel;
    private readonly CalendarViewModel _calendarViewModel;
    private readonly ReportsViewModel _reportsViewModel;
    private readonly SettingsViewModel _settingsViewModel;
    private readonly INotificationService _notificationService;

    [ObservableProperty]
    private object _currentPage;

    [ObservableProperty]
    private string _currentPageTitle = "Dashboard";

    [ObservableProperty]
    private string _globalSearchQuery = string.Empty;

    [ObservableProperty]
    private int _unreadNotificationCount;

    [ObservableProperty]
    private string _currentDateDisplay = DateTime.Today.ToString("dddd, MMM dd, yyyy");

    public DashboardViewModel DashboardVM => _dashboardViewModel;
    public PurchasesViewModel PurchasesVM => _purchasesViewModel;
    public PaymentsViewModel PaymentsVM => _paymentsViewModel;
    public CalendarViewModel CalendarVM => _calendarViewModel;
    public ReportsViewModel ReportsVM => _reportsViewModel;
    public SettingsViewModel SettingsVM => _settingsViewModel;

    public MainViewModel(
        DashboardViewModel dashboardViewModel,
        PurchasesViewModel purchasesViewModel,
        PaymentsViewModel paymentsViewModel,
        CalendarViewModel calendarViewModel,
        ReportsViewModel reportsViewModel,
        SettingsViewModel settingsViewModel,
        INotificationService notificationService)
    {
        _dashboardViewModel = dashboardViewModel;
        _purchasesViewModel = purchasesViewModel;
        _paymentsViewModel = paymentsViewModel;
        _calendarViewModel = calendarViewModel;
        _reportsViewModel = reportsViewModel;
        _settingsViewModel = settingsViewModel;
        _notificationService = notificationService;

        _currentPage = _dashboardViewModel;
    }

    [RelayCommand]
    public async Task NavigateAsync(string destination)
    {
        CurrentPageTitle = destination;
        switch (destination)
        {
            case "Dashboard":
                CurrentPage = _dashboardViewModel;
                await _dashboardViewModel.LoadDashboardAsync();
                break;
            case "Purchases":
                CurrentPage = _purchasesViewModel;
                await _purchasesViewModel.LoadPurchasesAsync();
                break;
            case "Installments":
            case "Payments":
                CurrentPage = _paymentsViewModel;
                await _paymentsViewModel.LoadDataAsync();
                break;
            case "Calendar":
                CurrentPage = _calendarViewModel;
                await _calendarViewModel.LoadCalendarAsync();
                break;
            case "Reports":
                CurrentPage = _reportsViewModel;
                await _reportsViewModel.LoadReportsAsync();
                break;
            case "Settings":
                CurrentPage = _settingsViewModel;
                await _settingsViewModel.LoadSettingsAsync();
                break;
        }
    }

    [RelayCommand]
    public async Task CheckNotificationsAsync()
    {
        var dueItems = await _notificationService.CheckUpcomingDueNotificationsAsync();
        foreach (var item in dueItems)
        {
            await _notificationService.LogNotificationAsync(item);
            CodexBlaze.PaymentReminder.Wpf.Services.ToastNotificationManager.ShowToast(item.Title, item.Message);
        }
        var notifs = await _notificationService.GetRecentNotificationsAsync(10);
        UnreadNotificationCount = notifs.Count(n => !n.IsRead);
    }
}
