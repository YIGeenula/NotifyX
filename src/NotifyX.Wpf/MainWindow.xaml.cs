using System.Windows;
using CodexBlaze.PaymentReminder.Wpf.ViewModels;

namespace CodexBlaze.PaymentReminder.Wpf;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public bool IsExplicitExit { get; set; } = false;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;

        Loaded += MainWindow_Loaded;
        Closing += MainWindow_Closing;
    }

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (!IsExplicitExit)
        {
            e.Cancel = true;
            Hide();
            Services.ToastNotificationManager.ShowToast(
                "Running in Background",
                "CODEXBLAZE Payment Reminder is active in the background and monitoring your due dates.");
        }
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        // Auto-pull existing records from cloud/Google Sheet on launch
        try
        {
            await _viewModel.SettingsVM.PullFromGoogleSheetCommand.ExecuteAsync(null);
        }
        catch { }

        await _viewModel.NavigateAsync("Dashboard");
        await _viewModel.CheckNotificationsAsync();
    }

    private void NotifyIcon_TrayMouseDoubleClick(object sender, RoutedEventArgs e)
    {
        ShowWindow();
    }

    private void MenuOpen_Click(object sender, RoutedEventArgs e)
    {
        ShowWindow();
    }

    private async void MenuCheckReminders_Click(object sender, RoutedEventArgs e)
    {
        await _viewModel.CheckNotificationsAsync();
    }

    private void MenuExit_Click(object sender, RoutedEventArgs e)
    {
        IsExplicitExit = true;
        NotifyIcon.Dispose();
        Application.Current.Shutdown();
    }

    private void ShowWindow()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }
}
