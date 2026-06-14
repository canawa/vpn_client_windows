namespace CoffeeManiaVPN.Core.Models;

public sealed class ProxyNode
{
    public required string Name { get; init; }
    public required string Protocol { get; init; }
    public required string Address { get; init; }
    public required int Port { get; init; }
    public required string UserId { get; init; }
    public string Encryption { get; init; } = "none";
    public string Network { get; init; } = "tcp";
    public string Security { get; init; } = "none";
    public string? Flow { get; init; }
    public string? Sni { get; init; }
    public string? Fingerprint { get; init; }
    public string? PublicKey { get; init; }
    public string? ShortId { get; init; }
    public string? SpiderX { get; init; }
    public string? Path { get; init; }
    public string? Host { get; init; }
    public string? ServiceName { get; init; }
    public string? Alpn { get; init; }
    public string RawLink { get; init; } = string.Empty;
    public string? XrayConfigJson { get; init; }

    public bool HasFullConfig => !string.IsNullOrWhiteSpace(XrayConfigJson);

    public bool IsPlaceholder =>
        !HasFullConfig &&
        (Address is "0.0.0.0" or "127.0.0.1" ||
         UserId == "00000000-0000-0000-0000-000000000000");

    public string DisplayAddress => $"{Address}:{Port}";
}
