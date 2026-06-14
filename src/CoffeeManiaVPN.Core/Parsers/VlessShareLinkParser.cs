using CoffeeManiaVPN.Core.Models;

namespace CoffeeManiaVPN.Core.Parsers;

public static class VlessShareLinkParser
{
    public static bool TryParse(string link, out ProxyNode? node)
    {
        node = null;
        if (!link.StartsWith("vless://", StringComparison.OrdinalIgnoreCase))
            return false;

        try
        {
            var uri = new Uri(link);
            if (string.IsNullOrWhiteSpace(uri.UserInfo) || string.IsNullOrWhiteSpace(uri.Host))
                return false;

            var userId = Uri.UnescapeDataString(uri.UserInfo);
            var address = uri.Host;
            var port = uri.Port > 0 ? uri.Port : 443;
            var query = ParseQuery(uri.Query);
            var name = string.IsNullOrEmpty(uri.Fragment)
                ? $"{address}:{port}"
                : Uri.UnescapeDataString(uri.Fragment.TrimStart('#'));

            node = new ProxyNode
            {
                Name = name,
                Protocol = "vless",
                Address = address,
                Port = port,
                UserId = userId,
                Encryption = GetQueryValue(query, "encryption") ?? "none",
                Network = GetQueryValue(query, "type") ?? "tcp",
                Security = GetQueryValue(query, "security") ?? "none",
                Flow = GetQueryValue(query, "flow"),
                Sni = GetQueryValue(query, "sni"),
                Fingerprint = GetQueryValue(query, "fp"),
                PublicKey = GetQueryValue(query, "pbk"),
                ShortId = GetQueryValue(query, "sid"),
                SpiderX = GetQueryValue(query, "spx"),
                Path = GetQueryValue(query, "path"),
                Host = GetQueryValue(query, "host"),
                ServiceName = GetQueryValue(query, "serviceName"),
                Alpn = GetQueryValue(query, "alpn"),
                RawLink = link
            };

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static Dictionary<string, string> ParseQuery(string query)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var trimmed = query.TrimStart('?');
        if (string.IsNullOrEmpty(trimmed))
            return result;

        foreach (var part in trimmed.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = part.IndexOf('=');
            if (separator < 0)
                continue;

            var key = Uri.UnescapeDataString(part[..separator]);
            var value = Uri.UnescapeDataString(part[(separator + 1)..]);
            result[key] = value;
        }

        return result;
    }

    private static string? GetQueryValue(IReadOnlyDictionary<string, string> query, string key) =>
        query.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : null;
}
