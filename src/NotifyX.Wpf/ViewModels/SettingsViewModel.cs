using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CodexBlaze.PaymentReminder.Core.Models;
using CodexBlaze.PaymentReminder.Core.Services;
using CodexBlaze.PaymentReminder.Wpf.Services;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.IO;

namespace CodexBlaze.PaymentReminder.Wpf.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settingsService;
    private readonly IExcelExportImportService _excelService;
    private readonly IBackupRestoreService _backupService;
    private readonly INotificationService _notificationService;
    private readonly ICloudSyncService _cloudSyncService;
    private readonly IPurchaseService _purchaseService;
    private readonly IUpdateService _updateService;

    [ObservableProperty]
    private string _currentAppVersion = "1.0.1";

    [ObservableProperty]
    private bool _isCheckingUpdate;

    [ObservableProperty]
    private bool _isUpdateAvailable;

    [ObservableProperty]
    private bool _isDownloadingUpdate;

    [ObservableProperty]
    private double _downloadProgress;

    [ObservableProperty]
    private string _updateInfoText = string.Empty;

    [ObservableProperty]
    private string _latestVersionTag = string.Empty;

    [ObservableProperty]
    private string _releaseNotes = string.Empty;

    private string? _downloadUrl;
    private string? _downloadedFilePath;

    [ObservableProperty]
    private string _currentTheme = "Dark";

    [ObservableProperty]
    private string _userVaultId = string.Empty;

    [ObservableProperty]
    private string _googleSheetSyncUrl = CloudSyncService.DefaultSyncUrl;

    [ObservableProperty]
    private bool _notificationsEnabled = true;

    [ObservableProperty]
    private bool _remind7Days = true;

    [ObservableProperty]
    private bool _remind3Days = true;

    [ObservableProperty]
    private bool _remind1Day = true;

    [ObservableProperty]
    private bool _remindDueDay = true;

    [ObservableProperty]
    private bool _remindOverdue = true;

    [ObservableProperty]
    private bool _startWithWindows;

    [ObservableProperty]
    private bool _minimizeToTray = true;

    [ObservableProperty]
    private bool _closeToTray = true;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public ObservableCollection<NotificationHistory> NotificationHistoryList { get; } = new();

    public SettingsViewModel(
        ISettingsService settingsService,
        IExcelExportImportService excelService,
        IBackupRestoreService backupService,
        INotificationService notificationService,
        ICloudSyncService cloudSyncService,
        IPurchaseService purchaseService,
        IUpdateService updateService)
    {
        _settingsService = settingsService;
        _excelService = excelService;
        _backupService = backupService;
        _notificationService = notificationService;
        _cloudSyncService = cloudSyncService;
        _purchaseService = purchaseService;
        _updateService = updateService;
        _currentAppVersion = _updateService.CurrentVersion;
    }

    [RelayCommand]
    public async Task LoadSettingsAsync()
    {
        var settings = await _settingsService.GetAllSettingsAsync();

        if (settings.TryGetValue("Theme", out var theme)) CurrentTheme = theme;
        if (settings.TryGetValue("GoogleSheetSyncUrl", out var gUrl) && !string.IsNullOrWhiteSpace(gUrl)) GoogleSheetSyncUrl = gUrl;
        UserVaultId = await _cloudSyncService.GetUserVaultIdAsync();
        if (settings.TryGetValue("NotificationsEnabled", out var ne) && bool.TryParse(ne, out var neb)) NotificationsEnabled = neb;
        if (settings.TryGetValue("Remind7Days", out var r7) && bool.TryParse(r7, out var r7b)) Remind7Days = r7b;
        if (settings.TryGetValue("Remind3Days", out var r3) && bool.TryParse(r3, out var r3b)) Remind3Days = r3b;
        if (settings.TryGetValue("Remind1Day", out var r1) && bool.TryParse(r1, out var r1b)) Remind1Day = r1b;
        if (settings.TryGetValue("RemindDueDay", out var rd) && bool.TryParse(rd, out var rdb)) RemindDueDay = rdb;
        if (settings.TryGetValue("RemindOverdue", out var ro) && bool.TryParse(ro, out var rob)) RemindOverdue = rob;
        if (settings.TryGetValue("StartWithWindows", out var sw) && bool.TryParse(sw, out var swb)) StartWithWindows = swb;
        if (settings.TryGetValue("MinimizeToTray", out var mt) && bool.TryParse(mt, out var mtb)) MinimizeToTray = mtb;
        if (settings.TryGetValue("CloseToTray", out var ct) && bool.TryParse(ct, out var ctb)) CloseToTray = ctb;

        await LoadNotificationsAsync();
    }

    [RelayCommand]
    public async Task SaveSettingsAsync()
    {
        var dict = new Dictionary<string, string>
        {
            ["Theme"] = CurrentTheme,
            ["GoogleSheetSyncUrl"] = GoogleSheetSyncUrl,
            ["NotificationsEnabled"] = NotificationsEnabled.ToString(),
            ["Remind7Days"] = Remind7Days.ToString(),
            ["Remind3Days"] = Remind3Days.ToString(),
            ["Remind1Day"] = Remind1Day.ToString(),
            ["RemindDueDay"] = RemindDueDay.ToString(),
            ["RemindOverdue"] = RemindOverdue.ToString(),
            ["StartWithWindows"] = StartWithWindows.ToString(),
            ["MinimizeToTray"] = MinimizeToTray.ToString(),
            ["CloseToTray"] = CloseToTray.ToString()
        };

        await _settingsService.SaveSettingsAsync(dict);
        WindowsStartupManager.SetStartup(StartWithWindows);
        ThemeManager.ApplyTheme(CurrentTheme);
        StatusMessage = "Settings saved successfully! Startup registration updated.";
    }

    [RelayCommand]
    public async Task PullFromGoogleSheetAsync()
    {
        StatusMessage = "Pulling records from Google Sheet into local database...";
        int pulled = await _cloudSyncService.PullFromCloudAsync();
        StatusMessage = pulled > 0
            ? $"Successfully imported {pulled} new purchases from Google Sheet!"
            : "Local database is already up to date with Google Sheet.";
    }

    [RelayCommand]
    public async Task SyncAllToGoogleSheetAsync()
    {
        StatusMessage = "Syncing all local purchases to Google Sheet...";
        var allPurchases = await _purchaseService.GetAllPurchasesAsync();
        bool ok = await _cloudSyncService.SyncAllPurchasesAsync(allPurchases);
        if (ok)
        {
            StatusMessage = $"Successfully synced {allPurchases.Count} purchases to your Google Sheet!";
        }
        else
        {
            StatusMessage = "Sync completed with warnings. Check your connection or Google Webhook URL.";
        }
    }

    [RelayCommand]
    public async Task SwitchThemeAsync(string theme)
    {
        CurrentTheme = theme;
        ThemeManager.ApplyTheme(theme);
        await _settingsService.SaveSettingAsync("Theme", theme);
        StatusMessage = $"Theme changed to {theme} and saved.";
    }

    [RelayCommand]
    public async Task ExportExcelAsync()
    {
        var sfd = new SaveFileDialog
        {
            Filter = "Excel Workbook (*.xlsx)|*.xlsx",
            FileName = $"NotifyX_Export_{DateTime.Now:yyyyMMdd}.xlsx"
        };

        if (sfd.ShowDialog() == true)
        {
            await _excelService.ExportToExcelAsync(sfd.FileName);
            StatusMessage = $"Excel exported successfully to: {Path.GetFileName(sfd.FileName)}";
        }
    }

    [RelayCommand]
    public async Task ImportExcelAsync()
    {
        var ofd = new OpenFileDialog
        {
            Filter = "Excel Workbook (*.xlsx)|*.xlsx"
        };

        if (ofd.ShowDialog() == true)
        {
            var res = await _excelService.ImportFromExcelAsync(ofd.FileName);
            if (res.Success)
            {
                StatusMessage = $"Import completed! {res.PurchasesImported} purchases, {res.InstallmentsImported} installments imported.";
            }
            else
            {
                StatusMessage = $"Import encountered issues: {string.Join(", ", res.Errors.Take(3))}";
            }
        }
    }

    [RelayCommand]
    public async Task CreateBackupAsync()
    {
        var sfd = new SaveFileDialog
        {
            Filter = "NotifyX Backup (*.cbz)|*.cbz",
            FileName = $"NotifyX_Backup_{DateTime.Now:yyyyMMdd_HHmmss}.cbz"
        };

        if (sfd.ShowDialog() == true)
        {
            string dir = Path.GetDirectoryName(sfd.FileName)!;
            string created = await _backupService.CreateBackupAsync(dir);
            StatusMessage = $"Backup created successfully: {Path.GetFileName(created)}";
        }
    }

    [RelayCommand]
    public async Task RestoreBackupAsync()
    {
        var ofd = new OpenFileDialog
        {
            Filter = "NotifyX Backup (*.cbz)|*.cbz"
        };

        if (ofd.ShowDialog() == true)
        {
            bool ok = await _backupService.RestoreBackupAsync(ofd.FileName);
            if (ok)
            {
                StatusMessage = "Backup restored successfully! Please restart the application if needed.";
            }
        }
    }

    [RelayCommand]
    public async Task ClearNotificationLogsAsync()
    {
        await _notificationService.ClearNotificationsAsync();
        NotificationHistoryList.Clear();
        StatusMessage = "Notification logs cleared successfully.";
    }

    [RelayCommand]
    public async Task TriggerTestNotificationAsync()
    {
        var testItem = new NotificationItem("Payment Reminder Alert!", "Your installment of LKR 12,500.00 is due tomorrow for 'Sony WH-1000XM5'.", "Test", null, null);
        await _notificationService.LogNotificationAsync(testItem);
        CodexBlaze.PaymentReminder.Wpf.Services.ToastNotificationManager.ShowToast(testItem.Title, testItem.Message);
        await LoadNotificationsAsync();
        StatusMessage = "Test toast notification displayed and logged!";
    }

    [RelayCommand]
    public async Task CheckForUpdatesAsync()
    {
        IsCheckingUpdate = true;
        StatusMessage = "Checking for new software releases on GitHub...";
        UpdateInfoText = "Checking for updates...";

        try
        {
            var result = await _updateService.CheckForUpdatesAsync();
            if (result.IsUpdateAvailable)
            {
                IsUpdateAvailable = true;
                LatestVersionTag = result.LatestVersion;
                ReleaseNotes = string.IsNullOrWhiteSpace(result.ReleaseNotes) ? "New update available with performance and security improvements." : result.ReleaseNotes;
                _downloadUrl = result.DownloadUrl;
                UpdateInfoText = $"🎉 New version v{result.LatestVersion} is available! (Current: v{result.CurrentVersion})";
                StatusMessage = $"Update available: v{result.LatestVersion}";
            }
            else
            {
                IsUpdateAvailable = false;
                UpdateInfoText = $"NotifyX is up to date! (v{CurrentAppVersion})";
                StatusMessage = $"You are using the latest version (v{CurrentAppVersion}).";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = "Failed to check for updates: " + ex.Message;
            UpdateInfoText = "Unable to check updates. Please check your internet connection.";
        }
        finally
        {
            IsCheckingUpdate = false;
        }
    }

    [RelayCommand]
    public async Task DownloadAndInstallUpdateAsync()
    {
        if (string.IsNullOrEmpty(_downloadUrl))
        {
            StatusMessage = "No direct download link available for this release. Please visit GitHub Releases.";
            return;
        }

        try
        {
            IsDownloadingUpdate = true;
            DownloadProgress = 0;
            StatusMessage = "Downloading latest update package...";

            var progress = new Progress<double>(p => DownloadProgress = Math.Round(p, 1));
            _downloadedFilePath = await _updateService.DownloadUpdateAsync(_downloadUrl, progress);

            StatusMessage = "Download complete! Applying update and restarting NotifyX...";
            await Task.Delay(1000);

            _updateService.ApplyUpdateAndRestart(_downloadedFilePath);
        }
        catch (Exception ex)
        {
            StatusMessage = "Error updating software: " + ex.Message;
            UpdateInfoText = "Update installation failed: " + ex.Message;
        }
        finally
        {
            IsDownloadingUpdate = false;
        }
    }

    [RelayCommand]
    public void OpenReleasesPage()
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "https://github.com/YIGeenula/NotifyX/releases",
                UseShellExecute = true
            };
            System.Diagnostics.Process.Start(psi);
        }
        catch (Exception ex)
        {
            StatusMessage = "Could not open browser: " + ex.Message;
        }
    }

    [RelayCommand]
    public async Task OpenWebDashboardAsync()
    {
        try
        {
            string vault = await _cloudSyncService.GetUserVaultIdAsync();
            string endpoint = await _cloudSyncService.GetSyncEndpointAsync();

            string url = "https://yigeenula.github.io/NotifyX/";
            var queryParams = new List<string>();

            if (!string.IsNullOrWhiteSpace(vault))
            {
                queryParams.Add($"vault={Uri.EscapeDataString(vault)}");
            }
            if (!string.IsNullOrWhiteSpace(endpoint))
            {
                queryParams.Add($"api={Uri.EscapeDataString(endpoint)}");
            }

            if (queryParams.Count > 0)
            {
                url += "?" + string.Join("&", queryParams);
            }

            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            };
            System.Diagnostics.Process.Start(psi);
        }
        catch (Exception ex)
        {
            StatusMessage = "Could not open Web Dashboard: " + ex.Message;
        }
    }

    [RelayCommand]
    public async Task CopyVaultIdAsync()
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(UserVaultId))
            {
                System.Windows.Clipboard.SetText(UserVaultId);
                StatusMessage = $"Vault ID '{UserVaultId}' copied to clipboard!";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = "Failed to copy: " + ex.Message;
        }
    }

    [RelayCommand]
    public async Task LoadNotificationsAsync()
    {
        NotificationHistoryList.Clear();
        var list = await _notificationService.GetRecentNotificationsAsync(20);
        foreach (var item in list) NotificationHistoryList.Add(item);
    }
}
