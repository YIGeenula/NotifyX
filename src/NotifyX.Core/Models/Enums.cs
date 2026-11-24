namespace CodexBlaze.PaymentReminder.Core.Models;

public enum PaymentStatus
{
    Upcoming,
    DueSoon,
    DueToday,
    Overdue,
    PartiallyPaid,
    Paid
}

public enum PurchaseStatus
{
    Active,
    Completed,
    Defaulted,
    Cancelled
}

public enum ScheduleInterval
{
    Weekly,
    Biweekly,
    Monthly,
    Custom
}
