using CoffeeManiaVPN.Core.Models;

namespace CoffeeManiaVPN.Helpers;

public static class ServerDisplayHelper
{
    private static readonly (string[] Keys, string Code, string Label)[] CountryMap =
    [
        (["АВТО", "AUTO", "BEST", "РЕГИОН"], "eu", "EU"),
        (["ЛАТВ", "LATV", "LATVIJA"], "lv", "LV"),
        (["ПОЛЬ", "POLAND", "POLSK", "WARSZ"], "pl", "PL"),
        (["ГЕРМ", "GERMAN", "DEUTSCH", "АНТИЗАГЛУШ"], "de", "DE"),
        (["ФИНЛ", "FINLAND", "SUOMI", "ИГРОВ"], "fi", "FI"),
        (["НИДЕР", "NETHER", "HOLLAND", "AMSTER"], "nl", "NL"),
        (["ЭСТОН", "ESTON", "TALLINN"], "ee", "EE"),
        (["ЛИТВ", "LITHU", "VILNI"], "lt", "LT"),
        (["ШВЕЦ", "SWEDEN", "STOCK"], "se", "SE"),
        (["НОРВ", "NORWA", "OSLO"], "no", "NO"),
        (["ФРАН", "FRANC", "PARIS"], "fr", "FR"),
        (["АНГЛ", "UK", "LONDON", "BRIT"], "gb", "GB"),
        (["ЧЕХ", "CZECH", "PRAGUE"], "cz", "CZ"),
        (["АВСТРИ", "AUSTRIA", "VIENNA"], "at", "AT"),
        (["ШВЕЙЦ", "SWISS", "ZURICH"], "ch", "CH"),
        (["УКРА", "UKRAI", "KYIV", "KIEV"], "ua", "UA"),
        (["ТУРЦ", "TURK", "ISTANB"], "tr", "TR"),
        (["КАЗАХ", "KAZAK", "ALMATY"], "kz", "KZ"),
        (["РОСС", "RUSS", "MOSCOW", "МОСКВ"], "ru", "RU"),
        (["США", "USA", "UNITED STATES"], "us", "US"),
        (["СИНГАП", "SINGAP"], "sg", "SG"),
        (["ЯПОН", "JAPAN", "TOKYO"], "jp", "JP"),
        (["ОАЭ", "EMIR", "DUBAI"], "ae", "AE"),
        (["КАНАД", "CANAD", "TORONT"], "ca", "CA"),
        (["АВСТРАЛ", "AUSTRAL", "SYDNEY"], "au", "AU")
    ];

    public static string GetCountryCode(ProxyNode node)
    {
        var name = node.Name.ToUpperInvariant();
        foreach (var (keys, code, _) in CountryMap)
        {
            if (keys.Any(key => name.Contains(key, StringComparison.Ordinal)))
                return code;
        }

        return "un";
    }

    public static string GetCountryLabel(ProxyNode node)
    {
        var name = node.Name.ToUpperInvariant();
        foreach (var (keys, _, label) in CountryMap)
        {
            if (keys.Any(key => name.Contains(key, StringComparison.Ordinal)))
                return label;
        }

        return "VPN";
    }

    public static string GetFlagEmoji(ProxyNode node) => GetCountryLabel(node);

    public static string GetDisplayName(ProxyNode node)
    {
        var parts = node.Name.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length <= 1)
            return node.Name.Trim();

        var displayParts = parts.Skip(1).ToArray();
        return displayParts.Length == 0 ? node.Name.Trim() : string.Join(' ', displayParts);
    }

    public static string GetShortName(ProxyNode node)
    {
        var name = GetDisplayName(node);
        if (name.Length <= 28)
            return name;

        return name[..25] + "...";
    }

    public static string GetProtocolBadge(ProxyNode node)
    {
        var name = node.Name.ToUpperInvariant();
        if (name.Contains("HYSTERIA") || name.Contains("HY2"))
            return "HY2";
        if (name.Contains("REALITY") || node.Security.Equals("reality", StringComparison.OrdinalIgnoreCase))
            return "VLESS";
        if (name.Contains("STEALTH") || name.Contains("АНТИ"))
            return "STEALTH";
        if (node.Protocol.Equals("vless", StringComparison.OrdinalIgnoreCase))
            return "VLESS";

        return node.Protocol.ToUpperInvariant();
    }

    public static string GetProtocolHint(ProxyNode node)
    {
        var badge = GetProtocolBadge(node);
        return badge switch
        {
            "HY2" => "Hysteria",
            "STEALTH" => "High Priority",
            "VLESS" when node.Name.Contains("АВТО", StringComparison.OrdinalIgnoreCase) => "Рекомендуется",
            _ => node.Network.ToUpperInvariant()
        };
    }

    public static bool IsRecommended(ProxyNode node) =>
        node.Name.Contains("АВТО", StringComparison.OrdinalIgnoreCase) ||
        node.Name.Contains("AUTO", StringComparison.OrdinalIgnoreCase);

    public static int GetSignalLevel(ProxyNode node) =>
        IsRecommended(node) ? 3 : 2;
}
