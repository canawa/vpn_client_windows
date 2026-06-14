using System.Windows;
using System.Windows.Controls;

namespace CoffeeManiaVPN.Views;

public partial class SettingsView : UserControl
{
    public event EventHandler? OpenServersRequested;
    public event EventHandler? OpenConnectionRequested;
    public event EventHandler? OpenSubscriptionRequested;
    public event EventHandler? OpenLogsRequested;
    public event EventHandler? OpenAboutRequested;
    public event EventHandler? CloseAppRequested;

    public SettingsView()
    {
        InitializeComponent();
    }

    public void SetStatus(string message) => StatusTextBlock.Text = message;

    private void ServersButton_Click(object sender, RoutedEventArgs e) =>
        OpenServersRequested?.Invoke(this, EventArgs.Empty);

    private void ConnectionButton_Click(object sender, RoutedEventArgs e) =>
        OpenConnectionRequested?.Invoke(this, EventArgs.Empty);

    private void SubscriptionButton_Click(object sender, RoutedEventArgs e) =>
        OpenSubscriptionRequested?.Invoke(this, EventArgs.Empty);

    private void LogsButton_Click(object sender, RoutedEventArgs e) =>
        OpenLogsRequested?.Invoke(this, EventArgs.Empty);

    private void AboutButton_Click(object sender, RoutedEventArgs e) =>
        OpenAboutRequested?.Invoke(this, EventArgs.Empty);

    private void CloseAppButton_Click(object sender, RoutedEventArgs e) =>
        CloseAppRequested?.Invoke(this, EventArgs.Empty);
}
