using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using CoffeeManiaVPN.Core.Models;
using CoffeeManiaVPN.Helpers;

namespace CoffeeManiaVPN.Views;

public partial class HomeView : UserControl
{
    private static readonly TimeSpan FadeDuration = TimeSpan.FromMilliseconds(280);
    private static readonly TimeSpan RingFadeOutDuration = TimeSpan.FromMilliseconds(450);
    private static readonly TimeSpan StatusFadeDuration = TimeSpan.FromMilliseconds(160);
    private static readonly IEasingFunction SmoothEase = new CubicEase { EasingMode = EasingMode.EaseInOut };
    private static readonly IEasingFunction SmoothEaseOut = new CubicEase { EasingMode = EasingMode.EaseOut };
    private static readonly IEasingFunction FadeOutEase = new CubicEase { EasingMode = EasingMode.EaseIn };

    private bool _isConnected;
    private bool _isConnecting;
    private bool _isDisconnectingAnimation;
    private bool _isSubscriptionImporting;
    private bool _isSubscriptionAvailable;
    private bool _isSubscriptionConfigured;
    private Storyboard? _connectingSpinStoryboard;
    private Storyboard? _connectingPulseStoryboard;

    public event EventHandler? ConnectToggleRequested;
    public event EventHandler? OpenServersRequested;
    public event EventHandler? PasteSubscriptionRequested;

    public HomeView()
    {
        InitializeComponent();
        Loaded += (_, _) => UpdateConnectButtonAppearance();
        ConnectButton.MouseEnter += (_, _) => OnConnectButtonMouseEnter();
        ConnectButton.MouseLeave += (_, _) => UpdateConnectButtonAppearance();
    }

    private void RootGrid_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        var height = e.NewSize.Height;
        var compact = height < 540;
        var veryCompact = height < 460;

        PromoBanner.Visibility = height < 500 ? Visibility.Collapsed : Visibility.Visible;
        ServerCardBorder.Visibility = veryCompact ? Visibility.Collapsed : Visibility.Visible;

        StatusHeaderPanel.Margin = new Thickness(0, 0, 0, compact ? 16 : 28);
        StatusTextBlock.FontSize = veryCompact ? 26 : compact ? 30 : 36;

        var scale = Math.Clamp(height / 620.0, 0.58, 1.0);
        ConnectAreaViewbox.MaxWidth = 220 * scale;
        ConnectAreaViewbox.MaxHeight = 220 * scale;
        ConnectAreaViewbox.Margin = new Thickness(0, 0, 0, compact ? 12 : 24);

        BuySubscriptionButton.Margin = new Thickness(0, 0, 0, compact ? 8 : 10);
        PasteSubscriptionButton.Margin = new Thickness(0, 0, 0, compact ? 12 : 24);

        if (HomeScrollViewer.VerticalOffset > 0 && height > e.PreviousSize.Height)
            HomeScrollViewer.ScrollToVerticalOffset(0);
    }

    public void SetConnected(bool connected, string? serverName = null)
    {
        _isConnected = connected;
        SetConnecting(false);

        if (connected)
        {
            AnimateStatusText("Подключено", (Brush)FindResource("OnSurfaceBrush"));
            AnimateTrafficStatsVisible(true);
        }
        else
        {
            AnimateStatusText("Отключено", (Brush)FindResource("OnSurfaceBrush"));
            AnimateTrafficStatsVisible(false);
            SetTrafficStats("0 KB/s", "0 KB/s", "00:00");
        }

        UpdateConnectInteractivity();
        UpdateConnectButtonAppearance();
    }

    public void SetConnecting(bool connecting, string statusText = "Подключение...", bool isDisconnecting = false)
    {
        if (connecting)
            _isSubscriptionImporting = false;

        _isConnecting = connecting;
        _isDisconnectingAnimation = connecting && isDisconnecting;

        if (connecting)
        {
            var accentBrush = GetAnimationAccentBrush();
            AnimateStatusText(statusText, accentBrush);
            ConnectingRingOuter.Stroke = accentBrush;
            ConnectingRingInner.Stroke = accentBrush;
            AnimateConnectingRingVisible(true);
            ConnectButton.IsEnabled = false;
            ConnectButton.IsHitTestVisible = false;
            ConnectButton.Cursor = Cursors.Arrow;
            StartConnectingAnimations();
            UpdateConnectButtonAppearance();
            return;
        }

        _isDisconnectingAnimation = false;
        AnimateConnectingRingVisible(false);
        ConnectButton.IsHitTestVisible = true;
        UpdateConnectInteractivity();
        UpdateConnectButtonAppearance();

        if (_isConnected)
        {
            AnimateStatusText("Подключено", (Brush)FindResource("OnSurfaceBrush"));
            return;
        }

        AnimateStatusText("Отключено", (Brush)FindResource("OnSurfaceBrush"));
    }

    public void SetSubscriptionImporting(bool importing, string statusText = "Загрузка подписки...")
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(() => SetSubscriptionImporting(importing, statusText));
            return;
        }

        _isSubscriptionImporting = importing;

        if (importing)
        {
            _isConnecting = true;
            _isDisconnectingAnimation = false;

            var accentBrush = (Brush)FindResource("PrimaryBrush");
            ResetStatusTextImmediate(statusText, accentBrush);
            ConnectingRingOuter.Stroke = accentBrush;
            ConnectingRingInner.Stroke = accentBrush;
            ConnectingRingFadeHost.BeginAnimation(UIElement.OpacityProperty, null);
            StopConnectingAnimations();
            ConnectingRingFadeHost.Visibility = Visibility.Visible;
            ConnectingRingFadeHost.Opacity = 1;
            ConnectButton.IsEnabled = false;
            ConnectButton.IsHitTestVisible = false;
            ConnectButton.Cursor = Cursors.Arrow;
            StartConnectingAnimations();
            UpdateConnectButtonAppearance();
            return;
        }

        _isConnecting = false;
        StopConnectingAnimations();
        AnimateConnectingRingVisible(false);
        ConnectButton.IsHitTestVisible = true;
        UpdateConnectInteractivity();
        UpdateConnectButtonAppearance();
        ResetStatusTextImmediate(
            _isConnected ? "Подключено" : "Отключено",
            (Brush)FindResource("OnSurfaceBrush"));
    }

    public void UpdateSubscriptionImportStatus(string statusText)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(() => UpdateSubscriptionImportStatus(statusText));
            return;
        }

        if (!_isSubscriptionImporting)
            return;

        ResetStatusTextImmediate(statusText, (Brush)FindResource("PrimaryBrush"));
    }

    public void SetSubscriptionAvailable(bool available)
    {
        _isSubscriptionAvailable = available;
        UpdateConnectInteractivity();
        UpdateConnectButtonAppearance();
    }

    public void SetSubscriptionConfigured(bool configured)
    {
        _isSubscriptionConfigured = configured;
        UpdateConnectInteractivity();
    }

    public void SetTrafficStats(string upload, string download, string duration)
    {
        UploadSpeedTextBlock.Text = upload;
        DownloadSpeedTextBlock.Text = download;
        ConnectionTimerTextBlock.Text = duration;
    }

    private void UpdateConnectInteractivity()
    {
        if (_isConnecting || _isSubscriptionImporting)
            return;

        var canConnect = _isSubscriptionAvailable || _isConnected;
        ConnectButton.IsEnabled = canConnect;
        ConnectButton.Cursor = canConnect ? Cursors.Hand : Cursors.Arrow;

        var showBuySubscription = !_isSubscriptionConfigured &&
                                  !_isConnected &&
                                  !_isConnecting &&
                                  !_isSubscriptionImporting;
        BuySubscriptionButton.Visibility = showBuySubscription ? Visibility.Visible : Visibility.Collapsed;
        PasteSubscriptionButton.Visibility = canConnect ? Visibility.Collapsed : Visibility.Visible;
    }

    private void UpdateConnectButtonAppearance()
    {
        ConnectButton.ApplyTemplate();

        if (ConnectButton.Template?.FindName("ConnectRing", ConnectButton) is not System.Windows.Shapes.Ellipse connectRing ||
            ConnectButton.Template?.FindName("ConnectLogo", ConnectButton) is not Controls.LogoMark logo)
        {
            return;
        }

        if (_isConnecting)
        {
            var accentBrush = GetAnimationAccentBrush();
            SetShapeStroke(connectRing, accentBrush);
            AnimateConnectRingOpacity(connectRing, 0.55);
            SetLogoBrush(logo, (Brush)FindResource("ConnectButtonIconBrush"));
            AnimateOpacity(ConnectButton, 1);
            return;
        }

        AnimateConnectRingOpacity(connectRing, 1);

        if (_isConnected)
        {
            SetShapeStroke(connectRing, (Brush)FindResource("TertiaryBrush"));
            SetLogoBrush(logo, (Brush)FindResource("ConnectButtonIconBrush"));
            AnimateOpacity(ConnectButton, 1);
            return;
        }

        if (!_isSubscriptionAvailable)
        {
            SetShapeStroke(connectRing, (Brush)FindResource("ConnectButtonDisabledRingBrush"));
            SetLogoBrush(logo, (Brush)FindResource("OnSurfaceVariantBrush"));
            AnimateOpacity(ConnectButton, 0.55);
            return;
        }

        SetShapeStroke(connectRing, (Brush)FindResource("ConnectButtonRingBrush"));
        SetLogoBrush(logo, (Brush)FindResource("ConnectButtonIconBrush"));
        AnimateOpacity(ConnectButton, 1);
    }

    private static void SetShapeStroke(System.Windows.Shapes.Shape shape, Brush brush) =>
        shape.Stroke = brush;

    private static void SetLogoBrush(Controls.LogoMark logo, Brush brush) =>
        logo.IconBrush = brush;

    private void OnConnectButtonMouseEnter()
    {
        if (_isConnected || _isConnecting || _isSubscriptionImporting || !_isSubscriptionAvailable)
            return;

        ConnectButton.ApplyTemplate();
        if (ConnectButton.Template?.FindName("ConnectRing", ConnectButton) is System.Windows.Shapes.Ellipse connectRing)
            SetShapeStroke(connectRing, (Brush)FindResource("ConnectButtonRingHoverBrush"));
    }

    private void AnimateConnectRingOpacity(UIElement connectRing, double to)
    {
        connectRing.BeginAnimation(UIElement.OpacityProperty, null);

        if (MotionPreferences.SimplifiedAnimations)
        {
            connectRing.Opacity = to;
            return;
        }

        AnimateOpacity(connectRing, to, RingFadeOutDuration, to < 1 ? SmoothEaseOut : FadeOutEase);
    }

    private void StartConnectingAnimations()
    {
        StopConnectingAnimations();

        _connectingSpinStoryboard ??= (Storyboard)FindResource("ConnectingSpinStoryboard");
        _connectingPulseStoryboard ??= (Storyboard)FindResource("ConnectingPulseStoryboard");
        _connectingSpinStoryboard.Begin(this, true);
        _connectingPulseStoryboard.Begin(this, true);
    }

    private void StopConnectingAnimations(bool resetRotation = true)
    {
        _connectingSpinStoryboard?.Stop(this);
        _connectingPulseStoryboard?.Stop(this);

        ConnectingRingOuter.BeginAnimation(UIElement.OpacityProperty, null);
        ConnectingRingInner.BeginAnimation(UIElement.OpacityProperty, null);
        ConnectingRingOuter.Opacity = 0.45;
        ConnectingRingInner.Opacity = 1;

        if (!resetRotation || ConnectingRingRotate is null)
            return;

        ConnectingRingRotate.BeginAnimation(RotateTransform.AngleProperty, null);
        ConnectingRingRotate.Angle = 0;
    }

    private void AnimateConnectingRingVisible(bool visible)
    {
        ConnectingRingFadeHost.BeginAnimation(UIElement.OpacityProperty, null);

        if (MotionPreferences.SimplifiedAnimations)
        {
            if (visible)
            {
                StopConnectingAnimations();
                ConnectingRingFadeHost.Visibility = Visibility.Visible;
                ConnectingRingFadeHost.Opacity = 1;
            }
            else
            {
                StopConnectingAnimations();
                ConnectingRingFadeHost.Visibility = Visibility.Collapsed;
                ConnectingRingFadeHost.Opacity = 0;
            }

            return;
        }

        if (visible)
        {
            StopConnectingAnimations();
            ConnectingRingFadeHost.Visibility = Visibility.Visible;
            AnimateOpacity(ConnectingRingFadeHost, 1, FadeDuration, SmoothEaseOut);
            return;
        }

        if (ConnectingRingFadeHost.Visibility != Visibility.Visible)
        {
            StopConnectingAnimations();
            return;
        }

        var fadeOut = CreateOpacityAnimation(0, RingFadeOutDuration, FadeOutEase);
        fadeOut.Completed += (_, _) =>
        {
            StopConnectingAnimations();
            ConnectingRingFadeHost.Visibility = Visibility.Collapsed;
        };
        ConnectingRingFadeHost.BeginAnimation(UIElement.OpacityProperty, fadeOut);
    }

    private void AnimateTrafficStatsVisible(bool visible)
    {
        TrafficStatsPanel.BeginAnimation(UIElement.OpacityProperty, null);
        TrafficStatsPanel.IsHitTestVisible = visible;

        if (MotionPreferences.SimplifiedAnimations)
        {
            TrafficStatsPanel.Opacity = visible ? 1 : 0;
            return;
        }

        if (visible)
        {
            AnimateOpacity(TrafficStatsPanel, 1, FadeDuration, SmoothEaseOut);
            return;
        }

        var fadeOut = CreateOpacityAnimation(0, RingFadeOutDuration, FadeOutEase);
        TrafficStatsPanel.BeginAnimation(UIElement.OpacityProperty, fadeOut);
    }

    private void AnimateStatusText(string text, Brush brush)
    {
        if (StatusTextBlock.Text == text &&
            Equals(StatusTextBlock.Foreground, brush))
        {
            return;
        }

        if (MotionPreferences.SimplifiedAnimations)
        {
            ResetStatusTextImmediate(text, brush);
            return;
        }

        StatusTextBlock.BeginAnimation(UIElement.OpacityProperty, null);

        var fadeOut = CreateOpacityAnimation(0, StatusFadeDuration, SmoothEaseOut);
        fadeOut.Completed += (_, _) =>
        {
            StatusTextBlock.Text = text;
            StatusTextBlock.Foreground = brush;
            AnimateOpacity(StatusTextBlock, 1, StatusFadeDuration, SmoothEaseOut);
        };
        StatusTextBlock.BeginAnimation(UIElement.OpacityProperty, fadeOut);
    }

    private void ResetStatusTextImmediate(string text, Brush brush)
    {
        StatusTextBlock.BeginAnimation(UIElement.OpacityProperty, null);
        StatusTextBlock.Opacity = 1;
        StatusTextBlock.Text = text;
        StatusTextBlock.Foreground = brush;
    }

    private void AnimateOpacity(UIElement element, double to, TimeSpan? duration = null, IEasingFunction? easing = null)
    {
        if (MotionPreferences.SimplifiedAnimations)
        {
            element.BeginAnimation(UIElement.OpacityProperty, null);
            element.Opacity = to;
            return;
        }

        element.BeginAnimation(UIElement.OpacityProperty, CreateOpacityAnimation(to, duration ?? FadeDuration, easing ?? SmoothEase));
    }

    private static DoubleAnimation CreateOpacityAnimation(double to, TimeSpan duration, IEasingFunction? easing)
    {
        var animation = new DoubleAnimation(to, duration)
        {
            EasingFunction = easing
        };
        return animation;
    }

    private static void AnimateBrush(DependencyObject target, DependencyProperty property, Brush brush)
    {
        if (brush is not SolidColorBrush solidBrush)
        {
            target.SetValue(property, brush);
            return;
        }

        if (target.GetValue(property) is not SolidColorBrush currentBrush)
        {
            target.SetValue(property, brush);
            return;
        }

        var fromColor = currentBrush.Color;
        var toColor = solidBrush.Color;
        if (fromColor == toColor)
            return;

        var animatedBrush = new SolidColorBrush(fromColor);
        target.SetValue(property, animatedBrush);

        var animation = new ColorAnimation(toColor, FadeDuration)
        {
            EasingFunction = SmoothEase
        };
        animatedBrush.BeginAnimation(SolidColorBrush.ColorProperty, animation);
    }

    public void SetCurrentServer(string serverName, ProxyNode? node = null)
    {
        CurrentServerTextBlock.SourceText = string.IsNullOrWhiteSpace(serverName) ? "Не выбран" : serverName;
        ServerFlag.CountryCode = node is not null
            ? ServerDisplayHelper.GetCountryCode(node)
            : "eu";
    }

    public void RefreshThemeAppearance()
    {
        if (_isConnected)
            StatusTextBlock.Foreground = (Brush)FindResource("OnSurfaceBrush");
        else if (!_isConnecting)
            StatusTextBlock.Foreground = (Brush)FindResource("OnSurfaceBrush");

        UpdateConnectButtonAppearance();
    }

    private void ConnectButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isConnecting || _isSubscriptionImporting)
            return;

        var canConnect = _isSubscriptionAvailable || _isConnected;
        if (!canConnect)
            return;

        var statusText = _isConnected ? "Отключение..." : "Подключение...";
        SetConnecting(true, statusText, isDisconnecting: _isConnected);
        ConnectToggleRequested?.Invoke(this, EventArgs.Empty);
    }

    private Brush GetAnimationAccentBrush()
    {
        if (_isSubscriptionImporting)
            return (Brush)FindResource("PrimaryBrush");

        return _isDisconnectingAnimation
            ? (Brush)FindResource("ErrorBrush")
            : (Brush)FindResource("TertiaryBrush");
    }

    private void PasteSubscriptionButton_Click(object sender, RoutedEventArgs e) =>
        PasteSubscriptionRequested?.Invoke(this, EventArgs.Empty);

    private void BuySubscriptionButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://coffeemaniavpn.ru/register",
                UseShellExecute = true
            });
        }
        catch
        {
            // ignored
        }
    }

    private void ServerCard_Click(object sender, MouseButtonEventArgs e) =>
        OpenServersRequested?.Invoke(this, EventArgs.Empty);

    private void PromoBanner_Click(object sender, MouseButtonEventArgs e)
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
