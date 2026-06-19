using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shell;
using System.Windows.Threading;
using CoffeeManiaVPN.Core.Models;
using CoffeeManiaVPN.Core.Services;
using CoffeeManiaVPN.Core.Xray;
using CoffeeManiaVPN.Helpers;
using CoffeeManiaVPN.Views;

namespace CoffeeManiaVPN;

public partial class MainWindow : Window
{
    private const double SidebarWidthExpanded = 268;
    private const double SidebarWidthCompact = 76;
    private const double SidebarDesignHeight = 320;
    private const double MinWindowWidthExpanded = 620;
    private const double MinWindowWidthCompact = 460;
    private const double MinWindowHeightValue = 440;

    private double _sidebarBaseWidth = SidebarWidthExpanded;
    private double _sidebarDesignWidth = 228;

    private TranslateTransform _pageHostTransform = null!;
    private bool _isPageTransitioning;
    private Page _pendingTransitionPage;

    private readonly AppSettings _settings;
    private readonly DeviceIdentityService _deviceIdentity;
    private readonly SubscriptionService _subscriptionService;
    private readonly ServerPingService _serverPingService = new();
    private readonly VpnTrafficMonitor _trafficMonitor = new();
    private readonly XrayRunner _xrayRunner = new();
    private readonly DispatcherTimer _trafficTimer;
    private readonly DispatcherTimer _subscriptionAutoUpdateTimer;
    private readonly AppLogService _appLog = new();
    private readonly KillSwitchService _killSwitch = new();
    private readonly string _xrayDirectory;

    private readonly HomeView _homeView = new();
    private readonly ServersView _serversView = new();
    private readonly SettingsView _settingsView = new();
    private readonly ConnectionSettingsView _connectionSettingsView = new();
    private readonly SiteSplitTunnelSettingsView _siteSplitTunnelSettingsView = new();
    private readonly AppSplitTunnelSettingsView _appSplitTunnelSettingsView = new();
    private readonly KillSwitchSettingsView _killSwitchSettingsView = new();
    private readonly SubscriptionSettingsView _subscriptionSettingsView = new();
    private readonly LogsView _logsView;
    private readonly AboutView _aboutView = new();
    private readonly ThemeSettingsView _themeSettingsView = new();

    private IReadOnlyList<ProxyNode> _nodes = Array.Empty<ProxyNode>();
    private SubscriptionInfo _subscriptionInfo = SubscriptionInfo.Empty;
    private ProxyNode? _selectedNode;
    private bool _isConnected;
    private Page _currentPage = Page.Home;
    private bool _homeSubscriptionImportPending;
    private readonly SemaphoreSlim _connectionGate = new(1, 1);
    private readonly TrayIconService _trayIcon;

    public MainWindow()
    {
        InitializeComponent();
        _pageHostTransform = new TranslateTransform();
        PageHost.RenderTransform = _pageHostTransform;
        PageHost.RenderTransformOrigin = new Point(0.5, 0.5);
        RenderOptions.SetBitmapScalingMode(PageHost, BitmapScalingMode.HighQuality);
        RenderOptions.SetCachingHint(PageHost, CachingHint.Cache);
        SourceInitialized += OnSourceInitialized;
        AppIconHelper.ApplyWindowIcon(this);

        _settings = AppSettings.Load();
        _deviceIdentity = new DeviceIdentityService(_settings);
        _subscriptionService = new SubscriptionService(_deviceIdentity);
        _xrayDirectory = AppPaths.XrayDirectory;
        _logsView = new LogsView(_appLog);

        _trafficTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _trafficTimer.Tick += (_, _) => UpdateTrafficStats();

        _subscriptionAutoUpdateTimer = new DispatcherTimer();
        _subscriptionAutoUpdateTimer.Tick += async (_, _) => await RefreshSubscriptionAsync();

        WireViews();
        _themeSettingsView.Load(ThemeManager.Parse(_settings.Theme), _settings.CompactSidebar, _settings.SimplifiedAnimations);
        ApplyMotionPreferences(_settings.SimplifiedAnimations);
        ApplyCompactSidebar(_settings.CompactSidebar);
        UpdateResponsiveLayout();
        NavigateTo(Page.Home);

        _trayIcon = new TrayIconService(this, ToggleConnection);
        _trayIcon.UpdateConnectionState(false);

        _appLog.LoadExisting();
        _appLog.Append("Приложение запущено.");
        _killSwitch.Disengage();

        _xrayRunner.LogReceived += (_, message) =>
            Dispatcher.Invoke(() => _appLog.Append(message));
        _xrayRunner.Exited += OnXrayExited;

        _subscriptionSettingsView.SubscriptionUrl = _settings.SubscriptionUrl;
        _subscriptionSettingsView.AutoUpdateEnabled = _settings.AutoUpdateSubscription;
        _subscriptionSettingsView.AutoUpdateIntervalMinutes = _settings.AutoUpdateIntervalMinutes;
        ConfigureSubscriptionAutoUpdate();
        UpdateCurrentServerDisplay();
        UpdateSubscriptionAvailability();

        if (!string.IsNullOrWhiteSpace(_settings.SubscriptionUrl))
            _ = RefreshSubscriptionAsync();

        BrandIntegrityGuard.Attach(this, () => BrandTextBlock.Text);
    }

    public void ActivateFromExternalRequest()
    {
        Dispatcher.Invoke(() =>
        {
            _trayIcon.ShowWindow();
            NavigateTo(Page.Home);
        });
    }

    public void HandleUrlScheme(string rawUrl)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(() => HandleUrlScheme(rawUrl));
            return;
        }

        if (!UrlSchemeParser.TryParse(rawUrl, out var request))
        {
            _appLog.Append($"Не удалось разобрать URL-схему: {rawUrl}");
            return;
        }

        _ = HandleUrlSchemeAsync(request);
    }

    private async Task HandleUrlSchemeAsync(UrlSchemeRequest request)
    {
        ActivateFromExternalRequest();
        _appLog.Append($"URL-схема: cfm://{request.Action.ToString().ToLowerInvariant()}");

        try
        {
            switch (request.Action)
            {
                case UrlSchemeAction.Connect:
                    if (!_isConnected)
                        await ToggleConnectionAsync();
                    break;

                case UrlSchemeAction.Disconnect:
                    if (_isConnected)
                        await ToggleConnectionAsync();
                    break;

                case UrlSchemeAction.Toggle:
                    await ToggleConnectionAsync();
                    break;

                case UrlSchemeAction.Import:
                case UrlSchemeAction.Add:
                    await ImportSubscriptionFromUrlSchemeAsync(request.Payload);
                    break;
            }
        }
        catch (Exception ex)
        {
            _appLog.Append($"Ошибка URL-схемы: {ex.Message}");
            _settingsView.SetStatus(ex.Message);
            _homeSubscriptionImportPending = false;
            if (Dispatcher.CheckAccess())
                _homeView.SetSubscriptionImporting(false);
            else
                Dispatcher.Invoke(() => _homeView.SetSubscriptionImporting(false));
        }
    }

    private async Task ImportSubscriptionFromUrlSchemeAsync(string? payload)
    {
        NavigateTo(Page.Home);
        await Dispatcher.InvokeAsync(static () => { }, DispatcherPriority.Loaded);
        _homeView.SetSubscriptionImporting(true, "Обработка ссылки...");
        _homeSubscriptionImportPending = true;

        try
        {
            _appLog.Append($"Импорт подписки: {payload ?? "(пусто)"}");

            var subscriptionUrl = UrlSchemeParser.ResolveSubscriptionUrl(payload);
            if (string.IsNullOrWhiteSpace(subscriptionUrl))
            {
                var message = "Не удалось распознать ссылку подписки из URL-схемы.";
                _settingsView.SetStatus(message);
                _subscriptionSettingsView.SetStatus(message);
                _appLog.Append(message);
                NavigateTo(Page.Subscription);
                return;
            }

            _homeView.UpdateSubscriptionImportStatus("Загрузка подписки...");
            _appLog.Append($"Подписка из URL-схемы: {subscriptionUrl}");
            await ApplySubscriptionUrlAsync(subscriptionUrl).ConfigureAwait(true);
        }
        finally
        {
            EndHomeSubscriptionImportIfPending();
        }
    }

    private void EndHomeSubscriptionImportIfPending()
    {
        if (!_homeSubscriptionImportPending)
            return;

        _homeSubscriptionImportPending = false;

        if (Dispatcher.CheckAccess())
        {
            _homeView.SetSubscriptionImporting(false);
            return;
        }

        Dispatcher.Invoke(() => _homeView.SetSubscriptionImporting(false));
    }

    private async Task ApplySubscriptionUrlAsync(string subscriptionUrl)
    {
        _subscriptionSettingsView.SetSubscriptionConfigured(true);
        _subscriptionSettingsView.SubscriptionUrl = subscriptionUrl;
        _settings.SubscriptionUrl = subscriptionUrl;
        _settings.Save();
        await RefreshSubscriptionAsync();
    }

    private enum Page
    {
        Home,
        Servers,
        Settings,
        Connection,
        ConnectionSites,
        ConnectionApps,
        ConnectionKillSwitch,
        Subscription,
        Logs,
        About,
        Theme
    }

    private void WireViews()
    {
        _homeView.ConnectToggleRequested += (_, _) => ToggleConnection();
        _homeView.OpenServersRequested += (_, _) => NavigateTo(Page.Servers);
        _homeView.PasteSubscriptionRequested += async (_, _) => await PasteSubscriptionFromClipboardAsync();

        _serversView.RefreshRequested += async (_, _) => await RefreshSubscriptionAsync();
        _serversView.PingRequested += async (_, _) => await PingServersAsync();
        _serversView.ServerSelected += (_, node) => OnServerSelected(node);
        _serversView.ServerReconnectRequested += (_, node) => OnServerReconnectRequested(node);

        _settingsView.OpenServersRequested += (_, _) => NavigateTo(Page.Servers);
        _settingsView.OpenConnectionRequested += (_, _) => NavigateTo(Page.Connection);
        _settingsView.OpenSubscriptionRequested += (_, _) => NavigateTo(Page.Subscription);
        _settingsView.OpenLogsRequested += (_, _) => NavigateTo(Page.Logs);
        _settingsView.OpenAboutRequested += (_, _) => NavigateTo(Page.About);
        _settingsView.OpenThemeRequested += (_, _) => NavigateTo(Page.Theme);
        _settingsView.CloseAppRequested += (_, _) => Close();

        _themeSettingsView.ThemeChanged += (_, theme) => ApplyTheme(theme);
        _themeSettingsView.CompactSidebarChanged += (_, enabled) =>
        {
            _settings.CompactSidebar = enabled;
            _settings.Save();
            ApplyCompactSidebar(enabled);
        };
        _themeSettingsView.SimplifiedAnimationsChanged += (_, enabled) =>
        {
            _settings.SimplifiedAnimations = enabled;
            _settings.Save();
            ApplyMotionPreferences(enabled);
        };

        _subscriptionSettingsView.RefreshRequested += async (_, _) => await RefreshSubscriptionAsync();
        _subscriptionSettingsView.DeleteRequested += async (_, _) => await DeleteSubscriptionAsync();
        _subscriptionSettingsView.PasteFromClipboardRequested += async (_, _) => await PasteSubscriptionFromClipboardAsync();
        _subscriptionSettingsView.AutoUpdateChanged += (_, enabled) =>
        {
            _settings.AutoUpdateSubscription = enabled;
            _settings.Save();
            ConfigureSubscriptionAutoUpdate();
        };
        _subscriptionSettingsView.AutoUpdateIntervalChanged += (_, minutes) =>
        {
            _settings.AutoUpdateIntervalMinutes = AppSettings.NormalizeAutoUpdateIntervalMinutes(minutes);
            _settings.Save();
            ConfigureSubscriptionAutoUpdate();
        };

        _connectionSettingsView.OpenSiteSplitRequested += (_, _) => NavigateTo(Page.ConnectionSites);
        _connectionSettingsView.OpenAppSplitRequested += (_, _) => NavigateTo(Page.ConnectionApps);
        _connectionSettingsView.OpenKillSwitchRequested += (_, _) => NavigateTo(Page.ConnectionKillSwitch);

        _siteSplitTunnelSettingsView.EnabledChanged += (_, enabled) =>
        {
            _settings.SiteSplitTunnelEnabled = enabled;
            _settings.Save();
            _ = ReconnectIfConnectedAsync("Настройки сайтов применены.");
        };
        _siteSplitTunnelSettingsView.ModeChanged += (_, mode) =>
        {
            _settings.SiteSplitTunnelMode = mode;
            _settings.Save();
            _ = ReconnectIfConnectedAsync("Режим туннелирования сайтов изменён.");
        };
        _siteSplitTunnelSettingsView.DomainsChanged += (_, domains) =>
        {
            _settings.SiteSplitTunnelDomains = domains.ToList();
            _settings.Save();
            _ = ReconnectIfConnectedAsync("Список доменов обновлён.");
        };

        _appSplitTunnelSettingsView.EnabledChanged += (_, enabled) =>
        {
            _settings.AppSplitTunnelEnabled = enabled;
            _settings.Save();
            _ = ReconnectIfConnectedAsync("Настройки приложений применены.");
        };
        _appSplitTunnelSettingsView.ModeChanged += (_, mode) =>
        {
            _settings.AppSplitTunnelMode = mode;
            _settings.Save();
            _ = ReconnectIfConnectedAsync("Режим туннелирования приложений изменён.");
        };
        _appSplitTunnelSettingsView.AppsChanged += (_, apps) =>
        {
            _settings.AppSplitTunnelApps = apps.ToList();
            _settings.Save();
            _ = ReconnectIfConnectedAsync("Список приложений обновлён.");
        };

        _killSwitchSettingsView.EnabledChanged += (_, enabled) =>
        {
            _settings.KillSwitchEnabled = enabled;
            _settings.Save();

            if (!enabled && _killSwitch.IsEngaged)
            {
                _killSwitch.Disengage();
                _killSwitchSettingsView.UpdateEngagedStatus(false);
                _appLog.Append("Kill Switch отключён.");
            }
            else
            {
                _killSwitchSettingsView.UpdateEngagedStatus(_killSwitch.IsEngaged);
            }
        };
    }

    private void NavigateTo(Page page)
    {
        if (!_isPageTransitioning && page == _currentPage && PageHost.Content is not null)
        {
            ApplyPageChrome(page);
            return;
        }

        if (!IsLoaded || PageHost.Content is null || MotionPreferences.SimplifiedAnimations)
        {
            ApplyPageContent(page);
            ApplyPageChrome(page);
            PageHost.Opacity = 1;
            PageHost.CacheMode = null;
            _isPageTransitioning = false;
            return;
        }

        _pendingTransitionPage = page;

        if (_isPageTransitioning)
            return;

        BeginPageTransition();
    }

    private void BeginPageTransition()
    {
        _isPageTransitioning = true;
        PageHost.CacheMode = new BitmapCache(1.0);

        PageHost.BeginAnimation(UIElement.OpacityProperty, null);
        _pageHostTransform.Y = 0;
        _pageHostTransform.BeginAnimation(TranslateTransform.YProperty, null);

        var ease = new SineEase { EasingMode = EasingMode.EaseInOut };
        var fadeOut = new DoubleAnimation(0.9, TimeSpan.FromMilliseconds(120))
        {
            EasingFunction = ease,
            FillBehavior = FillBehavior.HoldEnd
        };

        fadeOut.Completed += (_, _) =>
        {
            var page = _pendingTransitionPage;
            ApplyPageContent(page);
            ApplyPageChrome(page);

            PageHost.Opacity = 0.9;

            var fadeIn = new DoubleAnimation(1, TimeSpan.FromMilliseconds(240))
            {
                EasingFunction = ease,
                FillBehavior = FillBehavior.Stop
            };

            fadeIn.Completed += (_, _) =>
            {
                PageHost.BeginAnimation(UIElement.OpacityProperty, null);
                PageHost.Opacity = 1;
                PageHost.CacheMode = null;
                _isPageTransitioning = false;

                if (_pendingTransitionPage != _currentPage)
                    BeginPageTransition();
            };

            PageHost.BeginAnimation(UIElement.OpacityProperty, fadeIn);
        };

        PageHost.BeginAnimation(UIElement.OpacityProperty, fadeOut);
    }

    private void ApplyPageContent(Page page)
    {
        _currentPage = page;

        PageHost.Content = page switch
        {
            Page.Home => _homeView,
            Page.Servers => _serversView,
            Page.Settings => _settingsView,
            Page.Connection => _connectionSettingsView,
            Page.ConnectionSites => _siteSplitTunnelSettingsView,
            Page.ConnectionApps => _appSplitTunnelSettingsView,
            Page.ConnectionKillSwitch => _killSwitchSettingsView,
            Page.Subscription => _subscriptionSettingsView,
            Page.Logs => _logsView,
            Page.About => _aboutView,
            Page.Theme => _themeSettingsView,
            _ => _homeView
        };

        if (page == Page.ConnectionSites)
        {
            _siteSplitTunnelSettingsView.Load(
                _settings.SiteSplitTunnelEnabled,
                _settings.SiteSplitTunnelMode,
                _settings.SiteSplitTunnelDomains);
        }
        else if (page == Page.ConnectionApps)
        {
            _appSplitTunnelSettingsView.Load(
                _settings.AppSplitTunnelEnabled,
                _settings.AppSplitTunnelMode,
                _settings.AppSplitTunnelApps);
        }
        else if (page == Page.ConnectionKillSwitch)
        {
            _killSwitchSettingsView.Load(_settings.KillSwitchEnabled, _killSwitch.IsEngaged);
        }
        else if (page == Page.Theme)
        {
            _themeSettingsView.Load(ThemeManager.Parse(_settings.Theme), _settings.CompactSidebar, _settings.SimplifiedAnimations);
        }
    }

    private void ApplyPageChrome(Page page)
    {
        PageTitleTextBlock.Text = page switch
        {
            Page.Home => "Главная",
            Page.Servers => "Серверы",
            Page.Settings => "Настройки",
            Page.Connection => "Соединение",
            Page.ConnectionSites => "Туннелирование сайтов",
            Page.ConnectionApps => "Туннелирование приложений",
            Page.ConnectionKillSwitch => "Kill switch",
            Page.Subscription => "Подписка",
            Page.Logs => "Логи",
            Page.About => "О КОФЕМАНИЯ ВПН",
            Page.Theme => "Визуал",
            _ => "Главная"
        };

        var isSubPage = page is Page.Connection
            or Page.ConnectionSites
            or Page.ConnectionApps
            or Page.ConnectionKillSwitch
            or Page.Subscription
            or Page.Logs
            or Page.About
            or Page.Theme;
        BackButton.Visibility = isSubPage ? Visibility.Visible : Visibility.Collapsed;

        NavHomeButton.Style = page == Page.Home
            ? (Style)FindResource("NavPillActiveButtonStyle")
            : (Style)FindResource("NavPillButtonStyle");
        NavServersButton.Style = page == Page.Servers
            ? (Style)FindResource("NavPillActiveButtonStyle")
            : (Style)FindResource("NavPillButtonStyle");
        NavSettingsButton.Style = page is Page.Settings
            or Page.Connection
            or Page.ConnectionSites
            or Page.ConnectionApps
            or Page.ConnectionKillSwitch
            or Page.Subscription
            or Page.Logs
            or Page.About
            or Page.Theme
            ? (Style)FindResource("NavPillActiveButtonStyle")
            : (Style)FindResource("NavPillButtonStyle");
    }

    private void NavigateBack()
    {
        if (_currentPage is Page.ConnectionSites or Page.ConnectionApps or Page.ConnectionKillSwitch)
        {
            NavigateTo(Page.Connection);
            return;
        }

        if (_currentPage is Page.Connection or Page.Subscription or Page.Logs or Page.About or Page.Theme)
            NavigateTo(Page.Settings);
    }

    private void ApplyTheme(AppTheme theme)
    {
        ThemeManager.Apply(theme);
        _settings.Theme = ThemeManager.ToSettingValue(theme);
        _settings.Save();
        _themeSettingsView.Load(theme, _settings.CompactSidebar, _settings.SimplifiedAnimations);
        ApplyWindowBorderColor();
        _homeView.RefreshThemeAppearance();
    }

    private void ApplyMotionPreferences(bool simplified)
    {
        MotionPreferences.SimplifiedAnimations = simplified;
    }

    private void ApplyCompactSidebar(bool compact)
    {
        _sidebarBaseWidth = compact ? SidebarWidthCompact : SidebarWidthExpanded;
        _sidebarDesignWidth = compact ? 52 : 228;

        SidebarPanel.Width = _sidebarDesignWidth;
        SidebarPanel.Margin = compact ? new Thickness(12, 28, 12, 24) : new Thickness(20, 28, 20, 24);
        BrandPanel.HorizontalAlignment = compact ? HorizontalAlignment.Center : HorizontalAlignment.Left;
        BrandTextBlock.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;

        var labelVisibility = compact ? Visibility.Collapsed : Visibility.Visible;
        NavHomeLabel.Visibility = labelVisibility;
        NavServersLabel.Visibility = labelVisibility;
        NavSettingsLabel.Visibility = labelVisibility;

        var iconMargin = compact ? new Thickness(0) : new Thickness(0, 0, 14, 0);
        NavHomeIcon.Margin = iconMargin;
        NavServersIcon.Margin = iconMargin;
        NavSettingsIcon.Margin = iconMargin;

        NavHomeButton.HorizontalContentAlignment = compact ? HorizontalAlignment.Center : HorizontalAlignment.Stretch;
        NavServersButton.HorizontalContentAlignment = compact ? HorizontalAlignment.Center : HorizontalAlignment.Stretch;
        NavSettingsButton.HorizontalContentAlignment = compact ? HorizontalAlignment.Center : HorizontalAlignment.Stretch;
        NavHomeButton.Padding = compact ? new Thickness(10, 12, 10, 12) : new Thickness(14, 12, 14, 12);
        NavServersButton.Padding = compact ? new Thickness(10, 12, 10, 12) : new Thickness(14, 12, 14, 12);
        NavSettingsButton.Padding = compact ? new Thickness(10, 12, 10, 12) : new Thickness(14, 12, 14, 12);

        NavHomeButton.ToolTip = compact ? "Главная" : null;
        NavServersButton.ToolTip = compact ? "Серверы" : null;
        NavSettingsButton.ToolTip = compact ? "Настройки" : null;

        ContentHostGrid.Margin = compact ? new Thickness(16, 16, 16, 20) : new Thickness(24, 20, 24, 32);
        MinWidth = compact ? MinWindowWidthCompact : MinWindowWidthExpanded;
        MinHeight = MinWindowHeightValue;

        UpdateResponsiveLayout();
    }

    private void Window_SizeChanged(object sender, SizeChangedEventArgs e) =>
        UpdateResponsiveLayout();

    private void UpdateResponsiveLayout()
    {
        if (!IsLoaded)
            return;

        var heightScale = Math.Clamp(ActualHeight / 760.0, 0.62, 1.0);
        var widthReference = _settings.CompactSidebar ? 480.0 : 920.0;
        var widthScale = Math.Clamp(ActualWidth / widthReference, 0.62, 1.0);
        var scale = Math.Min(heightScale, widthScale);

        var sidebarWidth = _sidebarBaseWidth * scale;
        var sidebarHeight = SidebarDesignHeight * scale;

        SidebarViewbox.MaxWidth = sidebarWidth;
        SidebarViewbox.MaxHeight = sidebarHeight;
        SidebarColumn.Width = new GridLength(sidebarWidth);
    }

    private async Task PasteSubscriptionFromClipboardAsync()
    {
        if (!Clipboard.ContainsText())
        {
            const string message = "Буфер обмена пуст.";
            _settingsView.SetStatus(message);
            _subscriptionSettingsView.SetStatus(message);
            return;
        }

        var url = Clipboard.GetText().Trim();
        if (string.IsNullOrWhiteSpace(url))
        {
            const string message = "В буфере нет ссылки подписки.";
            _settingsView.SetStatus(message);
            _subscriptionSettingsView.SetStatus(message);
            return;
        }

        await ApplySubscriptionUrlAsync(url);
    }

    private void UpdateSubscriptionAvailability()
    {
        var hasConfiguredUrl = !string.IsNullOrWhiteSpace(_settings.SubscriptionUrl);
        var hasServers = _nodes.Any(static node => !node.IsPlaceholder);

        _subscriptionSettingsView.SetSubscriptionConfigured(hasConfiguredUrl);
        _homeView.SetSubscriptionConfigured(hasConfiguredUrl);
        _homeView.SetSubscriptionAvailable(hasConfiguredUrl && hasServers);
    }

    private async Task RefreshSubscriptionAsync()
    {
        var url = _subscriptionSettingsView.SubscriptionUrl.Trim();
        if (string.IsNullOrWhiteSpace(url))
            url = _settings.SubscriptionUrl.Trim();

        if (string.IsNullOrWhiteSpace(url))
        {
            var message = "Укажите ссылку подписки в настройках.";
            _settingsView.SetStatus(message);
            _subscriptionSettingsView.SetStatus(message);
            _serversView.SetHeaderStatus(message);
            return;
        }

        _settingsView.SetStatus("Загрузка подписки...");
        _subscriptionSettingsView.SetStatus("Загрузка подписки...");
        _subscriptionSettingsView.SetBusy(true);
        _serversView.SetRefreshing(true);
        _serversView.SetHeaderStatus("Обновление подписки...");
        _appLog.Append("Обновление подписки...");

        try
        {
            var result = await _subscriptionService.FetchAsync(url);
            _nodes = result.Nodes;
            _subscriptionInfo = result.Info;
            _settings.SubscriptionUrl = url;
            _settings.Save();
            _subscriptionSettingsView.SubscriptionUrl = url;
            ConfigureSubscriptionAutoUpdate();

            var selectedIndex = _settings.SelectedNodeIndex;
            if (selectedIndex < 0 || selectedIndex >= _nodes.Count)
            {
                var firstAvailable = _nodes.Select((node, index) => (node, index))
                    .FirstOrDefault(item => !item.node.IsPlaceholder);
                selectedIndex = firstAvailable.node is not null ? firstAvailable.index : 0;
            }

            _serversView.SetServers(_nodes, selectedIndex);
            _serversView.SetSubscriptionInfo(_subscriptionInfo);
            _selectedNode = _serversView.GetSelectedNode();
            if (_selectedNode is not null)
            {
                _settings.SelectedNodeIndex = selectedIndex;
                _settings.Save();
            }

            UpdateCurrentServerDisplay();
            UpdateSubscriptionAvailability();
            var success = $"Загружено серверов: {_nodes.Count}.";
            _settingsView.SetStatus(success);
            _subscriptionSettingsView.SetStatus(success);
            _serversView.SetHeaderStatus(success);
            _appLog.Append(success);
        }
        catch (Exception ex)
        {
            _nodes = Array.Empty<ProxyNode>();
            _subscriptionInfo = SubscriptionInfo.Empty;
            _serversView.SetServers(_nodes, -1);
            _serversView.SetSubscriptionInfo(_subscriptionInfo);
            _selectedNode = null;
            UpdateCurrentServerDisplay();
            UpdateSubscriptionAvailability();
            _settingsView.SetStatus(ex.Message);
            _subscriptionSettingsView.SetStatus(ex.Message);
            _serversView.SetHeaderStatus(ex.Message);
            _appLog.Append($"Ошибка подписки: {ex.Message}");
        }
        finally
        {
            _subscriptionSettingsView.SetBusy(false);
            _serversView.SetRefreshing(false);
            EndHomeSubscriptionImportIfPending();
        }
    }

    private async Task DeleteSubscriptionAsync()
    {
        if (_isConnected)
        {
            await DisconnectAsync("VPN отключён.");
        }

        _settings.SubscriptionUrl = string.Empty;
        _settings.SelectedNodeIndex = -1;
        _settings.Save();

        _nodes = Array.Empty<ProxyNode>();
        _subscriptionInfo = SubscriptionInfo.Empty;
        _selectedNode = null;

        _subscriptionSettingsView.SubscriptionUrl = string.Empty;
        _serversView.SetServers(_nodes, -1);
        _serversView.SetSubscriptionInfo(_subscriptionInfo);
        UpdateCurrentServerDisplay();
        ConfigureSubscriptionAutoUpdate();
        UpdateSubscriptionAvailability();

        const string message = "Подписка удалена.";
        _settingsView.SetStatus(message);
        _subscriptionSettingsView.SetStatus(message);
        _serversView.SetHeaderStatus(message);
        _appLog.Append(message);
    }

    private void ConfigureSubscriptionAutoUpdate()
    {
        var intervalMinutes = AppSettings.NormalizeAutoUpdateIntervalMinutes(_settings.AutoUpdateIntervalMinutes);
        _settings.AutoUpdateIntervalMinutes = intervalMinutes;
        _subscriptionAutoUpdateTimer.Interval = TimeSpan.FromMinutes(intervalMinutes);

        if (_settings.AutoUpdateSubscription &&
            !string.IsNullOrWhiteSpace(_settings.SubscriptionUrl))
        {
            _subscriptionAutoUpdateTimer.Start();
            return;
        }

        _subscriptionAutoUpdateTimer.Stop();
    }

    private async Task PingServersAsync()
    {
        if (_nodes.Count == 0)
        {
            _serversView.SetHeaderStatus("Сначала загрузите подписку.");
            return;
        }

        _serversView.SetPinging(true);
        _serversView.SetHeaderStatus("Пинг серверов...");

        try
        {
            var progress = new Progress<(ProxyNode Node, int? Ping)>(tuple =>
                _serversView.ApplyPingResult(tuple.Node, tuple.Ping));

            var results = await _serverPingService.PingNodesAsync(_nodes, progress);

            var successful = results.Values.Count(static ping => ping is > 0);
            _serversView.SetHeaderStatus($"Пинг завершён: {successful}/{results.Count}");
        }
        catch (Exception ex)
        {
            _serversView.SetHeaderStatus(ex.Message);
        }
        finally
        {
            _serversView.SetPinging(false);
        }
    }

    private void SelectServer(ProxyNode node)
    {
        _selectedNode = node;
        var index = _nodes.Select((n, i) => (n, i))
            .FirstOrDefault(x => ReferenceEquals(x.n, node) || x.n.Name == node.Name).i;
        if (index >= 0)
        {
            _settings.SelectedNodeIndex = index;
            _settings.Save();
        }

        UpdateCurrentServerDisplay();
    }

    private void OnServerSelected(ProxyNode node)
    {
        if (node.IsPlaceholder)
        {
            SelectServer(node);
            return;
        }

        var previousNode = _selectedNode;
        SelectServer(node);

        if (!_isConnected || previousNode is null || IsSameServer(previousNode, node))
            return;

        var serverName = ServerDisplayHelper.GetShortName(node);
        _ = ReconnectIfConnectedAsync($"Переключено на «{serverName}».");
    }

    private void OnServerReconnectRequested(ProxyNode node)
    {
        if (node.IsPlaceholder)
            return;

        SelectServer(node);
        NavigateTo(Page.Home);

        if (_isConnected)
        {
            var serverName = ServerDisplayHelper.GetShortName(node);
            _ = ReconnectIfConnectedAsync($"Переподключение к «{serverName}».");
            return;
        }

        _ = ToggleConnectionAsync();
    }

    private static bool IsSameServer(ProxyNode left, ProxyNode right) =>
        ReferenceEquals(left, right) ||
        (left.Address == right.Address && left.Port == right.Port);

    private void ToggleConnection()
    {
        _ = ToggleConnectionAsync();
    }

    private async Task ToggleConnectionAsync()
    {
        if (!await _connectionGate.WaitAsync(0))
            return;

        try
        {
            if (_isConnected)
            {
                _homeView.SetConnecting(true, "Отключение...", isDisconnecting: true);
                try
                {
                    await DisconnectAsync("Отключено.");
                }
                finally
                {
                    _homeView.SetConnecting(false);
                }

                return;
            }

            if (!AdminElevationHelper.IsAdministrator())
            {
                _homeView.SetConnecting(false);
                if (AdminElevationHelper.TryEnsureAdministrator())
                    Application.Current.Shutdown();

                return;
            }

            _selectedNode ??= _serversView.GetSelectedNode();
            if (_selectedNode is null || _selectedNode.IsPlaceholder)
            {
                _homeView.SetConnecting(false);
                _settingsView.SetStatus("Выберите сервер на вкладке «Серверы».");
                NavigateTo(Page.Servers);
                return;
            }

            _settingsView.SetStatus("Подключение...");
            _appLog.Append($"Подключение к «{_selectedNode.Name}»...");
            _homeView.SetConnecting(true);

            try
            {
                await _xrayRunner.StartAsync(_selectedNode, _xrayDirectory, _settings);
                SetConnected(true, intentionalDisconnect: true);
                ApplyKillSwitchOnConnect();
                _settingsView.SetStatus($"Подключено к «{_selectedNode.Name}» через TUN.");
                _appLog.Append($"VPN подключён: «{_selectedNode.Name}».");
            }
            catch (Exception ex)
            {
                await _xrayRunner.StopAsync();
                SetConnected(false, intentionalDisconnect: true);
                _settingsView.SetStatus(ex.Message);
                _appLog.Append($"Ошибка подключения: {ex.Message}");
            }
        }
        finally
        {
            _connectionGate.Release();
        }
    }

    private async Task DisconnectAsync(string statusMessage)
    {
        await _xrayRunner.StopAsync();
        SetConnected(false, intentionalDisconnect: true);
        _settingsView.SetStatus(statusMessage);
        _appLog.Append(statusMessage);
    }

    private async Task ReconnectIfConnectedAsync(string statusMessage)
    {
        if (!_isConnected || _selectedNode is null || _selectedNode.IsPlaceholder)
            return;

        if (!await _connectionGate.WaitAsync(0))
            return;

        _settingsView.SetStatus("Применение настроек...");
        _appLog.Append(statusMessage);
        _homeView.SetConnecting(true, "Переподключение...");

        try
        {
            await _xrayRunner.StopAsync();
            await _xrayRunner.StartAsync(_selectedNode, _xrayDirectory, _settings);
            ApplyKillSwitchOnConnect();
            _settingsView.SetStatus(statusMessage);
            _serversView.SetHeaderStatus(statusMessage);
        }
        catch (Exception ex)
        {
            await _xrayRunner.StopAsync();
            SetConnected(false, intentionalDisconnect: true);
            _settingsView.SetStatus(ex.Message);
            _appLog.Append($"Ошибка переподключения: {ex.Message}");
        }
        finally
        {
            _homeView.SetConnecting(false);
            _connectionGate.Release();
        }
    }

    private void ApplyKillSwitchOnConnect()
    {
        if (!_settings.KillSwitchEnabled)
            return;

        try
        {
            var xrayExe = Path.Combine(_xrayDirectory, "xray.exe");
            var appExe = Environment.ProcessPath ?? Path.Combine(AppPaths.Root, "CoffeeManiaVPN.exe");
            _killSwitch.Engage(xrayExe, appExe);
            _killSwitchSettingsView.UpdateEngagedStatus(false);
            _appLog.Append("Kill Switch активирован.");
        }
        catch (Exception ex)
        {
            _appLog.Append($"Kill Switch: {ex.Message}");
            _killSwitchSettingsView.SetStatus(ex.Message);
        }
    }

    private void ApplyKillSwitchOnDisconnect(bool intentional)
    {
        if (intentional || !_settings.KillSwitchEnabled)
        {
            _killSwitch.Disengage();
            _killSwitchSettingsView.UpdateEngagedStatus(false);
            return;
        }

        try
        {
            var xrayExe = Path.Combine(_xrayDirectory, "xray.exe");
            var appExe = Environment.ProcessPath ?? Path.Combine(AppPaths.Root, "CoffeeManiaVPN.exe");
            _killSwitch.Engage(xrayExe, appExe);
            _killSwitchSettingsView.UpdateEngagedStatus(true);
            _appLog.Append("Kill Switch: интернет заблокирован после обрыва VPN.");
        }
        catch (Exception ex)
        {
            _appLog.Append($"Kill Switch: {ex.Message}");
        }
    }

    private void SetConnected(bool connected, bool intentionalDisconnect = false)
    {
        _isConnected = connected;
        var serverName = _selectedNode is not null
            ? ServerDisplayHelper.GetShortName(_selectedNode)
            : null;

        if (connected)
        {
            _trafficMonitor.Start();
            _trafficTimer.Start();
            UpdateTrafficStats();
        }
        else
        {
            _trafficTimer.Stop();
            _trafficMonitor.Stop();
            ApplyKillSwitchOnDisconnect(intentionalDisconnect);
        }

        _homeView.SetConnected(connected, serverName);
        _trayIcon.UpdateConnectionState(connected);
    }

    private void UpdateTrafficStats()
    {
        if (!_isConnected)
            return;

        var snapshot = _trafficMonitor.Sample();
        _homeView.SetTrafficStats(snapshot.Upload, snapshot.Download, snapshot.Duration);
    }

    private void UpdateCurrentServerDisplay()
    {
        var name = _selectedNode is not null
            ? ServerDisplayHelper.GetShortName(_selectedNode)
            : "Не выбран";
        _homeView.SetCurrentServer(name, _selectedNode);
    }

    private void OnXrayExited(object? sender, int code)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(() => OnXrayExited(sender, code));
            return;
        }

        if (!_isConnected)
            return;

        _ = OnXrayExitedAsync(code);
    }

    private async Task OnXrayExitedAsync(int code)
    {
        if (!await _connectionGate.WaitAsync(0))
            return;

        try
        {
            if (!_isConnected)
                return;

            await _xrayRunner.StopAsync();
            SetConnected(false, intentionalDisconnect: false);
            var message = code == 0
                ? "Соединение завершено."
                : _settings.KillSwitchEnabled
                    ? $"Xray завершился с кодом {code}. Kill Switch заблокировал интернет."
                    : $"Xray завершился с кодом {code}.";
            _settingsView.SetStatus(message);
            _appLog.Append(message);
        }
        finally
        {
            _homeView.SetConnecting(false);
            _connectionGate.Release();
        }
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero)
            return;

        // COLORREF BGR
        ApplyWindowBorderColor();

        var disableNcRendering = 1;
        _ = DwmSetWindowAttribute(hwnd, DwmwaNcRenderingPolicy, ref disableNcRendering, sizeof(int));

        UpdateWindowCorners();
    }

    private void Window_StateChanged(object? sender, EventArgs e)
    {
        UpdateWindowCorners();
        UpdateResponsiveLayout();

        if (WindowState == WindowState.Minimized && !_trayIcon.IsExiting)
            _trayIcon.HideToTray();
    }

    private void UpdateWindowCorners()
    {
        var rounded = WindowState != WindowState.Maximized;
        var radius = rounded ? 14.0 : 0.0;
        RootBorder.CornerRadius = new CornerRadius(radius);

        var chrome = WindowChrome.GetWindowChrome(this);
        if (chrome is not null)
            chrome.CornerRadius = new CornerRadius(radius);

        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero)
            return;

        var cornerPreference = rounded ? DwmwcpRound : DwmwcpDoNotRound;
        _ = DwmSetWindowAttribute(hwnd, DwmwaWindowCornerPreference, ref cornerPreference, sizeof(int));
    }

    private void ApplyWindowBorderColor()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero)
            return;

        var color = Application.Current.TryFindResource("WindowBorderColor") is Color themeColor
            ? themeColor
            : (Color)Application.Current.FindResource("SurfaceColor");
        var borderColor = color.B | (color.G << 8) | (color.R << 16);
        _ = DwmSetWindowAttribute(hwnd, DwmwaBorderColor, ref borderColor, sizeof(int));
    }

    private const int DwmwaBorderColor = 34;
    private const int DwmwaNcRenderingPolicy = 2;
    private const int DwmwaWindowCornerPreference = 33;
    private const int DwmwcpDoNotRound = 1;
    private const int DwmwcpRound = 2;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int pvAttribute, int cbAttribute);

    private void NavHomeButton_Click(object sender, RoutedEventArgs e) => NavigateTo(Page.Home);
    private void NavServersButton_Click(object sender, RoutedEventArgs e) => NavigateTo(Page.Servers);
    private void NavSettingsButton_Click(object sender, RoutedEventArgs e) => NavigateTo(Page.Settings);
    private void BackButton_Click(object sender, RoutedEventArgs e) => NavigateBack();

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        else
            DragMove();
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e) => _trayIcon.HideToTray();
    private void MaximizeButton_Click(object sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    private void CloseButton_Click(object sender, RoutedEventArgs e) => _trayIcon.HideToTray();

    private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (!_trayIcon.IsExiting)
        {
            e.Cancel = true;
            _trayIcon.HideToTray();
            return;
        }

        _killSwitch.Disengage();
        _xrayRunner.Stop();
        _trayIcon.Dispose();
        System.Windows.Application.Current.Shutdown();
    }
}
