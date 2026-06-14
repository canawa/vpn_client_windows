using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace CoffeeManiaVPN.Views;

public partial class LogsView : UserControl
{
    private readonly StringBuilder _logs = new();
    private const int MaxLength = 12000;

    public LogsView()
    {
        InitializeComponent();
    }

    public void AppendLog(string message)
    {
        if (_logs.Length > MaxLength)
            _logs.Clear();

        _logs.AppendLine(message);
        LogsTextBlock.Text = _logs.ToString();
        LogsScrollViewer.ScrollToEnd();
    }

    public void ClearLogs()
    {
        _logs.Clear();
        LogsTextBlock.Text = string.Empty;
    }

    private void ClearButton_Click(object sender, RoutedEventArgs e) => ClearLogs();
}
