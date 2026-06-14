using System.Windows;
using System.Windows.Controls;
using CoffeeManiaVPN.Core.Services;
using Microsoft.Win32;

namespace CoffeeManiaVPN.Views;

public partial class LogsView : UserControl
{
    private readonly AppLogService _logService;

    public LogsView(AppLogService logService)
    {
        _logService = logService;
        InitializeComponent();
        _logService.LineAppended += OnLineAppended;
        RefreshDisplay();
    }

    public LogsView() : this(new AppLogService())
    {
    }

    private void OnLineAppended(object? sender, string line)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(() => OnLineAppended(sender, line));
            return;
        }

        RefreshDisplay();
        if (!string.IsNullOrEmpty(line))
            LogsScrollViewer.ScrollToEnd();
    }

    private void RefreshDisplay()
    {
        var text = _logService.GetText();
        LogsTextBlock.Text = text;
        EmptyTextBlock.Visibility = string.IsNullOrWhiteSpace(text)
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        _logService.Clear();
        RefreshDisplay();
    }

    private void DownloadButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_logService.HasLogs)
        {
            MessageBox.Show(
                "Нет логов для сохранения.",
                "Кофемания ВПН",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var dialog = new SaveFileDialog
        {
            Title = "Сохранить логи",
            Filter = "Текстовые файлы (*.txt)|*.txt|Все файлы (*.*)|*.*",
            FileName = $"CoffeeManiaVPN_{DateTime.Now:yyyyMMdd_HHmmss}.txt",
            AddExtension = true,
            DefaultExt = ".txt"
        };

        if (dialog.ShowDialog() != true)
            return;

        try
        {
            _logService.ExportTo(dialog.FileName);
            MessageBox.Show(
                $"Логи сохранены:\n{dialog.FileName}",
                "Кофемания ВПН",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Не удалось сохранить логи:\n{ex.Message}",
                "Кофемания ВПН",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }
}
