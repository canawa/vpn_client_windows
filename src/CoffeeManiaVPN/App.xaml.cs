using System.Windows;
using CoffeeManiaVPN.Core.Services;
using CoffeeManiaVPN.Helpers;

namespace CoffeeManiaVPN;

public partial class App : Application
{
    private SingleInstanceService? _singleInstance;

    protected override void OnStartup(StartupEventArgs e)
    {
        var urlScheme = UrlSchemeParser.FindInCommandLine()
            ?? UrlSchemeParser.FindInArguments(e.Args);

        var canRunWithoutElevation = UrlSchemeParser.CanRunWithoutElevation(urlScheme);

        if (!AdminElevationHelper.IsAdministrator() && !canRunWithoutElevation)
        {
            var forwarded = !string.IsNullOrWhiteSpace(urlScheme)
                ? SingleInstanceService.TryForwardToRunningInstance(urlScheme)
                : SingleInstanceService.TryForwardToRunningInstance(SingleInstanceService.ActivateMessage);

            if (forwarded)
            {
                Shutdown();
                return;
            }

            if (AdminElevationHelper.TryEnsureAdministrator())
            {
                Shutdown();
                return;
            }

            urlScheme = UrlSchemeParser.FindInCommandLine()
                ?? UrlSchemeParser.FindInArguments(e.Args);
        }

        var settings = AppSettings.Load();
        ThemeManager.Apply(ThemeManager.Parse(settings.Theme));

        ProtocolRegistrationHelper.EnsureRegistered();

        _singleInstance = SingleInstanceService.Create();

        if (!_singleInstance.IsPrimary)
        {
            if (!string.IsNullOrWhiteSpace(urlScheme))
                _singleInstance.TrySendToPrimary(urlScheme);
            else
                _singleInstance.TrySendToPrimary(SingleInstanceService.ActivateMessage);

            Shutdown();
            return;
        }

        base.OnStartup(e);
        PreloadServerEmojis();

        var mainWindow = new MainWindow();
        MainWindow = mainWindow;

        _singleInstance.StartListening(message =>
        {
            mainWindow.Dispatcher.BeginInvoke(() =>
            {
                if (string.Equals(message, SingleInstanceService.ActivateMessage, StringComparison.Ordinal))
                {
                    mainWindow.ActivateFromExternalRequest();
                    return;
                }

                mainWindow.HandleUrlScheme(message);
            });
        });

        var pendingUrlScheme = urlScheme;
        mainWindow.Loaded += (_, _) =>
        {
            if (!string.IsNullOrWhiteSpace(pendingUrlScheme))
                mainWindow.HandleUrlScheme(pendingUrlScheme);
        };

        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _singleInstance?.Dispose();
        base.OnExit(e);
    }

    private static void PreloadServerEmojis()
    {
        foreach (var emoji in new[] { "🦎", "🌱", "🤏", "🟢", "⚪", "⚫" })
            EmojiImageHelper.TryGetImage(emoji);
    }
}
