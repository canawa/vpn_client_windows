using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CoffeeManiaVPN.Helpers;

public static class CountryFlagHelper
{
    private static readonly Dictionary<string, ImageSource> Cache = new(StringComparer.OrdinalIgnoreCase);

    public static ImageSource? GetImage(string countryCode)
    {
        var code = string.IsNullOrWhiteSpace(countryCode)
            ? "un"
            : countryCode.Trim().ToLowerInvariant();

        if (Cache.TryGetValue(code, out var cached))
            return cached;

        var path = Path.Combine(AppPaths.AssetsDirectory, "Flags", $"{code}.png");
        if (!File.Exists(path))
        {
            path = Path.Combine(AppPaths.AssetsDirectory, "Flags", "un.png");
            if (!File.Exists(path))
                return null;
        }

        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.UriSource = new Uri(path, UriKind.Absolute);
        image.EndInit();
        image.Freeze();

        Cache[code] = image;
        return image;
    }
}
