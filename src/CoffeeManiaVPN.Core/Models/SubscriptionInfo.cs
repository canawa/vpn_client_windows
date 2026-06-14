namespace CoffeeManiaVPN.Core.Models;

public sealed class SubscriptionInfo
{
    public long Upload { get; init; }
    public long Download { get; init; }
    public long Total { get; init; }
    public long Expire { get; init; }

    public static SubscriptionInfo Empty { get; } = new();

    public bool HasQuota => Total > 0;

    public bool IsLifetime => Expire <= 0;

    public string SubscriptionLabel => IsLifetime ? "Бессрочная подписка" : "Активная подписка";

    public string UsageText => HasQuota
        ? $"{FormatBytes(Download)} / {FormatBytes(Total)}"
        : $"{FormatBytes(Download)} использовано";

    public double UsageRatio => HasQuota
        ? Math.Clamp(Download / (double)Total, 0, 1)
        : 0;

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double value = bytes;
        var unit = 0;

        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return unit switch
        {
            0 => $"{value:0} {units[unit]}",
            1 => $"{value:0.#} {units[unit]}",
            _ => $"{value:0.#} {units[unit]}"
        };
    }
}
