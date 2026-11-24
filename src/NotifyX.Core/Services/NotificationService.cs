using Microsoft.EntityFrameworkCore;
using CodexBlaze.PaymentReminder.Core.Data;
using CodexBlaze.PaymentReminder.Core.Models;
using Serilog;

namespace CodexBlaze.PaymentReminder.Core.Services;

public record NotificationItem(string Title, string Message, string NotificationType, int? PurchaseId, int? InstallmentId);

public interface INotificationService
{
    Task<List<NotificationItem>> CheckUpcomingDueNotificationsAsync();
    Task LogNotificationAsync(NotificationItem item);
    Task<List<NotificationHistory>> GetRecentNotificationsAsync(int count = 20);
    Task ClearNotificationsAsync();
}

public class NotificationService : INotificationService
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;

    public NotificationService(IDbContextFactory<AppDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<List<NotificationItem>> CheckUpcomingDueNotificationsAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        // Check user settings
        var settings = await context.AppSettings.ToDictionaryAsync(s => s.Key, s => s.Value);
        bool enabled = settings.TryGetValue("NotificationsEnabled", out var en) && bool.TryParse(en, out var b) && b;
        if (!enabled) return new List<NotificationItem>();

        bool remind7 = !settings.TryGetValue("Remind7Days", out var r7) || bool.Parse(r7);
        bool remind3 = !settings.TryGetValue("Remind3Days", out var r3) || bool.Parse(r3);
        bool remind1 = !settings.TryGetValue("Remind1Day", out var r1) || bool.Parse(r1);
        bool remindToday = !settings.TryGetValue("RemindDueDay", out var rt) || bool.Parse(rt);
        bool remindOverdue = !settings.TryGetValue("RemindOverdue", out var ro) || bool.Parse(ro);

        DateTime today = DateTime.Today;

        // Fetch unpaid installments
        var pending = await context.Installments
            .Include(i => i.Purchase)
                .ThenInclude(p => p!.Provider)
            .Where(i => i.RemainingAmount > 0)
            .ToListAsync();

        // Fetch notifications sent in the past 24 hours to prevent spam/duplicate triggers
        var recentSent = await context.NotificationHistories
            .Where(n => n.SentAt >= DateTime.UtcNow.AddHours(-24))
            .ToListAsync();

        var notificationsToTrigger = new List<NotificationItem>();

        foreach (var inst in pending)
        {
            int daysRemaining = (inst.DueDate.Date - today).Days;
            string? type = null;
            string? title = null;
            string? message = null;

            string provider = inst.Purchase?.Provider?.Name ?? "Provider";
            string item = inst.Purchase?.ItemName ?? "Item";
            string amountStr = $"LKR {inst.RemainingAmount:N2}";

            if (daysRemaining == 7 && remind7)
            {
                type = "7Days";
                title = "Payment Due in 7 Days";
                message = $"Your {provider} installment for '{item}' of {amountStr} is due on {inst.DueDate:MMM dd}.";
            }
            else if (daysRemaining == 3 && remind3)
            {
                type = "3Days";
                title = "Payment Due in 3 Days";
                message = $"Upcoming {provider} installment for '{item}': {amountStr} due on {inst.DueDate:MMM dd}.";
            }
            else if (daysRemaining == 1 && remind1)
            {
                type = "1Day";
                title = "Payment Due Tomorrow!";
                message = $"Your {provider} installment for '{item}' is due tomorrow. Amount: {amountStr}.";
            }
            else if (daysRemaining == 0 && remindToday)
            {
                type = "DueToday";
                title = "Payment Due Today!";
                message = $"Installment due today for '{item}' via {provider}. Amount: {amountStr}.";
            }
            else if (daysRemaining < 0 && remindOverdue)
            {
                type = "Overdue";
                title = "Overdue Payment Alert!";
                message = $"Your {provider} payment for '{item}' is overdue by {Math.Abs(daysRemaining)} days! Amount: {amountStr}.";
            }

            if (type != null)
            {
                // Verify duplicate within 24h
                bool alreadySent = recentSent.Any(s => s.InstallmentId == inst.Id && s.NotificationType == type);
                if (!alreadySent)
                {
                    notificationsToTrigger.Add(new NotificationItem(title!, message!, type, inst.PurchaseId, inst.Id));
                }
            }
        }

        return notificationsToTrigger;
    }

    public async Task LogNotificationAsync(NotificationItem item)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var entity = new NotificationHistory
        {
            PurchaseId = item.PurchaseId,
            InstallmentId = item.InstallmentId,
            Title = item.Title,
            Message = item.Message,
            NotificationType = item.NotificationType,
            SentAt = DateTime.UtcNow,
            IsRead = false
        };

        await context.NotificationHistories.AddAsync(entity);
        await context.SaveChangesAsync();
        Log.Information("Logged notification: {Title} for Installment {InstallmentId}", item.Title, item.InstallmentId);
    }

    public async Task<List<NotificationHistory>> GetRecentNotificationsAsync(int count = 20)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.NotificationHistories
            .OrderByDescending(n => n.SentAt)
            .Take(count)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task ClearNotificationsAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        context.NotificationHistories.RemoveRange(context.NotificationHistories);
        await context.SaveChangesAsync();
    }
}
