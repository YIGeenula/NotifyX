using System.Windows;
using CodexBlaze.PaymentReminder.Wpf.Views;

namespace CodexBlaze.PaymentReminder.Wpf.Services;

public static class ToastNotificationManager
{
    public static void ShowToast(string title, string message)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            try
            {
                var toast = new NotificationToastWindow(title, message);
                toast.Show();
            }
            catch { }
        });
    }
}
