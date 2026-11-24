using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using CodexBlaze.PaymentReminder.Core.Models;

namespace CodexBlaze.PaymentReminder.Wpf.Converters;

public class StatusToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is PaymentStatus status)
        {
            return status switch
            {
                PaymentStatus.Paid => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2ECC8A")),
                PaymentStatus.PartiallyPaid => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#21C8B7")),
                PaymentStatus.DueToday => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F5B942")),
                PaymentStatus.DueSoon => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#38A3F8")),
                PaymentStatus.Overdue => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F06464")),
                _ => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#929BB0"))
            };
        }

        if (value is PurchaseStatus pStatus)
        {
            return pStatus switch
            {
                PurchaseStatus.Completed => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2ECC8A")),
                PurchaseStatus.Active => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#7C5CFC")),
                PurchaseStatus.Defaulted => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F06464")),
                _ => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#929BB0"))
            };
        }

        return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#929BB0"));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class CurrencyFormatter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is decimal val)
        {
            return $"LKR {val:N2}";
        }
        return "LKR 0.00";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class CountToZeroVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int count)
        {
            return count == 0 ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
        }
        return System.Windows.Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}
