using System.Text.Json;
using System.Text.Json.Nodes;

namespace CoffeeManiaVPN.Core.Xray;

public static class XrayTunConfigurator
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    public static string Apply(string configJson)
    {
        var config = JsonNode.Parse(configJson)?.AsObject()
            ?? throw new InvalidOperationException("Не удалось разобрать конфиг Xray.");

        EnsureLog(config);
        ReplaceInboundsWithTun(config);

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
        if (config.ContainsKey("log"))
            return;

        config["log"] = new JsonObject
        {
            ["loglevel"] = "warning"
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
}
