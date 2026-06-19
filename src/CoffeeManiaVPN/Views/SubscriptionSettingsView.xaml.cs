using System.Windows;
using System.Windows.Controls;
using CoffeeManiaVPN.Core.Services;

namespace CoffeeManiaVPN.Views;

public partial class SubscriptionSettingsView : UserControl
{
    public event EventHandler? RefreshRequested;
    public event EventHandler? DeleteRequested;
    public event EventHandler? PasteFromClipboardRequested;
    public event EventHandler<bool>? AutoUpdateChanged;
    public event EventHandler<int>? AutoUpdateIntervalChanged;

    private bool _suppressAutoUpdateEvent;
    private int _selectedIntervalMinutes = 60;

    public SubscriptionSettingsView()
    {
        InitializeComponent();
        _suppressAutoUpdateEvent = true;
        AutoUpdateCheckBox.IsChecked = true;
        _suppressAutoUpdateEvent = false;
        UpdateIntervalButtons();
        UpdateAutoUpdateVisibility();
    }

    public string SubscriptionUrl
    {
        get => SubscriptionUrlTextBox.Text;
        set => SubscriptionUrlTextBox.Text = value;
    }

    public bool AutoUpdateEnabled
    {
        get => AutoUpdateCheckBox.IsChecked == true;
        set
        {
            _suppressAutoUpdateEvent = true;
            AutoUpdateCheckBox.IsChecked = value;
            _suppressAutoUpdateEvent = false;
            UpdateAutoUpdateVisibility();
        }
    }

    public int AutoUpdateIntervalMinutes
    {
        get => _selectedIntervalMinutes;
        set
        {
            _selectedIntervalMinutes = AppSettings.NormalizeAutoUpdateIntervalMinutes(value);
            UpdateIntervalButtons();
            UpdateAutoUpdateHint();
        }
    }

    public void SetSubscriptionConfigured(bool configured)
    {
        EmptySubscriptionPanel.Visibility = configured ? Visibility.Collapsed : Visibility.Visible;
        ConfiguredSubscriptionPanel.Visibility = configured ? Visibility.Visible : Visibility.Collapsed;
    }

    public void SetStatus(string message) => StatusTextBlock.Text = message;

    public void SetBusy(bool busy)
    {
        DeleteButton.IsEnabled = !busy;
        RefreshButton.IsEnabled = !busy;
        PasteButton.IsEnabled = !busy;
        BuySubscriptionButton.IsEnabled = !busy;
        SubscriptionUrlTextBox.IsEnabled = !busy;
    }

    private void PasteButton_Click(object sender, RoutedEventArgs e) =>
        PasteFromClipboardRequested?.Invoke(this, EventArgs.Empty);

    private void BuySubscriptionButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
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

    private void RefreshButton_Click(object sender, RoutedEventArgs e) =>
        RefreshRequested?.Invoke(this, EventArgs.Empty);

    private void DeleteButton_Click(object sender, RoutedEventArgs e) =>
        DeleteRequested?.Invoke(this, EventArgs.Empty);

    private void AutoUpdateCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        UpdateAutoUpdateVisibility();

        if (_suppressAutoUpdateEvent)
            return;

        AutoUpdateChanged?.Invoke(this, AutoUpdateCheckBox.IsChecked == true);
    }

    private void IntervalButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string tag } || !int.TryParse(tag, out var minutes))
            return;

        _selectedIntervalMinutes = AppSettings.NormalizeAutoUpdateIntervalMinutes(minutes);
        UpdateIntervalButtons();
        UpdateAutoUpdateHint();
        AutoUpdateIntervalChanged?.Invoke(this, _selectedIntervalMinutes);
    }

    private void UpdateAutoUpdateVisibility()
    {
        if (AutoUpdateIntervalPanel is null || AutoUpdateCheckBox is null)
            return;

        AutoUpdateIntervalPanel.Visibility = AutoUpdateCheckBox.IsChecked == true
            ? Visibility.Visible
            : Visibility.Collapsed;
        UpdateAutoUpdateHint();
    }

    private void UpdateIntervalButtons()
    {
        SetIntervalButtonStyle(Interval1HourButton, 60);
        SetIntervalButtonStyle(Interval3HoursButton, 180);
        SetIntervalButtonStyle(Interval6HoursButton, 360);
        SetIntervalButtonStyle(Interval24HoursButton, 1440);
    }

    private void SetIntervalButtonStyle(Button button, int minutes)
    {
        button.Style = minutes == _selectedIntervalMinutes
            ? (Style)FindResource("SegmentButtonActiveStyle")
            : (Style)FindResource("SegmentButtonStyle");
    }

    private void UpdateAutoUpdateHint()
    {
        if (AutoUpdateCheckBox.IsChecked != true)
        {
            AutoUpdateHintTextBlock.Text = "Периодически обновлять список серверов";
            return;
        }

        AutoUpdateHintTextBlock.Text = _selectedIntervalMinutes switch
        {
            180 => "Обновлять подписку каждые 3 часа",
            360 => "Обновлять подписку каждые 6 часов",
            1440 => "Обновлять подписку каждые 24 часа",
            _ => "Обновлять подписку каждый час"
        };
    }
}
