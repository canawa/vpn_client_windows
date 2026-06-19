using System.Windows;

namespace CoffeeManiaVPN.Helpers;

public enum AppTheme
{
    Dark,
    Light
}

public static class ThemeManager
{
    private const string DarkThemeUri = "Themes/Colors.Dark.xaml";
    private const string LightThemeUri = "Themes/Colors.Light.xaml";

    public static AppTheme CurrentTheme { get; private set; } = AppTheme.Light;

    public static event EventHandler<AppTheme>? ThemeChanged;

    public static AppTheme Parse(string? value) =>
        string.Equals(value, "light", StringComparison.OrdinalIgnoreCase)
            ? AppTheme.Light
            : AppTheme.Dark;

    public static string ToSettingValue(AppTheme theme) =>
        theme == AppTheme.Light ? "light" : "dark";

    public static void Apply(AppTheme theme)
    {
        var app = Application.Current;
        if (app is null)
            return;

        var uri = new Uri(theme == AppTheme.Light ? LightThemeUri : DarkThemeUri, UriKind.Relative);
        var newDictionary = new ResourceDictionary { Source = uri };
        var merged = app.Resources.MergedDictionaries;

        var replaced = false;
        for (var i = 0; i < merged.Count; i++)
        {
            var source = merged[i].Source?.OriginalString;
            if (source is null ||
                (!source.Contains("Colors.", StringComparison.OrdinalIgnoreCase) &&
                 !source.EndsWith("Colors.xaml", StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            merged[i] = newDictionary;
            replaced = true;
            break;
        }

        if (!replaced)
            merged.Insert(0, newDictionary);

        CurrentTheme = theme;
        ThemeChanged?.Invoke(null, theme);
    }
}
