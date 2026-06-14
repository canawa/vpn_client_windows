using System.Windows;
using System.Windows.Controls;

namespace CoffeeManiaVPN.Views;

public partial class ConnectionSettingsView : UserControl
{
    public event EventHandler? OpenSiteSplitRequested;
    public event EventHandler? OpenAppSplitRequested;
    public event EventHandler? OpenKillSwitchRequested;

    public ConnectionSettingsView()
    {
        InitializeComponent();
    }

    private void SiteSplitButton_Click(object sender, RoutedEventArgs e) =>
        OpenSiteSplitRequested?.Invoke(this, EventArgs.Empty);

    private void AppSplitButton_Click(object sender, RoutedEventArgs e) =>
        OpenAppSplitRequested?.Invoke(this, EventArgs.Empty);

    private void KillSwitchButton_Click(object sender, RoutedEventArgs e) =>
        OpenKillSwitchRequested?.Invoke(this, EventArgs.Empty);
}
