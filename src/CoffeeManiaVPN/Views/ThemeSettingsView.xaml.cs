using System.Windows;
using System.Windows.Controls;
using CoffeeManiaVPN.Helpers;

namespace CoffeeManiaVPN.Views;

public partial class ThemeSettingsView : UserControl
{
    public event EventHandler<AppTheme>? ThemeChanged;
    public event EventHandler<bool>? CompactSidebarChanged;
    public event EventHandler<bool>? SimplifiedAnimationsChanged;

    public ThemeSettingsView()
    {
        InitializeComponent();
    }

    public void Load(AppTheme theme, bool compactSidebar, bool simplifiedAnimations)
    {
        SetSelectedTheme(theme, notify: false);
        CompactSidebarCheckBox.IsChecked = compactSidebar;
        SimplifiedAnimationsCheckBox.IsChecked = simplifiedAnimations;
    }

    private void DarkThemeButton_Click(object sender, RoutedEventArgs e) =>
        SetSelectedTheme(AppTheme.Dark);

    private void LightThemeButton_Click(object sender, RoutedEventArgs e) =>
        SetSelectedTheme(AppTheme.Light);

    private void CompactSidebarCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (CompactSidebarCheckBox.IsChecked is bool enabled)
            CompactSidebarChanged?.Invoke(this, enabled);
    }

    private void SimplifiedAnimationsCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (SimplifiedAnimationsCheckBox.IsChecked is bool enabled)
            SimplifiedAnimationsChanged?.Invoke(this, enabled);
    }

    private void SetSelectedTheme(AppTheme theme, bool notify = true)
    {
        var activeStyle = (Style)FindResource("SegmentButtonActiveStyle");
        var normalStyle = (Style)FindResource("SegmentButtonStyle");

        DarkThemeButton.Style = theme == AppTheme.Dark ? activeStyle : normalStyle;
        LightThemeButton.Style = theme == AppTheme.Light ? activeStyle : normalStyle;

        if (notify)
            ThemeChanged?.Invoke(this, theme);
    }
}
