using CodexBlaze.PaymentReminder.Core.Models;

namespace CodexBlaze.PaymentReminder.Core.Services;

public class InstallmentCalculationService
{
    public record GeneratedInstallment(int InstallmentNumber, decimal AmountDue, DateTime DueDate);

    /// <summary>
    /// Generates automated installment schedules with correct cent/rounding distribution on the last installment.
    /// Never leaves unallocated rounding errors.
    /// </summary>
    public List<GeneratedInstallment> GenerateSchedule(
        decimal totalFinancedAmount,
        int installmentCount,
        DateTime firstDueDate,
        ScheduleInterval interval)
    {
        if (installmentCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(installmentCount), "Installment count must be at least 1.");

        if (totalFinancedAmount <= 0)
            throw new ArgumentOutOfRangeException(nameof(totalFinancedAmount), "Total financed amount must be greater than zero.");

        var result = new List<GeneratedInstallment>();

        // Exact split rounded to 2 decimal places
        decimal rawBaseAmount = decimal.Round(totalFinancedAmount / installmentCount, 2, MidpointRounding.AwayFromZero);
        decimal accumulated = 0m;

        for (int i = 1; i <= installmentCount; i++)
        {
            DateTime dueDate = CalculateDueDate(firstDueDate, i - 1, interval);
            decimal amount;

            if (i == installmentCount)
            {
                // Final installment absorbs any cent difference to guarantee total matches exactly
                amount = totalFinancedAmount - accumulated;
            }
            else
            {
                amount = rawBaseAmount;
                accumulated += amount;
            }

            result.Add(new GeneratedInstallment(i, amount, dueDate));
        }

        return result;
    }

    public DateTime CalculateDueDate(DateTime baseDate, int steps, ScheduleInterval interval)
    {
        return interval switch
        {
            ScheduleInterval.Weekly => baseDate.AddDays(steps * 7),
            ScheduleInterval.Biweekly => baseDate.AddDays(steps * 14),
            ScheduleInterval.Monthly => baseDate.AddMonths(steps),
            _ => baseDate.AddMonths(steps)
        };
    }

    /// <summary>
    /// Computes payment status based on amounts and due date relative to today.
    /// </summary>
    public PaymentStatus DetermineStatus(decimal amountDue, decimal amountPaid, DateTime dueDate, DateTime today)
    {
        if (amountPaid >= amountDue && amountDue > 0)
        {
            return PaymentStatus.Paid;
        }

        if (amountPaid > 0 && amountPaid < amountDue)
        {
            return PaymentStatus.PartiallyPaid;
        }

        DateTime dueDateOnly = dueDate.Date;
        DateTime todayOnly = today.Date;

        if (dueDateOnly < todayOnly)
        {
            return PaymentStatus.Overdue;
        }

        if (dueDateOnly == todayOnly)
        {
            return PaymentStatus.DueToday;
        }

        if (dueDateOnly <= todayOnly.AddDays(3))
        {
            return PaymentStatus.DueSoon;
        }

        return PaymentStatus.Upcoming;
    }

    /// <summary>
    /// Applies a payment against an installment, calculating new paid and remaining amounts safely.
    /// </summary>
    public (decimal newAmountPaid, decimal newRemaining, PaymentStatus status) ApplyPayment(
        Installment installment,
        decimal paymentAmount,
        DateTime paymentDate)
    {
        if (paymentAmount <= 0)
            throw new ArgumentException("Payment amount must be greater than zero.", nameof(paymentAmount));

        decimal totalPaid = installment.AmountPaid + paymentAmount;
        decimal remaining = Math.Max(0m, installment.AmountDue - totalPaid);
        PaymentStatus status = DetermineStatus(installment.AmountDue, totalPaid, installment.DueDate, paymentDate);

        return (totalPaid, remaining, status);
    }
}
