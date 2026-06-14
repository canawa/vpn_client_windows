using System.Net.NetworkInformation;

namespace CoffeeManiaVPN.Core.Services;

public sealed class VpnTrafficMonitor
{
    private const string AdapterKeyword = "CoffeeManiaVPN";

    private NetworkInterface? _adapter;
    private long _lastSent;
    private long _lastReceived;
    private DateTime _lastSampleAt;
    private DateTime? _connectedAt;
    private double _uploadBps;
    private double _downloadBps;

    public void Start()
    {
        _connectedAt = DateTime.UtcNow;
        _adapter = FindAdapter();
        _uploadBps = 0;
        _downloadBps = 0;
        _lastSampleAt = DateTime.UtcNow;

        if (_adapter is null)
            return;

        try
        {
            var stats = _adapter.GetIPv4Statistics();
            _lastSent = stats.BytesSent;
            _lastReceived = stats.BytesReceived;
        }
        catch (NetworkInformationException)
        {
            _adapter = null;
        }
    }

    public void Stop()
    {
        _connectedAt = null;
        _adapter = null;
        _uploadBps = 0;
        _downloadBps = 0;
    }

    public TrafficSnapshot Sample()
    {
        if (_connectedAt is null)
            return TrafficSnapshot.Empty;

        var now = DateTime.UtcNow;
        var adapter = _adapter ??= FindAdapter();

        if (adapter is not null)
        {
            try
            {
                var stats = adapter.GetIPv4Statistics();
                var elapsed = (now - _lastSampleAt).TotalSeconds;

                if (elapsed >= 0.5)
                {
                    _uploadBps = Math.Max(0, (stats.BytesSent - _lastSent) / elapsed);
                    _downloadBps = Math.Max(0, (stats.BytesReceived - _lastReceived) / elapsed);
                    _lastSent = stats.BytesSent;
                    _lastReceived = stats.BytesReceived;
                    _lastSampleAt = now;
                }
            }
            catch (NetworkInformationException)
            {
                _adapter = null;
            }
        }

        var duration = now - _connectedAt.Value;
        return new TrafficSnapshot(
            FormatSpeed(_uploadBps),
            FormatSpeed(_downloadBps),
            FormatDuration(duration));
    }

    private static NetworkInterface? FindAdapter() =>
        NetworkInterface.GetAllNetworkInterfaces()
            .FirstOrDefault(ni =>
                ni.OperationalStatus == OperationalStatus.Up &&
                (ni.Description.Contains(AdapterKeyword, StringComparison.OrdinalIgnoreCase) ||
                 ni.Name.Contains(AdapterKeyword, StringComparison.OrdinalIgnoreCase)));

    private static string FormatSpeed(double bytesPerSecond) =>
        bytesPerSecond >= 1024 * 1024
            ? $"{bytesPerSecond / (1024 * 1024):0.##} MB/s"
            : $"{bytesPerSecond / 1024:0.##} KB/s";

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalHours >= 1)
            return $"{(int)duration.TotalHours}:{duration.Minutes:D2}:{duration.Seconds:D2}";

        return $"{duration.Minutes:D2}:{duration.Seconds:D2}";
    }
}

public readonly record struct TrafficSnapshot(string Upload, string Download, string Duration)
{
    public static TrafficSnapshot Empty { get; } = new("0 KB/s", "0 KB/s", "00:00");
}
