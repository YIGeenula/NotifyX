using Microsoft.EntityFrameworkCore;
using CodexBlaze.PaymentReminder.Core.Data;
using CodexBlaze.PaymentReminder.Core.Models;
using Serilog;

namespace CodexBlaze.PaymentReminder.Core.Services;

public class DatabaseInitializer
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;

    public DatabaseInitializer(IDbContextFactory<AppDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task InitializeAsync()
    {
        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            await context.Database.EnsureCreatedAsync();

            // Seed default providers if none exist
            if (!await context.Providers.AnyAsync())
            {
                var defaultProviders = new List<Provider>
                {
                    new() { Name = "Mintpay", Description = "Buy now pay later with Mintpay", BrandColorHex = "#00D1B2", IsDefault = true, IsActive = true },
                    new() { Name = "Koko", Description = "Shop now pay later with Koko", BrandColorHex = "#FF3860", IsDefault = false, IsActive = true },
                    new() { Name = "Payzy", Description = "Flexible payment provider Payzy", BrandColorHex = "#7C5CFC", IsDefault = false, IsActive = true }
                };

                await context.Providers.AddRangeAsync(defaultProviders);
            }
            else
            {
                // Ensure Payzy exists
                if (!await context.Providers.AnyAsync(p => p.Name == "Payzy"))
                {
                    await context.Providers.AddAsync(new Provider
                    {
                        Name = "Payzy",
                        Description = "Flexible payment provider Payzy",
                        BrandColorHex = "#7C5CFC",
                        IsDefault = false,
                        IsActive = true
                    });
                }

                // Remove or deactivate 'Other / Custom' provider
                var otherProvider = await context.Providers
                    .Include(p => p.Purchases)
                    .FirstOrDefaultAsync(p => p.Name.Contains("Other") || p.Name.Contains("Custom"));
                if (otherProvider != null)
                {
                    if (otherProvider.Purchases.Count == 0)
                    {
                        context.Providers.Remove(otherProvider);
                    }
                    else
                    {
                        otherProvider.IsActive = false;
                    }
                }
            }

            // Seed default settings if none exist
            if (!await context.AppSettings.AnyAsync())
            {
                var defaultSettings = new List<AppSetting>
                {
                    new() { Key = "Currency", Value = "LKR", Group = "General" },
                    new() { Key = "DateFormat", Value = "yyyy-MM-dd", Group = "General" },
                    new() { Key = "Theme", Value = "Dark", Group = "Appearance" },
                    new() { Key = "AccentColor", Value = "#7C5CFC", Group = "Appearance" },
                    new() { Key = "NotificationsEnabled", Value = "True", Group = "Notifications" },
                    new() { Key = "Remind7Days", Value = "True", Group = "Notifications" },
                    new() { Key = "Remind3Days", Value = "True", Group = "Notifications" },
                    new() { Key = "Remind1Day", Value = "True", Group = "Notifications" },
                    new() { Key = "RemindDueDay", Value = "True", Group = "Notifications" },
                    new() { Key = "RemindOverdue", Value = "True", Group = "Notifications" },
                    new() { Key = "StartWithWindows", Value = "False", Group = "System" },
                    new() { Key = "MinimizeToTray", Value = "True", Group = "System" },
                    new() { Key = "CloseToTray", Value = "True", Group = "System" },
                    new() { Key = "AutoBackupEnabled", Value = "True", Group = "Database" },
                    new() { Key = "BackupFrequencyDays", Value = "7", Group = "Database" }
                };

                await context.AppSettings.AddRangeAsync(defaultSettings);
            }

            // Clean up any old test notification history records
            var testLogs = await context.NotificationHistories
                .Where(n => n.NotificationType == "Test")
                .ToListAsync();
            if (testLogs.Count > 0)
            {
                context.NotificationHistories.RemoveRange(testLogs);
            }

            await context.SaveChangesAsync();
            Log.Information("Database initialized and verified successfully.");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to initialize SQLite database.");
            throw;
        }
    }
}
