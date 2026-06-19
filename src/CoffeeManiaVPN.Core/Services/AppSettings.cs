using System.Text.Json;
using CoffeeManiaVPN.Core.Xray;

namespace CoffeeManiaVPN.Core.Services;

public sealed class AppSettings : ISplitTunnelSettings
{
    public const string SplitTunnelModeBypass = "bypass";
    public const string SplitTunnelModeProxyOnly = "proxyOnly";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public string SubscriptionUrl { get; set; } = string.Empty;
    public int SelectedNodeIndex { get; set; } = -1;
    public string Hwid { get; set; } = string.Empty;
    public bool AutoUpdateSubscription { get; set; } = true;
    public int AutoUpdateIntervalMinutes { get; set; } = 60;

    public bool SiteSplitTunnelEnabled { get; set; }
    public string SiteSplitTunnelMode { get; set; } = SplitTunnelModeBypass;
    public List<string> SiteSplitTunnelDomains { get; set; } = [];
    IReadOnlyList<string> ISplitTunnelSettings.SiteSplitTunnelDomains => SiteSplitTunnelDomains;

    public bool AppSplitTunnelEnabled { get; set; }
    public string AppSplitTunnelMode { get; set; } = SplitTunnelModeBypass;
    public List<string> AppSplitTunnelApps { get; set; } = [];
    IReadOnlyList<string> ISplitTunnelSettings.AppSplitTunnelApps => AppSplitTunnelApps;

    public bool KillSwitchEnabled { get; set; }

    public string Theme { get; set; } = "light";

    public bool CompactSidebar { get; set; }

    public bool SimplifiedAnimations { get; set; }

    public static int NormalizeAutoUpdateIntervalMinutes(int minutes) =>
        minutes switch
        {
            180 => 180,
            360 => 360,
            1440 => 1440,
            _ => 60
        };

    public static string NormalizeSplitTunnelMode(string? mode) =>
        string.Equals(mode, SplitTunnelModeProxyOnly, StringComparison.OrdinalIgnoreCase)
            ? SplitTunnelModeProxyOnly
            : SplitTunnelModeBypass;

    public static string NormalizeTheme(string? theme) =>
        string.Equals(theme, "dark", StringComparison.OrdinalIgnoreCase) ? "dark" : "light";

    public static string SettingsPath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "CoffeeManiaVPN",
            "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsPath))
                return new AppSettings();

            var json = File.ReadAllText(SettingsPath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            settings.SiteSplitTunnelMode = NormalizeSplitTunnelMode(settings.SiteSplitTunnelMode);
            settings.AppSplitTunnelMode = NormalizeSplitTunnelMode(settings.AppSplitTunnelMode);
            settings.SiteSplitTunnelDomains ??= [];
            settings.AppSplitTunnelApps ??= [];
            settings.Theme = NormalizeTheme(settings.Theme);
            return settings;
        }
        catch
        {
            return new AppSettings();
        }
    }

    public void Save()
    {
        SiteSplitTunnelMode = NormalizeSplitTunnelMode(SiteSplitTunnelMode);
        AppSplitTunnelMode = NormalizeSplitTunnelMode(AppSplitTunnelMode);
        Theme = NormalizeTheme(Theme);
        SiteSplitTunnelDomains = SiteSplitTunnelDomains
            .Where(static domain => !string.IsNullOrWhiteSpace(domain))
            .Select(static domain => domain.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        AppSplitTunnelApps = AppSplitTunnelApps
            .Where(static path => !string.IsNullOrWhiteSpace(path))
            .Select(static path => path.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var directory = Path.GetDirectoryName(SettingsPath)!;
        Directory.CreateDirectory(directory);
        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(this, JsonOptions));
    }
}
