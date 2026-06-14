using System.Windows;
using System.Windows.Controls;

namespace CoffeeManiaVPN.Views;

public partial class SubscriptionSettingsView : UserControl
{
    public event EventHandler? RefreshRequested;

    public SubscriptionSettingsView()
    {
        InitializeComponent();
    }

    public string SubscriptionUrl
    {
        get => SubscriptionUrlTextBox.Text;
        set => SubscriptionUrlTextBox.Text = value;
    }

    public void SetStatus(string message) => StatusTextBlock.Text = message;

    private void RefreshButton_Click(object sender, RoutedEventArgs e) =>
        RefreshRequested?.Invoke(this, EventArgs.Empty);
}
