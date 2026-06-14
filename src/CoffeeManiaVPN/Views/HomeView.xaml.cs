using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CoffeeManiaVPN.Core.Models;
using CoffeeManiaVPN.Helpers;

namespace CoffeeManiaVPN.Views;

public partial class HomeView : UserControl
{
    public event EventHandler? ConnectToggleRequested;
    public event EventHandler? OpenServersRequested;

    public HomeView()
    {
        InitializeComponent();
        Loaded += (_, _) => UpdateConnectButtonAppearance(false);
    }

    public void SetConnected(bool connected, string? serverName = null)
    {
        StatusTextBlock.Text = connected ? "Подключено" : "Отключено";
        StatusTextBlock.Foreground = connected
            ? (Brush)FindResource("TertiaryBrush")
            : (Brush)FindResource("OnSurfaceBrush");

        TrafficStatsPanel.Visibility = connected ? Visibility.Visible : Visibility.Collapsed;
        UpdateConnectButtonAppearance(connected);
    }

    public void SetTrafficStats(string upload, string download, string duration)
    {
        UploadSpeedTextBlock.Text = upload;
        DownloadSpeedTextBlock.Text = download;
        ConnectionTimerTextBlock.Text = duration;
    }

    private void UpdateConnectButtonAppearance(bool connected)
    {
        ConnectButton.ApplyTemplate();

        if (ConnectButton.Template?.FindName("RingLayer", ConnectButton) is not Border ringLayer ||
            ConnectButton.Template?.FindName("ConnectLogo", ConnectButton) is not Controls.LogoMark logo)
        {
            return;
        }

        if (connected)
        {
            ringLayer.Background = (Brush)FindResource("TertiaryBrush");
            logo.IconBrush = (Brush)FindResource("TertiaryBrush");
            return;
        }

        ringLayer.Background = (Brush)FindResource("ConnectButtonBorderBrush");
        logo.IconBrush = (Brush)FindResource("OnSurfaceBrush");
    }

    public void SetCurrentServer(string serverName, ProxyNode? node = null)
    {
        CurrentServerTextBlock.SourceText = string.IsNullOrWhiteSpace(serverName) ? "Не выбран" : serverName;
        ServerFlag.CountryCode = node is not null
            ? ServerDisplayHelper.GetCountryCode(node)
            : "eu";
    }

    private void ConnectButton_Click(object sender, RoutedEventArgs e) =>
        ConnectToggleRequested?.Invoke(this, EventArgs.Empty);

    private void ServerCard_Click(object sender, System.Windows.Input.MouseButtonEventArgs e) =>
        OpenServersRequested?.Invoke(this, EventArgs.Empty);

    private void PromoBanner_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://coffeemaniavpn.ru",
                UseShellExecute = true
            });
        }
        catch
        {
            // ignored
        }
    }
}
