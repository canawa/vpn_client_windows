using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using System.Windows;

namespace CoffeeManiaVPN.Helpers;

public static class AdminElevationHelper
{
    private const string ElevatedMarker = "--elevated";

    public static bool TryEnsureAdministrator()
    {
        if (IsAdministrator())
            return true;

        if (HasElevationMarker())
            return false;

        try
        {
            var executablePath = ResolveExecutablePath();
            var arguments = BuildRelaunchArguments();

            Process.Start(new ProcessStartInfo
            {
                FileName = executablePath,
                Arguments = arguments,
                UseShellExecute = true,
                Verb = "runas",
                WorkingDirectory = AppContext.BaseDirectory
            });

            return true;
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            MessageBox.Show(
                "Для работы VPN требуются права администратора.\nПодтвердите запрос UAC или запустите приложение от имени администратора.",
                "КОФЕМАНИЯ ВПН",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Не удалось запустить приложение с правами администратора:\n{ex.Message}",
                "КОФЕМАНИЯ ВПН",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }

        return false;
    }

    public static bool IsAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    private static bool HasElevationMarker() =>
        Environment.GetCommandLineArgs().Any(static arg =>
            string.Equals(arg, ElevatedMarker, StringComparison.OrdinalIgnoreCase));

    private static string ResolveExecutablePath()
    {
        var processPath = Environment.ProcessPath;
        if (!string.IsNullOrWhiteSpace(processPath) &&
            !processPath.EndsWith("dotnet.exe", StringComparison.OrdinalIgnoreCase))
        {
            return processPath;
        }

        var appHost = Path.Combine(AppContext.BaseDirectory, "CoffeeManiaVPN.exe");
        if (File.Exists(appHost))
            return appHost;

        if (!string.IsNullOrWhiteSpace(processPath))
            return processPath;

        throw new InvalidOperationException("Не удалось определить путь к исполняемому файлу.");
    }

    private static string BuildRelaunchArguments()
    {
        var args = Environment.GetCommandLineArgs()
            .Skip(1)
            .Where(static arg => !string.Equals(arg, ElevatedMarker, StringComparison.OrdinalIgnoreCase))
            .ToList();

        args.Add(ElevatedMarker);
        return string.Join(" ", args.Select(QuoteArgument));
    }

    private static string QuoteArgument(string argument)
    {
        if (string.IsNullOrEmpty(argument))
            return "\"\"";

        var needsQuotes = argument.Contains(' ') ||
                          argument.Contains('"') ||
                          argument.Contains(':') ||
                          argument.Contains('/') ||
                          argument.Contains('&') ||
                          argument.Contains('=');

        return needsQuotes
            ? $"\"{argument.Replace("\"", "\\\"")}\""
            : argument;
    }
}
