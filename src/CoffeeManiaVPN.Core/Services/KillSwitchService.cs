using System.Diagnostics;

namespace CoffeeManiaVPN.Core.Services;

public sealed class KillSwitchService
{
    private const string BlockRule = "CoffeeManiaVPN-KillSwitch-Block";
    private const string AllowXrayRule = "CoffeeManiaVPN-KillSwitch-Allow-Xray";
    private const string AllowAppRule = "CoffeeManiaVPN-KillSwitch-Allow-App";
    private const string AllowLoopbackRule = "CoffeeManiaVPN-KillSwitch-Allow-Loopback";

    public bool IsEngaged { get; private set; }

    public void Engage(string xrayExePath, string appExePath)
    {
        Disengage();

        RunNetsh($"advfirewall firewall add rule name=\"{AllowXrayRule}\" dir=out action=allow enable=yes program=\"{xrayExePath}\" profile=any");
        RunNetsh($"advfirewall firewall add rule name=\"{AllowAppRule}\" dir=out action=allow enable=yes program=\"{appExePath}\" profile=any");
        RunNetsh($"advfirewall firewall add rule name=\"{AllowLoopbackRule}\" dir=out action=allow enable=yes remoteip=127.0.0.0/8 profile=any");
        RunNetsh($"advfirewall firewall add rule name=\"{BlockRule}\" dir=out action=block enable=yes profile=any");

        IsEngaged = true;
    }

    public void Disengage()
    {
        foreach (var rule in new[] { BlockRule, AllowXrayRule, AllowAppRule, AllowLoopbackRule })
            RunNetsh($"advfirewall firewall delete rule name=\"{rule}\"", ignoreErrors: true);

        IsEngaged = false;
    }

    private static void RunNetsh(string arguments, bool ignoreErrors = false)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "netsh",
            Arguments = arguments,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        using var process = Process.Start(startInfo);
        if (process is null)
        {
            if (!ignoreErrors)
                throw new InvalidOperationException("Не удалось запустить netsh для Kill Switch.");
            return;
        }

        process.WaitForExit();

        if (!ignoreErrors && process.ExitCode != 0)
            throw new InvalidOperationException("Не удалось применить правила Kill Switch. Запустите приложение от имени администратора.");
    }
}
