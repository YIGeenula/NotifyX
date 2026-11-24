using Microsoft.Win32;
using System.IO;
using System.Reflection;
using Serilog;

namespace CodexBlaze.PaymentReminder.Wpf.Services;

public static class WindowsStartupManager
{
    private const string StartupRegistryKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "NotifyXPaymentReminder";
    private const string LegacyAppName = "CodexBlazePaymentReminder";

    public static void SetStartup(bool enable)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(StartupRegistryKey, true);
            if (key == null) return;

            // Clean legacy key if present
            key.DeleteValue(LegacyAppName, false);

            if (enable)
            {
                string? exePath = Environment.ProcessPath;
                if (string.IsNullOrEmpty(exePath))
                {
                    exePath = Assembly.GetExecutingAssembly().Location;
                    if (exePath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                    {
                        exePath = Path.ChangeExtension(exePath, ".exe");
                    }
                }

                // Add --minimized argument so it starts silently in background
                string command = $"\"{exePath}\" --minimized";
                key.SetValue(AppName, command);
                Log.Information("Registered Windows startup: {Command}", command);
            }
            else
            {
                key.DeleteValue(AppName, false);
                Log.Information("Removed from Windows startup.");
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to update Windows startup registry key.");
        }
    }

    public static bool IsStartupEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(StartupRegistryKey, false);
            return key?.GetValue(AppName) != null || key?.GetValue(LegacyAppName) != null;
        }
        catch
        {
            return false;
        }
    }
}
