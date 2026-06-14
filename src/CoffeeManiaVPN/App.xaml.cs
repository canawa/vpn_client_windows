using System.Windows;
using CoffeeManiaVPN.Helpers;

namespace CoffeeManiaVPN;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        PreloadServerEmojis();
    }

    private static void PreloadServerEmojis()
    {
        foreach (var emoji in new[] { "🦎", "🌱", "🤏", "🟢", "⚪", "⚫" })
            EmojiImageHelper.TryGetImage(emoji);
    }
}
