using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CodexBlaze.PaymentReminder.Core.Models;
using CodexBlaze.PaymentReminder.Core.Services;
using System.Collections.ObjectModel;

namespace CodexBlaze.PaymentReminder.Wpf.ViewModels;

public record CalendarDay(DateTime Date, bool IsCurrentMonth, bool IsToday, List<Installment> Installments);

public partial class CalendarViewModel : ObservableObject
{
    private readonly IPaymentService _paymentService;

    [ObservableProperty]
    private DateTime _currentMonth = new(DateTime.Today.Year, DateTime.Today.Month, 1);

    [ObservableProperty]
    private string _monthTitle = string.Empty;

    public ObservableCollection<CalendarDay> Days { get; } = new();

    public CalendarViewModel(IPaymentService paymentService)
    {
        _paymentService = paymentService;
        UpdateMonthTitle();
    }

    private void UpdateMonthTitle()
    {
        MonthTitle = CurrentMonth.ToString("MMMM yyyy");
    }

    [RelayCommand]
    public async Task LoadCalendarAsync()
    {
        UpdateMonthTitle();
        Days.Clear();

        DateTime startOfMonth = CurrentMonth;
        DateTime endOfMonth = startOfMonth.AddMonths(1).AddDays(-1);

        // Fetch installments for this month plus buffer days
        var allInstallments = await _paymentService.GetAllInstallmentsAsync(fromDate: startOfMonth.AddDays(-7), toDate: endOfMonth.AddDays(7));

        int dayOfWeekOffset = (int)startOfMonth.DayOfWeek; // 0 = Sunday
        DateTime gridStart = startOfMonth.AddDays(-dayOfWeekOffset);

        for (int i = 0; i < 35; i++)
        {
            DateTime date = gridStart.AddDays(i);
            bool isCurrent = date.Month == CurrentMonth.Month;
            bool isToday = date.Date == DateTime.Today;

            var dayInstallments = allInstallments.Where(inst => inst.DueDate.Date == date.Date).ToList();
            Days.Add(new CalendarDay(date, isCurrent, isToday, dayInstallments));
        }
    }

    [RelayCommand]
    public async Task NextMonthAsync()
    {
        CurrentMonth = CurrentMonth.AddMonths(1);
        await LoadCalendarAsync();
    }

    [RelayCommand]
    public async Task PrevMonthAsync()
    {
        CurrentMonth = CurrentMonth.AddMonths(-1);
        await LoadCalendarAsync();
    }

    [RelayCommand]
    public async Task TodayAsync()
    {
        CurrentMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        await LoadCalendarAsync();
    }
}
