using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
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

    private IReadOnlyList<ProxyNode> _nodes = Array.Empty<ProxyNode>();
    private SubscriptionInfo _subscriptionInfo = SubscriptionInfo.Empty;
    private ProxyNode? _selectedNode;
    private bool _isConnected;
    private Page _currentPage = Page.Home;

    public MainWindow()
    {
        InitializeComponent();
        SourceInitialized += OnSourceInitialized;
        AppIconHelper.ApplyWindowIcon(this);

        _settings = AppSettings.Load();
        _deviceIdentity = new DeviceIdentityService(_settings);
        _subscriptionService = new SubscriptionService(_deviceIdentity);
        _xrayDirectory = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "xray"));
        _logsView = new LogsView(_appLog);

        _trafficTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _trafficTimer.Tick += (_, _) => UpdateTrafficStats();

        _subscriptionAutoUpdateTimer = new DispatcherTimer();
        _subscriptionAutoUpdateTimer.Tick += async (_, _) => await RefreshSubscriptionAsync();

        WireViews();
        NavigateTo(Page.Home);

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

        if (!string.IsNullOrWhiteSpace(_settings.SubscriptionUrl))
            _ = RefreshSubscriptionAsync();
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
        About
    }

    private void WireViews()
    {
        _homeView.ConnectToggleRequested += (_, _) => ToggleConnection();
        _homeView.OpenServersRequested += (_, _) => NavigateTo(Page.Servers);

        _serversView.RefreshRequested += async (_, _) => await RefreshSubscriptionAsync();
        _serversView.PingRequested += async (_, _) => await PingServersAsync();
        _serversView.ServerSelected += (_, node) => SelectServer(node);

        _settingsView.OpenServersRequested += (_, _) => NavigateTo(Page.Servers);
        _settingsView.OpenConnectionRequested += (_, _) => NavigateTo(Page.Connection);
        _settingsView.OpenSubscriptionRequested += (_, _) => NavigateTo(Page.Subscription);
        _settingsView.OpenLogsRequested += (_, _) => NavigateTo(Page.Logs);
        _settingsView.OpenAboutRequested += (_, _) => NavigateTo(Page.About);
        _settingsView.CloseAppRequested += (_, _) => Close();

        _subscriptionSettingsView.RefreshRequested += async (_, _) => await RefreshSubscriptionAsync();
        _subscriptionSettingsView.DeleteRequested += async (_, _) => await DeleteSubscriptionAsync();
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
            _ => "Главная"
        };

        var isSubPage = page is Page.Connection
            or Page.ConnectionSites
            or Page.ConnectionApps
            or Page.ConnectionKillSwitch
            or Page.Subscription
            or Page.Logs
            or Page.About;
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

        if (_currentPage is Page.Connection or Page.Subscription or Page.Logs or Page.About)
            NavigateTo(Page.Settings);
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
            _settingsView.SetStatus(ex.Message);
            _subscriptionSettingsView.SetStatus(ex.Message);
            _serversView.SetHeaderStatus(ex.Message);
            _appLog.Append($"Ошибка подписки: {ex.Message}");
        }
        finally
        {
            _subscriptionSettingsView.SetBusy(false);
            _serversView.SetRefreshing(false);
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

    private void ToggleConnection()
    {
        _ = ToggleConnectionAsync();
    }

    private async Task ToggleConnectionAsync()
    {
        if (_isConnected)
        {
            await DisconnectAsync("Отключено.");
            return;
        }

        _selectedNode ??= _serversView.GetSelectedNode();
        if (_selectedNode is null || _selectedNode.IsPlaceholder)
        {
            _settingsView.SetStatus("Выберите сервер на вкладке «Серверы».");
            NavigateTo(Page.Servers);
            return;
        }

        _settingsView.SetStatus("Подключение...");
        _appLog.Append($"Подключение к «{_selectedNode.Name}»...");

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

        _settingsView.SetStatus("Применение настроек...");
        _appLog.Append(statusMessage);

        try
        {
            await _xrayRunner.StopAsync();
            await _xrayRunner.StartAsync(_selectedNode, _xrayDirectory, _settings);
            ApplyKillSwitchOnConnect();
            _settingsView.SetStatus(statusMessage);
        }
        catch (Exception ex)
        {
            await _xrayRunner.StopAsync();
            SetConnected(false, intentionalDisconnect: true);
            _settingsView.SetStatus(ex.Message);
            _appLog.Append($"Ошибка переподключения: {ex.Message}");
        }
    }

    private void ApplyKillSwitchOnConnect()
    {
        if (!_settings.KillSwitchEnabled)
            return;

        try
        {
            var xrayExe = Path.Combine(_xrayDirectory, "xray.exe");
            var appExe = Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "CoffeeManiaVPN.exe");
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
            var appExe = Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "CoffeeManiaVPN.exe");
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

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero)
            return;

        // #1A1614 -> COLORREF BGR
        var borderColor = 0x0014161A;
        _ = DwmSetWindowAttribute(hwnd, DwmwaBorderColor, ref borderColor, sizeof(int));

        var disableNcRendering = 1;
        _ = DwmSetWindowAttribute(hwnd, DwmwaNcRenderingPolicy, ref disableNcRendering, sizeof(int));

        UpdateWindowCorners();
    }

    private void Window_StateChanged(object? sender, EventArgs e) => UpdateWindowCorners();

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

    private void MinimizeButton_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void MaximizeButton_Click(object sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        _killSwitch.Disengage();
        _xrayRunner.Stop();
    }
}
