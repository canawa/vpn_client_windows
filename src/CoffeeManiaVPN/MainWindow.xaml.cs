using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
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
    private readonly string _xrayDirectory;

    private readonly HomeView _homeView = new();
    private readonly ServersView _serversView = new();
    private readonly SettingsView _settingsView = new();
    private readonly ConnectionSettingsView _connectionSettingsView = new();
    private readonly SubscriptionSettingsView _subscriptionSettingsView = new();
    private readonly LogsView _logsView = new();
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

        _trafficTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _trafficTimer.Tick += (_, _) => UpdateTrafficStats();

        WireViews();
        NavigateTo(Page.Home);

        _xrayRunner.LogReceived += (_, message) => Dispatcher.Invoke(() =>
        {
            _logsView.AppendLog(message);
            _settingsView.SetStatus(message);
            _subscriptionSettingsView.SetStatus(message);
        });
        _xrayRunner.Exited += OnXrayExited;

        _subscriptionSettingsView.SubscriptionUrl = _settings.SubscriptionUrl;
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
            Page.Subscription => _subscriptionSettingsView,
            Page.Logs => _logsView,
            Page.About => _aboutView,
            _ => _homeView
        };

        PageTitleTextBlock.Text = page switch
        {
            Page.Home => "Главная",
            Page.Servers => "Серверы",
            Page.Settings => "Настройки",
            Page.Connection => "Соединение",
            Page.Subscription => "Подписка",
            Page.Logs => "Логи",
            Page.About => "О КОФЕМАНИЯ ВПН",
            _ => "Главная"
        };

        var isSubPage = page is Page.Connection or Page.Subscription or Page.Logs or Page.About;
        BackButton.Visibility = isSubPage ? Visibility.Visible : Visibility.Collapsed;
        SettingsButton.Visibility = page == Page.Settings ? Visibility.Collapsed : Visibility.Visible;

        NavHomeButton.Style = page == Page.Home
            ? (Style)FindResource("NavPillActiveButtonStyle")
            : (Style)FindResource("NavPillButtonStyle");
        NavServersButton.Style = page == Page.Servers
            ? (Style)FindResource("NavPillActiveButtonStyle")
            : (Style)FindResource("NavPillButtonStyle");
        NavSettingsButton.Style = page is Page.Settings or Page.Connection or Page.Subscription or Page.Logs or Page.About
            ? (Style)FindResource("NavPillActiveButtonStyle")
            : (Style)FindResource("NavPillButtonStyle");
    }

    private void NavigateBack()
    {
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
        _serversView.SetRefreshing(true);
        _serversView.SetHeaderStatus("Обновление подписки...");

        try
        {
            var result = await _subscriptionService.FetchAsync(url);
            _nodes = result.Nodes;
            _subscriptionInfo = result.Info;
            _settings.SubscriptionUrl = url;
            _settings.Save();
            _subscriptionSettingsView.SubscriptionUrl = url;

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
        }
        finally
        {
            _serversView.SetRefreshing(false);
        }
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
            await _xrayRunner.StopAsync();
            SetConnected(false);
            _settingsView.SetStatus("Отключено.");
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

        try
        {
            await _xrayRunner.StartAsync(_selectedNode, _xrayDirectory);
            SetConnected(true);
            _settingsView.SetStatus($"Подключено к «{_selectedNode.Name}» через TUN.");
        }
        catch (Exception ex)
        {
            await _xrayRunner.StopAsync();
            SetConnected(false);
            _settingsView.SetStatus(ex.Message);
        }
    }

    private void SetConnected(bool connected)
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
        SetConnected(false);
        var message = code == 0
            ? "Соединение завершено."
            : $"Xray завершился с кодом {code}.";
        _settingsView.SetStatus(message);
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
    }

    private const int DwmwaBorderColor = 34;
    private const int DwmwaNcRenderingPolicy = 2;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int pvAttribute, int cbAttribute);

    private void NavHomeButton_Click(object sender, RoutedEventArgs e) => NavigateTo(Page.Home);
    private void NavServersButton_Click(object sender, RoutedEventArgs e) => NavigateTo(Page.Servers);
    private void NavSettingsButton_Click(object sender, RoutedEventArgs e) => NavigateTo(Page.Settings);
    private void SettingsButton_Click(object sender, RoutedEventArgs e) => NavigateTo(Page.Settings);
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

    private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e) =>
        _xrayRunner.Stop();
}
