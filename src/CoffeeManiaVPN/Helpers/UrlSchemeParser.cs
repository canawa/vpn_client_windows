using System.Text;

namespace CoffeeManiaVPN.Helpers;

public enum UrlSchemeAction
{
    Unknown,
    Connect,
    Disconnect,
    Toggle,
    Import,
    Add
}

public sealed class UrlSchemeRequest
{
    public UrlSchemeAction Action { get; init; }
    public string? Payload { get; init; }
}

public static class UrlSchemeParser
{
    public const string Scheme = "cfm";
    private const string SchemePrefix = $"{Scheme}://";

    public static bool TryParse(string? argument, out UrlSchemeRequest request)
    {
        request = new UrlSchemeRequest { Action = UrlSchemeAction.Unknown };

        if (string.IsNullOrWhiteSpace(argument))
            return false;

        var raw = NormalizeRawArgument(argument);
        if (!raw.StartsWith(SchemePrefix, StringComparison.OrdinalIgnoreCase))
            return false;

        var remainder = raw[SchemePrefix.Length..];
        if (string.IsNullOrWhiteSpace(remainder))
            return false;

        var slashIndex = remainder.IndexOf('/');
        var actionName = (slashIndex >= 0 ? remainder[..slashIndex] : remainder).Trim().ToLowerInvariant();
        var payload = slashIndex >= 0 ? remainder[(slashIndex + 1)..] : null;

        if (!string.IsNullOrWhiteSpace(payload))
            payload = Uri.UnescapeDataString(payload.Trim());

        var action = actionName switch
        {
            "connect" or "open" => UrlSchemeAction.Connect,
            "disconnect" or "close" => UrlSchemeAction.Disconnect,
            "toggle" => UrlSchemeAction.Toggle,
            "import" => UrlSchemeAction.Import,
            "add" => UrlSchemeAction.Add,
            _ => UrlSchemeAction.Unknown
        };

        if (action == UrlSchemeAction.Unknown)
            return false;

        request = new UrlSchemeRequest
        {
            Action = action,
            Payload = string.IsNullOrWhiteSpace(payload) ? null : payload
        };
        return true;
    }

    public static string? FindInCommandLine()
    {
        foreach (var arg in Environment.GetCommandLineArgs().Skip(1))
        {
            if (TryParse(arg, out _))
                return NormalizeRawArgument(arg);
        }

        return null;
    }

    public static string? FindInArguments(IEnumerable<string> args)
    {
        foreach (var arg in args)
        {
            if (TryParse(arg, out _))
                return NormalizeRawArgument(arg);
        }

        return null;
    }

    public static bool CanRunWithoutElevation(string? rawUrl)
    {
        if (string.IsNullOrWhiteSpace(rawUrl))
            return false;

        return TryParse(rawUrl, out var request) &&
               request.Action is UrlSchemeAction.Import or UrlSchemeAction.Add;
    }

    public static string? ResolveSubscriptionUrl(string? payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
            return null;

        foreach (var candidate in EnumerateCandidates(payload))
        {
            if (TryAsSubscriptionUrl(candidate, out var url))
                return url;
        }

        return null;
    }

    private static string NormalizeRawArgument(string argument) =>
        argument.Trim().Trim('"');

    private static IEnumerable<string> EnumerateCandidates(string payload)
    {
        var trimmed = Uri.UnescapeDataString(payload.Trim());
        yield return trimmed;

        var decoded = TryDecodeBase64(trimmed);
        if (decoded is null)
            yield break;

        yield return decoded.Trim();

        var decodedAgain = TryDecodeBase64(decoded.Trim());
        if (decodedAgain is not null)
            yield return decodedAgain.Trim();
    }

    private static bool TryAsSubscriptionUrl(string value, out string url)
    {
        url = value.Trim();
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return false;

        if (uri.Scheme is not ("http" or "https"))
            return false;

        return true;
    }

    private static string? TryDecodeBase64(string value)
    {
        var compact = value.Replace("\r", string.Empty).Replace("\n", string.Empty).Trim();
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
}
