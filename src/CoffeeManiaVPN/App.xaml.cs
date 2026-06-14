using System.Windows;
using CoffeeManiaVPN.Helpers;

namespace CoffeeManiaVPN;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        PreloadBundledEmojis();
    }

    private static void PreloadBundledEmojis()
    {
        foreach (var emoji in new[] { "🌐", "⚙", "📡", "🔗", "🎨", "🐞", "🛡", "🦎", "🌱", "🤏", "🟢", "⚪", "⚫" })
            EmojiImageHelper.TryGetImage(emoji);
    }
}

