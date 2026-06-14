using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using CoffeeManiaVPN.Core.Services;

namespace CoffeeManiaVPN.Views;

public partial class SiteSplitTunnelSettingsView : UserControl
{
    private readonly ObservableCollection<string> _domains = new();

    public event EventHandler<bool>? EnabledChanged;
    public event EventHandler<string>? ModeChanged;
    public event EventHandler<IReadOnlyList<string>>? DomainsChanged;

    public SiteSplitTunnelSettingsView()
    {
        InitializeComponent();
        DomainsItemsControl.ItemsSource = _domains;
        UpdateModeButtons(AppSettings.SplitTunnelModeBypass);
        UpdateModeHint(AppSettings.SplitTunnelModeBypass);
    }

    public void Load(bool enabled, string mode, IEnumerable<string> domains)
    {
        var normalizedMode = AppSettings.NormalizeSplitTunnelMode(mode);
        EnabledCheckBox.IsChecked = enabled;
        _domains.Clear();
        foreach (var domain in domains)
            _domains.Add(domain);

        UpdateModeButtons(normalizedMode);
        UpdateModeHint(normalizedMode);
    }

    private void EnabledCheckBox_Changed(object sender, RoutedEventArgs e) =>
        EnabledChanged?.Invoke(this, EnabledCheckBox.IsChecked == true);

    private void ModeButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string mode)
            return;

        var normalizedMode = AppSettings.NormalizeSplitTunnelMode(mode);
        UpdateModeButtons(normalizedMode);
        UpdateModeHint(normalizedMode);
        ModeChanged?.Invoke(this, normalizedMode);
    }

    private void AddButton_Click(object sender, RoutedEventArgs e)
    {
        var domain = DomainTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(domain))
        {
            SetStatus("Введите домен, например youtube.com");
            return;
        }

        if (_domains.Any(existing => string.Equals(existing, domain, StringComparison.OrdinalIgnoreCase)))
        {
            SetStatus("Этот домен уже в списке.");
            return;
        }

        _domains.Add(domain);
        DomainTextBox.Clear();
        SetStatus(null);
        DomainsChanged?.Invoke(this, _domains.ToList());
    }

    private void RemoveButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string domain })
            return;

        _domains.Remove(domain);
        DomainsChanged?.Invoke(this, _domains.ToList());
    }

    private void UpdateModeButtons(string mode)
    {
        var isBypass = mode == AppSettings.SplitTunnelModeBypass;
        BypassModeButton.Style = (Style)FindResource(isBypass ? "SegmentButtonActiveStyle" : "SegmentButtonStyle");
        ProxyOnlyModeButton.Style = (Style)FindResource(isBypass ? "SegmentButtonStyle" : "SegmentButtonActiveStyle");
    }

    private void UpdateModeHint(string mode)
    {
        ModeHintTextBlock.Text = mode == AppSettings.SplitTunnelModeProxyOnly
            ? "Через VPN пойдут только перечисленные домены. Остальной трафик — напрямую."
            : "Перечисленные домены обходят VPN и идут напрямую. Остальной трафик — через VPN.";
    }

    public void SetStatus(string? message) => StatusTextBlock.Text = message ?? string.Empty;
}
