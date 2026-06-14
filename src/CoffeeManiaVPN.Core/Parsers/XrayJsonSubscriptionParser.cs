using System.Text.Json;
using CoffeeManiaVPN.Core.Models;

namespace CoffeeManiaVPN.Core.Parsers;

public static class XrayJsonSubscriptionParser
{
    public static IReadOnlyList<ProxyNode> Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException("Неверный формат JSON-подписки.");

        var nodes = new List<ProxyNode>();
        foreach (var item in document.RootElement.EnumerateArray())
        {
            var configJson = item.GetRawText();
            var name = item.TryGetProperty("remarks", out var remarksElement)
                ? remarksElement.GetString()
                : null;
            name = string.IsNullOrWhiteSpace(name) ? $"Сервер {nodes.Count + 1}" : name;

            var endpoint = TryExtractEndpoint(item);
            nodes.Add(new ProxyNode
            {
                Name = name,
                Protocol = endpoint?.Protocol ?? "vless",
                Address = endpoint?.Address ?? name,
                Port = endpoint?.Port ?? 443,
                UserId = endpoint?.UserId ?? string.Empty,
                Network = endpoint?.Protocol == "hysteria" ? "hysteria" : "tcp",
                XrayConfigJson = configJson
            });
        }

        if (nodes.Count == 0)
            throw new InvalidOperationException("В JSON-подписке не найдено серверов.");

        return nodes;
    }

    public static EndpointInfo? TryExtractEndpoint(string configJson)
    {
        try
        {
            using var document = JsonDocument.Parse(configJson);
            return TryExtractEndpoint(document.RootElement);
        }
        catch
        {
            return null;
        }
    }

    public static EndpointInfo? TryExtractEndpoint(JsonElement config)
    {
        if (!config.TryGetProperty("outbounds", out var outbounds) ||
            outbounds.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var outbound in outbounds.EnumerateArray())
        {
            if (!outbound.TryGetProperty("protocol", out var protocolElement))
                continue;

            var protocol = protocolElement.GetString();
            if (string.IsNullOrWhiteSpace(protocol))
                continue;

            if (!outbound.TryGetProperty("settings", out var settings))
                continue;

            if (protocol.Equals("hysteria", StringComparison.OrdinalIgnoreCase))
            {
                var address = settings.TryGetProperty("address", out var addressElement)
                    ? addressElement.GetString()
                    : null;
                var port = settings.TryGetProperty("port", out var portElement) && portElement.TryGetInt32(out var hysteriaPort)
                    ? hysteriaPort
                    : 443;

                if (!string.IsNullOrWhiteSpace(address))
                    return new EndpointInfo("hysteria", address, port, string.Empty);

                continue;
            }

            if (protocol is not ("vless" or "vmess" or "trojan" or "shadowsocks"))
                continue;

            if (settings.TryGetProperty("vnext", out var vnext) &&
                vnext.ValueKind == JsonValueKind.Array &&
                vnext.GetArrayLength() > 0)
            {
                var server = vnext[0];
                var address = server.TryGetProperty("address", out var addressElement)
                    ? addressElement.GetString()
                    : null;
                var port = server.TryGetProperty("port", out var portElement) && portElement.TryGetInt32(out var parsedPort)
                    ? parsedPort
                    : 443;
                var userId = string.Empty;

                if (server.TryGetProperty("users", out var users) &&
                    users.ValueKind == JsonValueKind.Array &&
                    users.GetArrayLength() > 0 &&
                    users[0].TryGetProperty("id", out var idElement))
                {
                    userId = idElement.GetString() ?? string.Empty;
                }

                if (!string.IsNullOrWhiteSpace(address))
                    return new EndpointInfo(protocol, address, port, userId);
            }
        }

        return null;
    }

    public sealed record EndpointInfo(string Protocol, string Address, int Port, string UserId);
}
