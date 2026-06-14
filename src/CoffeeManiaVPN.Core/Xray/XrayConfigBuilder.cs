using System.Text.Json;
using System.Text.Json.Nodes;
using CoffeeManiaVPN.Core.Models;

namespace CoffeeManiaVPN.Core.Xray;

public static class XrayConfigBuilder
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    public static string Build(ProxyNode node, string xrayDirectory)
    {
        var config = new JsonObject
        {
            ["log"] = new JsonObject
            {
                ["loglevel"] = "warning"
            },
            ["dns"] = new JsonObject
            {
                ["servers"] = new JsonArray("1.1.1.1", "8.8.8.8")
            },
            ["outbounds"] = new JsonArray
            {
                CreateProxyOutbound(node),
                new JsonObject
                {
                    ["tag"] = "direct",
                    ["protocol"] = "freedom"
                },
                new JsonObject
                {
                    ["tag"] = "block",
                    ["protocol"] = "blackhole"
                }
            },
            ["routing"] = new JsonObject
            {
                ["domainStrategy"] = "IPIfNonMatch",
                ["rules"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["type"] = "field",
                        ["ip"] = new JsonArray("geoip:private"),
                        ["outboundTag"] = "direct"
                    },
                    new JsonObject
                    {
                        ["type"] = "field",
                        ["network"] = "tcp,udp",
                        ["outboundTag"] = "proxy"
                    }
                }
            }
        };

        return config.ToJsonString(SerializerOptions);
    }

    private static JsonObject CreateProxyOutbound(ProxyNode node)
    {
        var user = new JsonObject
        {
            ["id"] = node.UserId,
            ["encryption"] = node.Encryption
        };

        if (!string.IsNullOrWhiteSpace(node.Flow))
            user["flow"] = node.Flow;

        var outbound = new JsonObject
        {
            ["tag"] = "proxy",
            ["protocol"] = "vless",
            ["settings"] = new JsonObject
            {
                ["vnext"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["address"] = node.Address,
                        ["port"] = node.Port,
                        ["users"] = new JsonArray { user }
                    }
                }
            },
            ["streamSettings"] = BuildStreamSettings(node)
        };

        return outbound;
    }

    private static JsonObject BuildStreamSettings(ProxyNode node)
    {
        var stream = new JsonObject
        {
            ["network"] = node.Network,
            ["security"] = node.Security
        };

        switch (node.Security.ToLowerInvariant())
        {
            case "tls":
                stream["tlsSettings"] = BuildTlsSettings(node);
                break;
            case "reality":
                stream["realitySettings"] = BuildRealitySettings(node);
                break;
        }

        switch (node.Network.ToLowerInvariant())
        {
            case "ws":
                stream["wsSettings"] = new JsonObject
                {
                    ["path"] = node.Path ?? "/",
                    ["headers"] = string.IsNullOrWhiteSpace(node.Host)
                        ? new JsonObject()
                        : new JsonObject { ["Host"] = node.Host }
                };
                break;
            case "grpc":
                stream["grpcSettings"] = new JsonObject
                {
                    ["serviceName"] = node.ServiceName ?? string.Empty
                };
                break;
            case "tcp":
                stream["tcpSettings"] = new JsonObject();
                break;
        }

        return stream;
    }

    private static JsonObject BuildTlsSettings(ProxyNode node)
    {
        var tls = new JsonObject
        {
            ["allowInsecure"] = false,
            ["serverName"] = node.Sni ?? node.Host ?? node.Address
        };

        if (!string.IsNullOrWhiteSpace(node.Fingerprint))
            tls["fingerprint"] = node.Fingerprint;

        if (!string.IsNullOrWhiteSpace(node.Alpn))
        {
            var alpn = new JsonArray();
            foreach (var value in node.Alpn.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                alpn.Add(value);
            tls["alpn"] = alpn;
        }

        return tls;
    }

    private static JsonObject BuildRealitySettings(ProxyNode node)
    {
        var reality = new JsonObject
        {
            ["serverName"] = node.Sni ?? node.Host ?? node.Address,
            ["publicKey"] = node.PublicKey ?? string.Empty,
            ["shortId"] = node.ShortId ?? string.Empty
        };

        if (!string.IsNullOrWhiteSpace(node.Fingerprint))
            reality["fingerprint"] = node.Fingerprint;

        if (!string.IsNullOrWhiteSpace(node.SpiderX))
            reality["spiderX"] = node.SpiderX;

        return reality;
    }
}
