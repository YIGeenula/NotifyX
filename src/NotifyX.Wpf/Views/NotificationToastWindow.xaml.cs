using System.Windows;
using System.Windows.Threading;

namespace CodexBlaze.PaymentReminder.Wpf.Views;

public partial class NotificationToastWindow : Window
{
    private readonly DispatcherTimer _autoCloseTimer;

    public NotificationToastWindow(string title, string message)
    {
        InitializeComponent();

        ToastTitle.Text = title;
        ToastMessage.Text = message;

        PositionAtBottomRight();

        // Auto close after 6 seconds
        _autoCloseTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(6)
        };
        _autoCloseTimer.Tick += (s, e) =>
        {
            _autoCloseTimer.Stop();
            CloseToast();
        };
        _autoCloseTimer.Start();
    }

    private void PositionAtBottomRight()
    {
        var workArea = SystemParameters.WorkArea;
        Left = workArea.Right - Width - 16;
        Top = workArea.Bottom - Height - 16;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        CloseToast();
    }

    private void CloseToast()
    {
        try
        {
            Close();
        }
        catch { }
    }
}
