using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using CoffeeManiaVPN.Core.Services;
using Microsoft.Win32;

namespace CoffeeManiaVPN.Views;

public partial class AppSplitTunnelSettingsView : UserControl
{
    private readonly ObservableCollection<string> _apps = new();

    public event EventHandler<bool>? EnabledChanged;
    public event EventHandler<string>? ModeChanged;
    public event EventHandler<IReadOnlyList<string>>? AppsChanged;

    public AppSplitTunnelSettingsView()
    {
        InitializeComponent();
        AppsItemsControl.ItemsSource = _apps;
        UpdateModeButtons(AppSettings.SplitTunnelModeBypass);
        UpdateModeHint(AppSettings.SplitTunnelModeBypass);
    }

    public void Load(bool enabled, string mode, IEnumerable<string> apps)
    {
        var normalizedMode = AppSettings.NormalizeSplitTunnelMode(mode);
        EnabledCheckBox.IsChecked = enabled;
        _apps.Clear();
        foreach (var app in apps)
            _apps.Add(app);

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
        var dialog = new OpenFileDialog
        {
            Title = "Выберите приложение",
            Filter = "Приложения (*.exe)|*.exe",
            CheckFileExists = true,
            Multiselect = true
        };

        if (dialog.ShowDialog() != true)
            return;

        var added = false;
        foreach (var fileName in dialog.FileNames)
        {
            var fullPath = Path.GetFullPath(fileName);
            if (_apps.Any(existing => string.Equals(existing, fullPath, StringComparison.OrdinalIgnoreCase)))
                continue;

            _apps.Add(fullPath);
            added = true;
        }

        if (!added)
        {
            SetStatus("Выбранные приложения уже в списке.");
            return;
        }

        SetStatus(null);
        AppsChanged?.Invoke(this, _apps.ToList());
    }

    private void RemoveButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string app })
            return;

        _apps.Remove(app);
        AppsChanged?.Invoke(this, _apps.ToList());
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
            ? "Через VPN пойдут только выбранные приложения. Остальной трафик — напрямую."
            : "Выбранные приложения обходят VPN. Остальной трафик — через VPN.";
    }

    public void SetStatus(string? message) => StatusTextBlock.Text = message ?? string.Empty;
}
