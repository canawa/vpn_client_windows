using System.IO;
using System.Net.Http;
using System.Text;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CoffeeManiaVPN.Helpers;

public static class EmojiImageHelper
{
    private const string CdnBase = "https://cdn.jsdelivr.net/gh/twitter/twemoji@14.0.2/assets/72x72/";
    private static readonly HttpClient HttpClient = new() { Timeout = TimeSpan.FromSeconds(10) };
    private static readonly Dictionary<string, ImageSource> MemoryCache = new();
    private static readonly HashSet<string> PendingDownloads = new();

    public static event EventHandler<string>? EmojiCached;

    public static string CacheDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CoffeeManiaVPN", "emoji");

    public static ImageSource? TryGetImage(string emoji)
    {
        if (string.IsNullOrWhiteSpace(emoji))
            return null;

        if (MemoryCache.TryGetValue(emoji, out var cached))
            return cached;

        var path = GetCachePath(emoji);
        if (File.Exists(path))
        {
            var image = LoadBitmap(path);
            MemoryCache[emoji] = image;
            return image;
        }

        var bundled = GetBundledPath(emoji);
        if (bundled is not null)
        {
            var image = LoadBitmap(bundled);
            MemoryCache[emoji] = image;
            return image;
        }

        StartDownload(emoji);
        return null;
    }

    public static string ToFileName(string emoji)
    {
        var parts = new List<string>();
        for (var index = 0; index < emoji.Length;)
        {
            var codePoint = char.ConvertToUtf32(emoji, index);
            if (codePoint is not (0xFE0F or 0x200D))
                parts.Add(codePoint.ToString("x"));

            index += char.IsSurrogatePair(emoji, index) ? 2 : 1;
        }

        return parts.Count == 0 ? "2753.png" : string.Join("-", parts) + ".png";
    }

    private static string GetCachePath(string emoji) =>
        Path.Combine(CacheDirectory, ToFileName(emoji));

    private static string? GetBundledPath(string emoji)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Assets", "Emoji", ToFileName(emoji));
        return File.Exists(path) ? path : null;
    }

    private static void StartDownload(string emoji)
    {
        lock (PendingDownloads)
        {
            if (!PendingDownloads.Add(emoji))
                return;
        }

        _ = DownloadAsync(emoji);
    }

    private static async Task DownloadAsync(string emoji)
    {
        try
        {
            Directory.CreateDirectory(CacheDirectory);
            var fileName = ToFileName(emoji);
            var path = Path.Combine(CacheDirectory, fileName);
            var url = CdnBase + fileName;

            using var response = await HttpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
                return;

            await using var stream = await response.Content.ReadAsStreamAsync();
            await using var file = File.Create(path);
            await stream.CopyToAsync(file);

            MemoryCache[emoji] = LoadBitmap(path);
            EmojiCached?.Invoke(null, emoji);
        }
        catch
        {
            // ignored
        }
        finally
        {
            lock (PendingDownloads)
                PendingDownloads.Remove(emoji);
        }
    }

    private static ImageSource LoadBitmap(string path)
    {
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.UriSource = new Uri(path, UriKind.Absolute);
        image.EndInit();
        image.Freeze();
        return image;
    }
}
