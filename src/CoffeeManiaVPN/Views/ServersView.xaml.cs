using System.Collections;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using CoffeeManiaVPN.Core.Models;
using CoffeeManiaVPN.Helpers;

namespace CoffeeManiaVPN.Views;

public partial class ServersView : UserControl
{
    public event EventHandler? RefreshRequested;
    public event EventHandler? PingRequested;
    public event EventHandler<ProxyNode>? ServerSelected;
    public event EventHandler<ProxyNode>? ServerReconnectRequested;

    private double _usageRatio;
    private ScrollViewer? _serversScrollViewer;

    public ServersView()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            _serversScrollViewer = FindScrollViewer(ServersListBox);
            UpdateCompactLayout(RootGrid.ActualHeight);
        };
        PreviewMouseWheel += OnPreviewMouseWheel;
    }

    private void RootGrid_SizeChanged(object sender, SizeChangedEventArgs e) =>
        UpdateCompactLayout(e.NewSize.Height);

    private void UpdateCompactLayout(double height)
    {
        var compact = height < 560;
        var ultraCompact = height < 460;

        TelegramBanner.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
        BannerRow.Height = compact ? new GridLength(0) : GridLength.Auto;

        UsageSection.Visibility = ultraCompact ? Visibility.Collapsed : Visibility.Visible;
        ServerCountPanel.Visibility = ultraCompact ? Visibility.Collapsed : Visibility.Visible;

        HeaderCard.Padding = compact ? new Thickness(10) : new Thickness(14);
        HeaderCard.Margin = new Thickness(0, 0, 0, compact ? 6 : 10);
    }

    public void SetServers(IReadOnlyList<ProxyNode> nodes, int selectedIndex)
    {
        var items = nodes.Select(static node => new Models.ServerListItem { Node = node }).ToList();
        ServersListBox.ItemsSource = items;
        ServerCountTextBlock.Text = items.Count.ToString();

        if (selectedIndex >= 0 && selectedIndex < items.Count)
            ServersListBox.SelectedIndex = selectedIndex;
        else if (items.Count > 0)
            ServersListBox.SelectedIndex = 0;
    }

    public IReadOnlyList<Models.ServerListItem> GetServerItems()
    {
        if (ServersListBox.ItemsSource is not IEnumerable items)
            return Array.Empty<Models.ServerListItem>();

        return items.Cast<Models.ServerListItem>().ToList();
    }

    public void SetSubscriptionInfo(SubscriptionInfo info)
    {
        SubscriptionLabelTextBlock.Text = info.SubscriptionLabel;
        UsageTextBlock.Text = info.HasQuota ? info.UsageText : "Трафик: " + info.UsageText;
        _usageRatio = info.UsageRatio;
        UpdateUsageBar();
    }

    public void SetRefreshing(bool isRefreshing)
    {
        RefreshButton.IsEnabled = !isRefreshing;
        PingButton.IsEnabled = !isRefreshing;
        RefreshButton.Opacity = isRefreshing ? 0.5 : 1;
    }

    public void SetPinging(bool isPinging)
    {
        PingButton.IsEnabled = !isPinging;
        RefreshButton.IsEnabled = !isPinging;
        PingButton.Opacity = isPinging ? 0.5 : 1;

        foreach (var item in GetServerItems())
        {
            item.IsPinging = isPinging;
            if (isPinging)
                item.PingMs = null;
        }
    }

    public void SetHeaderStatus(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            HeaderStatusTextBlock.Text = string.Empty;
            HeaderStatusTextBlock.Visibility = Visibility.Collapsed;
            return;
        }

        HeaderStatusTextBlock.Text = message;
        HeaderStatusTextBlock.Visibility = Visibility.Visible;
    }

    public void ApplyPingResult(ProxyNode node, int? ping)
    {
        foreach (var item in GetServerItems())
        {
            if (!IsSameEndpoint(item.Node, node))
                continue;

            item.PingMs = ping;
            item.IsPinging = false;
        }
    }

    public void ApplyPingResults(IReadOnlyDictionary<ProxyNode, int?> results)
    {
        foreach (var pair in results)
            ApplyPingResult(pair.Key, pair.Value);
    }

    private static bool IsSameEndpoint(ProxyNode left, ProxyNode right) =>
        ReferenceEquals(left, right) ||
        (left.Address == right.Address && left.Port == right.Port);

    private void UsageBarGrid_SizeChanged(object sender, SizeChangedEventArgs e) => UpdateUsageBar();

    private void UpdateUsageBar()
    {
        var width = Math.Max(0, UsageBarGrid.ActualWidth * _usageRatio);
        UsageProgressBar.Width = width;
        UsageProgressDot.Margin = new Thickness(Math.Max(0, width - 5), 0, 0, 0);
    }

    public ProxyNode? GetSelectedNode() =>
        (ServersListBox.SelectedItem as Models.ServerListItem)?.Node;

    private void RefreshButton_Click(object sender, RoutedEventArgs e) =>
        RefreshRequested?.Invoke(this, EventArgs.Empty);

    private void PingButton_Click(object sender, RoutedEventArgs e) =>
        PingRequested?.Invoke(this, EventArgs.Empty);

    private void ServersListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ServersListBox.SelectedItem is Models.ServerListItem item)
            ServerSelected?.Invoke(this, item.Node);
    }

    private void ServersListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ServersListBox.SelectedItem is not Models.ServerListItem item || item.Node.IsPlaceholder)
            return;

        ServerReconnectRequested?.Invoke(this, item.Node);
        e.Handled = true;
    }

    private void TelegramBanner_Click(object sender, MouseButtonEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://t.me/coffemaniaVPNbot",
                UseShellExecute = true
            });
        }
        catch
        {
            // ignored
        }
    }

    private void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        _serversScrollViewer ??= FindScrollViewer(ServersListBox);
        if (_serversScrollViewer is null)
            return;

        SmoothScrollHelper.ScrollBy(_serversScrollViewer, -e.Delta * 0.28);

        e.Handled = true;
    }

    private static ScrollViewer? FindScrollViewer(DependencyObject parent)
    {
        if (parent is ScrollViewer scrollViewer)
            return scrollViewer;

        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            var found = FindScrollViewer(child);
            if (found is not null)
                return found;
        }

        return null;
    }
}
