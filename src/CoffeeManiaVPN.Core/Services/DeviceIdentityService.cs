using System.Runtime.InteropServices;

namespace CoffeeManiaVPN.Core.Services;

public sealed class DeviceIdentityService
{
    private readonly AppSettings _settings;

    public DeviceIdentityService(AppSettings settings)
    {
        _settings = settings;
        EnsureHwid();
    }

    public string Hwid => _settings.Hwid;

    public IReadOnlyDictionary<string, string> GetSubscriptionHeaders()
    {
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["x-hwid"] = Hwid,
            ["x-device-os"] = "Windows",
            ["x-ver-os"] = Environment.OSVersion.Version.ToString(),
            ["x-device-model"] = GetDeviceModel()
        };
    }

    private void EnsureHwid()
    {
        if (!string.IsNullOrWhiteSpace(_settings.Hwid))
            return;

        _settings.Hwid = Guid.NewGuid().ToString("N");
        _settings.Save();
    }

    private static string GetDeviceModel()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            try
            {
                var model = Environment.GetEnvironmentVariable("COMPUTERNAME");
                if (!string.IsNullOrWhiteSpace(model))
                    return model;
            }
            catch
            {
                // ignored
            }
        }

        return "Windows PC";
    }
}
