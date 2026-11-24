using System.Windows;

namespace CodexBlaze.PaymentReminder.Wpf.Services;

public class ThemeManager
{
    private const string DarkThemeUri = "Themes/DarkTheme.xaml";
    private const string LightThemeUri = "Themes/LightTheme.xaml";

    public static string CurrentTheme { get; private set; } = "Dark";

    public static void ApplyTheme(string themeName)
    {
        var app = Application.Current;
        if (app == null) return;

        string targetUri = themeName.Equals("Light", StringComparison.OrdinalIgnoreCase) ? LightThemeUri : DarkThemeUri;

        var dictToRemove = app.Resources.MergedDictionaries
            .FirstOrDefault(d => d.Source != null && (d.Source.OriginalString.Contains("DarkTheme") || d.Source.OriginalString.Contains("LightTheme")));

        if (dictToRemove != null)
        {
            app.Resources.MergedDictionaries.Remove(dictToRemove);
        }

        app.Resources.MergedDictionaries.Insert(0, new ResourceDictionary
        {
            Source = new Uri(targetUri, UriKind.Relative)
        });

        CurrentTheme = themeName;
    }
}
