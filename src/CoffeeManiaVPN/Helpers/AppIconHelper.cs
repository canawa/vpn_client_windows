using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CoffeeManiaVPN.Helpers;

public static class AppIconHelper
{
    public static ImageSource LoadAppIcon(int preferredSize = 32)
    {
        var icoPath = Path.Combine(AppPaths.AssetsDirectory, "app.ico");
        if (!File.Exists(icoPath))
            throw new FileNotFoundException("Не найден файл app.ico.", icoPath);

        using var stream = File.OpenRead(icoPath);
        var decoder = new IconBitmapDecoder(
            stream,
            BitmapCreateOptions.None,
            BitmapCacheOption.OnLoad);

        var frame = decoder.Frames
            .OrderBy(iconFrame => Math.Abs(iconFrame.PixelWidth - preferredSize))
            .First();
        frame.Freeze();
        return frame;
    }

    public static void ApplyWindowIcon(Window window) =>
        window.Icon = LoadAppIcon(32);
}
