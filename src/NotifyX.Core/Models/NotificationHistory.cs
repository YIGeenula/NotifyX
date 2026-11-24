namespace CodexBlaze.PaymentReminder.Core.Models;

public class NotificationHistory
{
    public int Id { get; set; }
    public int? PurchaseId { get; set; }
    public int? InstallmentId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string NotificationType { get; set; } = string.Empty; // 7Days, 3Days, 1Day, DueToday, Overdue
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
    public bool IsRead { get; set; }
}
