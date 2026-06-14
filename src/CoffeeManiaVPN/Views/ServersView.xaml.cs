using System.Collections;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using CoffeeManiaVPN.Core.Models;

namespace CoffeeManiaVPN.Views;

public partial class ServersView : UserControl
{
    public event EventHandler? RefreshRequested;
    public event EventHandler? PingRequested;
    public event EventHandler<ProxyNode>? ServerSelected;

    private double _usageRatio;
    private ScrollViewer? _serversScrollViewer;

    public ServersView()
    {
        InitializeComponent();
        Loaded += (_, _) => _serversScrollViewer = FindScrollViewer(ServersListBox);
        PreviewMouseWheel += OnPreviewMouseWheel;
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

        _serversScrollViewer.ScrollToVerticalOffset(
            _serversScrollViewer.VerticalOffset - e.Delta);

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
