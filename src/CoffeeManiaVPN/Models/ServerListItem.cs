using System.ComponentModel;
using System.Runtime.CompilerServices;
using CoffeeManiaVPN.Core.Models;
using CoffeeManiaVPN.Helpers;

namespace CoffeeManiaVPN.Models;

public sealed class ServerListItem : INotifyPropertyChanged
{
    private int? _pingMs;
    private bool _isPinging;

    public required ProxyNode Node { get; init; }

    public string CountryCode => ServerDisplayHelper.GetCountryCode(Node);

    public string Name => ServerDisplayHelper.GetDisplayName(Node);

    public string ProtocolBadge => ServerDisplayHelper.GetProtocolBadge(Node);

    public string ProtocolHint => ServerDisplayHelper.GetProtocolHint(Node);

    public bool IsRecommended => ServerDisplayHelper.IsRecommended(Node);

    public int? PingMs
    {
        get => _pingMs;
        set
        {
            if (_pingMs == value)
                return;

            _pingMs = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(PingText));
            OnPropertyChanged(nameof(HasPingResult));
            OnPropertyChanged(nameof(SignalLevel));
            OnPropertyChanged(nameof(Bar1Active));
            OnPropertyChanged(nameof(Bar2Active));
            OnPropertyChanged(nameof(Bar3Active));
        }
    }

    public bool IsPinging
    {
        get => _isPinging;
        set
        {
            if (_isPinging == value)
                return;

            _isPinging = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(PingText));
        }
    }

    public string PingText => IsPinging
        ? "..."
        : PingMs switch
        {
            null => "—",
            < 0 => "—",
            var ms => $"{ms} ms"
        };

    public bool HasPingResult => PingMs is not null;

    public int SignalLevel => PingMs switch
    {
        null => IsPinging ? 1 : 0,
        < 0 => 0,
        <= 80 => 3,
        <= 180 => 2,
        _ => 1
    };

    public bool Bar1Active => SignalLevel >= 1;
    public bool Bar2Active => SignalLevel >= 2;
    public bool Bar3Active => SignalLevel >= 3;

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
