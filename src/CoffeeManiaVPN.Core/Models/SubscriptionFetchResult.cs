namespace CoffeeManiaVPN.Core.Models;

public sealed class SubscriptionFetchResult
{
    public required IReadOnlyList<ProxyNode> Nodes { get; init; }
    public SubscriptionInfo Info { get; init; } = SubscriptionInfo.Empty;
}
