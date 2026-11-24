using System.IO;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using CodexBlaze.PaymentReminder.Core.Data;
using CodexBlaze.PaymentReminder.Core.Services;
using CodexBlaze.PaymentReminder.Wpf.Services;
using CodexBlaze.PaymentReminder.Wpf.ViewModels;
using Serilog;

namespace CodexBlaze.PaymentReminder.Wpf;

public partial class App : Application
{
    private IHost? _host;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        string appDataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CodexBlazePaymentReminder");

        Directory.CreateDirectory(appDataDir);
        string dbPath = Path.Combine(appDataDir, "paymentreminder.db");
        string logPath = Path.Combine(appDataDir, "logs", "app.log");

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(logPath, rollingInterval: RollingInterval.Day)
            .CreateLogger();

        Log.Information("Starting CODEXBLAZE PAYMENT REMINDER desktop application...");

        _host = Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                services.AddDbContextFactory<AppDbContext>(options =>
                    options.UseSqlite($"Data Source={dbPath}"));

                // Core Services
                services.AddSingleton<DatabaseInitializer>();
                services.AddSingleton<InstallmentCalculationService>();
                services.AddScoped<IPurchaseService, PurchaseService>();
                services.AddScoped<IPaymentService, PaymentService>();
                services.AddScoped<IDashboardService, DashboardService>();
                services.AddScoped<IExcelExportImportService, ExcelExportImportService>();
                services.AddScoped<IReportExportService, ReportExportService>();
                services.AddScoped<INotificationService, NotificationService>();
                services.AddScoped<ISettingsService, SettingsService>();
                services.AddScoped<ICloudSyncService, CloudSyncService>();
                services.AddSingleton<IUpdateService, UpdateService>();
                services.AddSingleton<IBackupRestoreService>(sp =>
                    new BackupRestoreService(sp.GetRequiredService<IDbContextFactory<AppDbContext>>(), dbPath));

                // ViewModels
                services.AddSingleton<DashboardViewModel>();
                services.AddSingleton<PurchasesViewModel>();
                services.AddSingleton<PaymentsViewModel>();
                services.AddSingleton<CalendarViewModel>();
                services.AddSingleton<ReportsViewModel>();
                services.AddSingleton<SettingsViewModel>();
                services.AddSingleton<MainViewModel>();

                // Windows
                services.AddSingleton<MainWindow>();
            })
            .Build();

        await _host.StartAsync();

        // Initialize Database & apply seeds
        var dbInit = _host.Services.GetRequiredService<DatabaseInitializer>();
        await dbInit.InitializeAsync();

        // Load saved theme
        var settingsService = _host.Services.GetRequiredService<ISettingsService>();
        string savedTheme = await settingsService.GetSettingAsync("Theme", "Dark");
        ThemeManager.ApplyTheme(savedTheme);

        // Ensure Windows Startup setting is enforced
        string startWithWin = await settingsService.GetSettingAsync("StartWithWindows", "True");
        if (bool.TryParse(startWithWin, out bool enableStartup) && enableStartup)
        {
            WindowsStartupManager.SetStartup(true);
        }

        // Set up recurring background reminder scheduler (every 60 minutes)
        var mainVm = _host.Services.GetRequiredService<MainViewModel>();
        var reminderTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMinutes(60)
        };
        reminderTimer.Tick += async (s, args) =>
        {
            try
            {
                await mainVm.CheckNotificationsAsync();
            }
            catch { }
        };
        reminderTimer.Start();

        var mainWindow = _host.Services.GetRequiredService<MainWindow>();

        // Check if launched with --minimized flag (e.g., at Windows Boot)
        bool startMinimized = e.Args.Any(a => a.Equals("--minimized", StringComparison.OrdinalIgnoreCase));
        if (!startMinimized)
        {
            mainWindow.Show();
        }
        else
        {
            // Running silently in background
            Log.Information("Started silently in background at Windows boot.");
        }
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host != null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }
        Log.CloseAndFlush();
        base.OnExit(e);
    }
}
