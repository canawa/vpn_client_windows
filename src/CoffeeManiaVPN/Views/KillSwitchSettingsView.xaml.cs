using System.Windows;
using System.Windows.Controls;

namespace CoffeeManiaVPN.Views;

public partial class KillSwitchSettingsView : UserControl
{
    public event EventHandler<bool>? EnabledChanged;

    public KillSwitchSettingsView()
    {
        InitializeComponent();
    }

    public void Load(bool enabled, bool isEngaged)
    {
        EnabledCheckBox.IsChecked = enabled;
        UpdateEngagedStatus(isEngaged);
    }

    private void EnabledCheckBox_Changed(object sender, RoutedEventArgs e) =>
        EnabledChanged?.Invoke(this, EnabledCheckBox.IsChecked == true);

    public void UpdateEngagedStatus(bool isEngaged)
    {
        StatusTextBlock.Text = isEngaged
            ? "Kill Switch активен: интернет заблокирован до переподключения."
            : EnabledCheckBox.IsChecked == true
                ? "Kill Switch включён и сработает при следующем подключении."
                : string.Empty;
    }

    public void SetStatus(string? message)
    {
        if (!string.IsNullOrWhiteSpace(message))
            StatusTextBlock.Text = message;
    }
}
