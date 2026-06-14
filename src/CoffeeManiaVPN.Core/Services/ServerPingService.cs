using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using CoffeeManiaVPN.Core.Models;
using CoffeeManiaVPN.Core.Parsers;

namespace CoffeeManiaVPN.Core.Services;

public sealed class ServerPingService
{
    private const int TimeoutMs = 3000;
    private const int MaxParallel = 6;

    public async Task<IReadOnlyDictionary<ProxyNode, int?>> PingNodesAsync(
        IEnumerable<ProxyNode> nodes,
        IProgress<(ProxyNode Node, int? Ping)>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var uniqueNodes = nodes
            .Where(static node => !node.IsPlaceholder)
            .DistinctBy(ResolveEndpointKey)
            .ToList();

        var results = new Dictionary<ProxyNode, int?>();
        using var gate = new SemaphoreSlim(MaxParallel);

        var tasks = uniqueNodes.Select(async node =>
        {
            await gate.WaitAsync(cancellationToken);
            try
            {
                var ping = await PingAsync(node, cancellationToken);
                lock (results)
                    results[node] = ping;

                progress?.Report((node, ping));
            }
            finally
            {
                gate.Release();
            }
        });

        await Task.WhenAll(tasks);
        return results;
    }

    public static async Task<int?> PingAsync(ProxyNode node, CancellationToken cancellationToken = default)
    {
        if (node.IsPlaceholder)
            return null;

        var endpoint = ResolveEndpoint(node);
        if (endpoint is null || string.IsNullOrWhiteSpace(endpoint.Address))
            return null;

        return IsHysteria(endpoint, node)
            ? await PingIcmpAsync(endpoint.Address, cancellationToken)
            : await PingTcpAsync(endpoint.Address, endpoint.Port, cancellationToken);
    }

    private static XrayJsonSubscriptionParser.EndpointInfo? ResolveEndpoint(ProxyNode node)
    {
        if (!string.IsNullOrWhiteSpace(node.XrayConfigJson))
        {
            var fromConfig = XrayJsonSubscriptionParser.TryExtractEndpoint(node.XrayConfigJson);
            if (fromConfig is not null)
                return fromConfig;
        }

        if (string.IsNullOrWhiteSpace(node.Address))
            return null;

        return new XrayJsonSubscriptionParser.EndpointInfo(node.Protocol, node.Address, node.Port, node.UserId);
    }

    private static string ResolveEndpointKey(ProxyNode node)
    {
        var endpoint = ResolveEndpoint(node);
        return endpoint is null
            ? node.Name
            : $"{endpoint.Protocol}:{endpoint.Address}:{endpoint.Port}";
    }

    private static bool IsHysteria(XrayJsonSubscriptionParser.EndpointInfo endpoint, ProxyNode node) =>
        endpoint.Protocol.Equals("hysteria", StringComparison.OrdinalIgnoreCase) ||
        node.Network.Equals("hysteria", StringComparison.OrdinalIgnoreCase) ||
        node.Name.Contains("HYSTERIA", StringComparison.OrdinalIgnoreCase) ||
        node.Name.Contains("HY2", StringComparison.OrdinalIgnoreCase);

    private static async Task<int?> PingTcpAsync(
        string address,
        int port,
        CancellationToken cancellationToken)
    {
        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeoutMs);

            using var client = new TcpClient();
            var stopwatch = Stopwatch.StartNew();
            await client.ConnectAsync(address, port, timeoutCts.Token);
            stopwatch.Stop();
            return Math.Max(1, (int)stopwatch.ElapsedMilliseconds);
        }
        catch
        {
            return -1;
        }
    }

    private static async Task<int?> PingIcmpAsync(string address, CancellationToken cancellationToken)
    {
        try
        {
            using var ping = new Ping();
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeoutMs);

            var reply = await ping.SendPingAsync(address, TimeoutMs).WaitAsync(timeoutCts.Token);
            if (reply.Status == IPStatus.Success)
                return Math.Max(1, (int)reply.RoundtripTime);

            return -1;
        }
        catch
        {
            return -1;
        }
    }
}
