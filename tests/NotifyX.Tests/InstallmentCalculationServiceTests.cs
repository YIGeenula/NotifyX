using CodexBlaze.PaymentReminder.Core.Models;
using CodexBlaze.PaymentReminder.Core.Services;
using Xunit;

namespace CodexBlaze.PaymentReminder.Tests;

public class InstallmentCalculationServiceTests
{
    private readonly InstallmentCalculationService _service = new();

    [Fact]
    public void GenerateSchedule_EvenDistribution_DividesCorrectly()
    {
        // 3000 in 3 installments = 1000, 1000, 1000
        var schedule = _service.GenerateSchedule(3000m, 3, new DateTime(2026, 1, 1), ScheduleInterval.Monthly);

        Assert.Equal(3, schedule.Count);
        Assert.Equal(1000m, schedule[0].AmountDue);
        Assert.Equal(1000m, schedule[1].AmountDue);
        Assert.Equal(1000m, schedule[2].AmountDue);
        Assert.Equal(3000m, schedule.Sum(s => s.AmountDue));
    }

    [Fact]
    public void GenerateSchedule_OddAmount_HandlesCentRoundingOnLastInstallment()
    {
        // 1000 in 3 installments = 333.33, 333.33, 333.34
        var schedule = _service.GenerateSchedule(1000m, 3, new DateTime(2026, 1, 1), ScheduleInterval.Monthly);

        Assert.Equal(3, schedule.Count);
        Assert.Equal(333.33m, schedule[0].AmountDue);
        Assert.Equal(333.33m, schedule[1].AmountDue);
        Assert.Equal(333.34m, schedule[2].AmountDue);
        Assert.Equal(1000m, schedule.Sum(s => s.AmountDue));
    }

    [Fact]
    public void GenerateSchedule_FiveInstallments_ReconcilesTotalExactly()
    {
        // 100.01 in 5 installments
        var schedule = _service.GenerateSchedule(100.01m, 5, new DateTime(2026, 1, 1), ScheduleInterval.Weekly);

        Assert.Equal(5, schedule.Count);
        Assert.Equal(100.01m, schedule.Sum(s => s.AmountDue));
    }

    [Theory]
    [InlineData(ScheduleInterval.Weekly, 7)]
    [InlineData(ScheduleInterval.Biweekly, 14)]
    public void GenerateSchedule_Intervals_CalculatesCorrectDates(ScheduleInterval interval, int daysDiff)
    {
        DateTime start = new DateTime(2026, 3, 1);
        var schedule = _service.GenerateSchedule(600m, 2, start, interval);

        Assert.Equal(start, schedule[0].DueDate);
        Assert.Equal(start.AddDays(daysDiff), schedule[1].DueDate);
    }

    [Fact]
    public void DetermineStatus_VariousScenarios_EvaluatesProperly()
    {
        DateTime today = new DateTime(2026, 6, 15);

        // Paid
        Assert.Equal(PaymentStatus.Paid, _service.DetermineStatus(500m, 500m, today.AddDays(5), today));
        // Partially Paid
        Assert.Equal(PaymentStatus.PartiallyPaid, _service.DetermineStatus(500m, 250m, today.AddDays(5), today));
        // Overdue
        Assert.Equal(PaymentStatus.Overdue, _service.DetermineStatus(500m, 0m, today.AddDays(-1), today));
        // Due Today
        Assert.Equal(PaymentStatus.DueToday, _service.DetermineStatus(500m, 0m, today, today));
        // Due Soon (within 3 days)
        Assert.Equal(PaymentStatus.DueSoon, _service.DetermineStatus(500m, 0m, today.AddDays(2), today));
        // Upcoming
        Assert.Equal(PaymentStatus.Upcoming, _service.DetermineStatus(500m, 0m, today.AddDays(10), today));
    }

    [Fact]
    public void ApplyPayment_CalculatesBalancesCorrectly()
    {
        var installment = new Installment
        {
            AmountDue = 1500m,
            AmountPaid = 500m,
            RemainingAmount = 1000m,
            DueDate = new DateTime(2026, 11, 30)
        };

        var (newPaid, newRemaining, status) = _service.ApplyPayment(installment, 500m, new DateTime(2026, 11, 20));

        Assert.Equal(1000m, newPaid);
        Assert.Equal(500m, newRemaining);
        Assert.Equal(PaymentStatus.PartiallyPaid, status);
    }
}
