using System.IO;
using Microsoft.Win32;

namespace CoffeeManiaVPN.Helpers;

public static class ProtocolRegistrationHelper
{
    private const string ProtocolName = "cfm";

    public static void EnsureRegistered()
    {
        if (!AdminElevationHelper.IsAdministrator())
            return;

        try
        {
            var exePath = ResolveExecutablePath();
            if (string.IsNullOrWhiteSpace(exePath) || !File.Exists(exePath))
                return;

            using var protocolKey = Registry.ClassesRoot.CreateSubKey(ProtocolName);
            if (protocolKey is null)
                return;

            protocolKey.SetValue(string.Empty, "URL:Кофемания ВПН");
            protocolKey.SetValue("URL Protocol", string.Empty);

            using var iconKey = protocolKey.CreateSubKey("DefaultIcon");
            iconKey?.SetValue(string.Empty, $"\"{exePath}\",0");

            using var shellKey = protocolKey.CreateSubKey(@"shell\open\command");
            shellKey?.SetValue(string.Empty, $"\"{exePath}\" \"%1\"");
        }
        catch
        {
            // ignored
        }
    }

    private static string ResolveExecutablePath()
    {
        var processPath = Environment.ProcessPath;
        if (!string.IsNullOrWhiteSpace(processPath) &&
            !processPath.EndsWith("dotnet.exe", StringComparison.OrdinalIgnoreCase))
        {
            return processPath;
        }

        var appHost = Path.Combine(AppContext.BaseDirectory, "CoffeeManiaVPN.exe");
        return File.Exists(appHost) ? appHost : processPath ?? string.Empty;
    }
}
