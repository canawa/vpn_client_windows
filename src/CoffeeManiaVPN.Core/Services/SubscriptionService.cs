using System.Net.Http.Headers;
using System.Text;
using CoffeeManiaVPN.Core.Models;
using CoffeeManiaVPN.Core.Parsers;

namespace CoffeeManiaVPN.Core.Services;

public sealed class SubscriptionService
{
    public const string UserAgent = "v2rayNG/1.0.0";

    private static readonly HttpClient HttpClient = CreateHttpClient();
    private readonly DeviceIdentityService _deviceIdentity;

    public SubscriptionService(DeviceIdentityService deviceIdentity)
    {
        _deviceIdentity = deviceIdentity;
    }

    public async Task<SubscriptionFetchResult> FetchAsync(
        string subscriptionUrl,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(subscriptionUrl))
            throw new ArgumentException("Укажите ссылку подписки.", nameof(subscriptionUrl));

        var url = subscriptionUrl.Trim();
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("http" or "https"))
        {
            throw new ArgumentException("Ссылка подписки должна начинаться с http:// или https://.");
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.UserAgent.ParseAdd(UserAgent);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*"));

        foreach (var header in _deviceIdentity.GetSubscriptionHeaders())
            request.Headers.TryAddWithoutValidation(header.Key, header.Value);

        using var response = await HttpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        ValidateHwidHeaders(response.Headers);

        var body = (await response.Content.ReadAsStringAsync(cancellationToken)).Trim();
        if (string.IsNullOrWhiteSpace(body))
            throw new InvalidOperationException("Подписка вернула пустой ответ.");

        return new SubscriptionFetchResult
        {
            Nodes = ParseSubscriptionBody(body),
            Info = ParseSubscriptionInfo(response.Headers)
        };
    }

    public async Task<IReadOnlyList<ProxyNode>> FetchNodesAsync(
        string subscriptionUrl,
        CancellationToken cancellationToken = default) =>
        (await FetchAsync(subscriptionUrl, cancellationToken)).Nodes;

    public static IReadOnlyList<ProxyNode> ParseSubscriptionBody(string body)
    {
        var trimmed = body.Trim();
        if (trimmed.StartsWith('['))
            return XrayJsonSubscriptionParser.Parse(trimmed);

        var text = TryDecodeBase64(trimmed) ?? trimmed;
        var nodes = new List<ProxyNode>();

        foreach (var line in text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (VlessShareLinkParser.TryParse(line, out var node) && node is not null)
                nodes.Add(node);
        }

        if (nodes.Count == 0)
            throw new InvalidOperationException("В подписке не найдено поддерживаемых серверов.");

        if (nodes.All(static node => node.IsPlaceholder))
        {
            var message = nodes[0].Name;
            throw new InvalidOperationException(
                string.IsNullOrWhiteSpace(message)
                    ? "Подписка недоступна для этого клиента."
                    : message);
        }

        return nodes.Where(static node => !node.IsPlaceholder).ToList();
    }

    private static SubscriptionInfo ParseSubscriptionInfo(System.Net.Http.Headers.HttpResponseHeaders headers)
    {
        if (!headers.TryGetValues("subscription-userinfo", out var values))
            return SubscriptionInfo.Empty;

        var raw = values.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(raw))
            return SubscriptionInfo.Empty;

        long upload = 0;
        long download = 0;
        long total = 0;
        long expire = 0;

        foreach (var part in raw.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var pair = part.Split('=', 2, StringSplitOptions.TrimEntries);
            if (pair.Length != 2 || !long.TryParse(pair[1], out var value))
                continue;

            switch (pair[0].ToLowerInvariant())
            {
                case "upload": upload = value; break;
                case "download": download = value; break;
                case "total": total = value; break;
                case "expire": expire = value; break;
            }
        }

        return new SubscriptionInfo
        {
            Upload = upload,
            Download = download,
            Total = total,
            Expire = expire
        };
    }

    private static void ValidateHwidHeaders(System.Net.Http.Headers.HttpResponseHeaders headers)
    {
        if (IsHeaderTrue(headers, "x-hwid-max-devices-reached"))
        {
            throw new InvalidOperationException(
                "Достигнут лимит устройств для этой подписки. Удалите одно из устройств в личном кабинете.");
        }

        if (IsHeaderTrue(headers, "x-hwid-not-supported"))
        {
            throw new InvalidOperationException(
                "Сервер не получил идентификатор устройства. Перезапустите приложение и попробуйте снова.");
        }
    }

    private static bool IsHeaderTrue(System.Net.Http.Headers.HttpResponseHeaders headers, string name)
    {
        if (!headers.TryGetValues(name, out var values))
            return false;

        return values.Any(static value =>
            string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "1", StringComparison.OrdinalIgnoreCase));
    }

    private static string? TryDecodeBase64(string body)
    {
        var compact = body.Replace("\r", string.Empty).Replace("\n", string.Empty).Trim();
        if (compact.Length == 0 || compact.Contains(' '))
            return null;

        try
        {
            var padding = (4 - compact.Length % 4) % 4;
            var padded = compact + new string('=', padding);
            var bytes = Convert.FromBase64String(padded);
            return Encoding.UTF8.GetString(bytes);
        }
        catch
        {
            return null;
        }
    }

    private static HttpClient CreateHttpClient()
    {
        return new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
    }
}
