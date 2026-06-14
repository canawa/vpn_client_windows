using System.Text.Json;
using System.Text.Json.Nodes;
using CoffeeManiaVPN.Core.Services;

namespace CoffeeManiaVPN.Core.Xray;

public static class XrayTunConfigurator
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    public static string Apply(string configJson, AppSettings? settings = null) =>
        Apply(configJson, settings as ISplitTunnelSettings);

    public static string Apply(string configJson, ISplitTunnelSettings? settings)
    {
        var config = JsonNode.Parse(configJson)?.AsObject()
            ?? throw new InvalidOperationException("Не удалось разобрать конфиг Xray.");

        EnsureLog(config);
        ReplaceInboundsWithTun(config);
        ApplySplitTunnelRules(config, settings);

        return config.ToJsonString(SerializerOptions);
    }

    public static JsonObject CreateTunInbound() => new()
    {
        ["tag"] = "tun",
        ["port"] = 0,
        ["protocol"] = "tun",
        ["settings"] = new JsonObject
        {
            ["name"] = "CoffeeManiaVPN",
            ["mtu"] = 1500,
            ["gateway"] = new JsonArray("172.19.0.1/30"),
            ["dns"] = new JsonArray("1.1.1.1", "1.0.0.1"),
            ["autoSystemRoutingTable"] = new JsonArray("0.0.0.0/0"),
            ["autoOutboundsInterface"] = "auto"
        },
        ["sniffing"] = new JsonObject
        {
            ["enabled"] = true,
            ["routeOnly"] = true,
            ["destOverride"] = new JsonArray("http", "tls", "quic")
        }
    };

    private static void EnsureLog(JsonObject config)
    {
        if (config["log"] is JsonObject)
            return;

        config["log"] = new JsonObject
        {
            ["loglevel"] = "info"
        };
    }

    private static void ReplaceInboundsWithTun(JsonObject config)
    {
        var inbounds = new JsonArray { CreateTunInbound() };

        if (config["inbounds"] is JsonArray existing)
        {
            foreach (var inbound in existing)
            {
                if (inbound is not JsonObject inboundObject)
                    continue;

                var protocol = inboundObject["protocol"]?.GetValue<string>();
                if (protocol is "tun" or "socks" or "http")
                    continue;

                inbounds.Add(inbound.DeepClone());
            }
        }

        config["inbounds"] = inbounds;
    }

    private static void ApplySplitTunnelRules(JsonObject config, ISplitTunnelSettings? settings)
    {
        if (settings is null)
            return;

        if (config["routing"] is not JsonObject routing)
            return;

        if (routing["rules"] is not JsonArray rules)
            return;

        var catchAllIndex = FindCatchAllRuleIndex(rules);
        if (catchAllIndex < 0)
            return;

        var insertIndex = catchAllIndex;
        var inserted = 0;

        if (settings.SiteSplitTunnelEnabled && settings.SiteSplitTunnelDomains.Count > 0)
        {
            var domains = settings.SiteSplitTunnelDomains
                .Select(NormalizeDomain)
                .Where(static domain => !string.IsNullOrWhiteSpace(domain))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(static domain => (JsonNode)domain)
                .ToArray();

            if (domains.Length > 0)
            {
                rules.Insert(insertIndex, CreateFieldRule(
                    domains,
                    settings.SiteSplitTunnelMode == AppSettings.SplitTunnelModeProxyOnly ? "proxy" : "direct"));
                inserted++;
            }
        }

        if (settings.AppSplitTunnelEnabled && settings.AppSplitTunnelApps.Count > 0)
        {
            var processes = settings.AppSplitTunnelApps
                .Select(NormalizeProcessPath)
                .Where(static path => !string.IsNullOrWhiteSpace(path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(static path => (JsonNode)path)
                .ToArray();

            if (processes.Length > 0)
            {
                rules.Insert(insertIndex, CreateFieldRule(
                    processes,
                    settings.AppSplitTunnelMode == AppSettings.SplitTunnelModeProxyOnly ? "proxy" : "direct",
                    isProcess: true));
                inserted++;
            }
        }

        if (inserted == 0)
            return;

        var useDirectCatchAll =
            (settings.SiteSplitTunnelEnabled &&
             settings.SiteSplitTunnelMode == AppSettings.SplitTunnelModeProxyOnly &&
             settings.SiteSplitTunnelDomains.Count > 0) ||
            (settings.AppSplitTunnelEnabled &&
             settings.AppSplitTunnelMode == AppSettings.SplitTunnelModeProxyOnly &&
             settings.AppSplitTunnelApps.Count > 0);

        if (!useDirectCatchAll)
            return;

        var catchAllRule = rules[catchAllIndex + inserted];
        if (catchAllRule is JsonObject catchAllObject)
            catchAllObject["outboundTag"] = "direct";
    }

    private static JsonObject CreateFieldRule(JsonNode[] values, string outboundTag, bool isProcess = false)
    {
        var rule = new JsonObject
        {
            ["type"] = "field",
            ["outboundTag"] = outboundTag
        };

        if (isProcess)
            rule["process"] = new JsonArray(values);
        else
            rule["domain"] = new JsonArray(values);

        return rule;
    }

    private static int FindCatchAllRuleIndex(JsonArray rules)
    {
        for (var index = rules.Count - 1; index >= 0; index--)
        {
            if (rules[index] is not JsonObject rule)
                continue;

            var network = rule["network"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(network))
                continue;

            return index;
        }

        return -1;
    }

    private static string NormalizeDomain(string raw)
    {
        var value = raw.Trim();
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        if (value.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            value.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            if (Uri.TryCreate(value, UriKind.Absolute, out var uri))
                value = uri.Host;
        }

        value = value.TrimStart('*', '.');
        value = value.Split('/', '?', '#')[0];

        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        if (value.Contains(':'))
            return value;

        return $"domain:{value}";
    }

    private static string NormalizeProcessPath(string raw)
    {
        var value = raw.Trim();
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return value.Replace('\\', '/');
    }
}

public interface ISplitTunnelSettings
{
    bool SiteSplitTunnelEnabled { get; }
    string SiteSplitTunnelMode { get; }
    IReadOnlyList<string> SiteSplitTunnelDomains { get; }
    bool AppSplitTunnelEnabled { get; }
    string AppSplitTunnelMode { get; }
    IReadOnlyList<string> AppSplitTunnelApps { get; }
}
