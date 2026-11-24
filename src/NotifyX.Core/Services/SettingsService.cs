using Microsoft.EntityFrameworkCore;
using CodexBlaze.PaymentReminder.Core.Data;
using CodexBlaze.PaymentReminder.Core.Models;
using Serilog;

namespace CodexBlaze.PaymentReminder.Core.Services;

public interface ISettingsService
{
    Task<string> GetSettingAsync(string key, string defaultValue = "");
    Task<Dictionary<string, string>> GetAllSettingsAsync();
    Task SaveSettingAsync(string key, string value, string group = "General");
    Task SaveSettingsAsync(Dictionary<string, string> settings);
}

public class SettingsService : ISettingsService
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;

    public SettingsService(IDbContextFactory<AppDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<string> GetSettingAsync(string key, string defaultValue = "")
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var setting = await context.AppSettings.FindAsync(key);
        return setting?.Value ?? defaultValue;
    }

    public async Task<Dictionary<string, string>> GetAllSettingsAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.AppSettings.ToDictionaryAsync(s => s.Key, s => s.Value);
    }

    public async Task SaveSettingAsync(string key, string value, string group = "General")
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var setting = await context.AppSettings.FindAsync(key);
        if (setting == null)
        {
            setting = new AppSetting { Key = key, Value = value, Group = group, UpdatedAt = DateTime.UtcNow };
            await context.AppSettings.AddAsync(setting);
        }
        else
        {
            setting.Value = value;
            setting.Group = group;
            setting.UpdatedAt = DateTime.UtcNow;
        }

        await context.SaveChangesAsync();
    }

    public async Task SaveSettingsAsync(Dictionary<string, string> settings)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        foreach (var (key, value) in settings)
        {
            var setting = await context.AppSettings.FindAsync(key);
            if (setting == null)
            {
                await context.AppSettings.AddAsync(new AppSetting { Key = key, Value = value, UpdatedAt = DateTime.UtcNow });
            }
            else
            {
                setting.Value = value;
                setting.UpdatedAt = DateTime.UtcNow;
            }
        }
        await context.SaveChangesAsync();
        Log.Information("Saved {Count} application settings.", settings.Count);
    }
}
