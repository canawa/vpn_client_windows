using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CoffeeManiaVPN.Helpers;

public static class AppIconHelper
{
    public static ImageSource LoadLogo(int size)
    {
        var pngPath = Path.Combine(AppContext.BaseDirectory, "Assets", "logo-white.png");
        if (!File.Exists(pngPath))
            throw new FileNotFoundException("Не найден файл logo-white.png.");

        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.UriSource = new Uri(pngPath, UriKind.Absolute);
        image.DecodePixelWidth = size;
        image.DecodePixelHeight = size;
        image.EndInit();
        image.Freeze();
        return image;
    }

    public static void ApplyWindowIcon(Window window) =>
        window.Icon = LoadLogo(32);
}
