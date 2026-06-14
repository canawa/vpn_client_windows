using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace CoffeeManiaVPN.Controls;

public enum UiIconKind
{
    Globe,
    Settings,
    Connection,
    Subscription,
    Theme,
    Logs,
    Info,
    Shield,
    Grid,
    Close
}

public partial class UiIcon : UserControl
{
    public static readonly DependencyProperty KindProperty =
        DependencyProperty.Register(
            nameof(Kind),
            typeof(UiIconKind),
            typeof(UiIcon),
            new PropertyMetadata(UiIconKind.Globe, OnIconPropertyChanged));

    public static readonly DependencyProperty IconBrushProperty =
        DependencyProperty.Register(
            nameof(IconBrush),
            typeof(Brush),
            typeof(UiIcon),
            new PropertyMetadata(Brushes.White));

    public static readonly DependencyProperty IconSizeProperty =
        DependencyProperty.Register(
            nameof(IconSize),
            typeof(double),
            typeof(UiIcon),
            new PropertyMetadata(20.0));

    public UiIconKind Kind
    {
        get => (UiIconKind)GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    public Brush IconBrush
    {
        get => (Brush)GetValue(IconBrushProperty);
        set => SetValue(IconBrushProperty, value);
    }

    public double IconSize
    {
        get => (double)GetValue(IconSizeProperty);
        set => SetValue(IconSizeProperty, value);
    }

    public UiIcon()
    {
        InitializeComponent();
        Loaded += (_, _) => UpdateIcon();
    }

    private static void OnIconPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is UiIcon icon)
            icon.UpdateIcon();
    }

    private void UpdateIcon()
    {
        var key = Kind switch
        {
            UiIconKind.Globe => "IconGlobe",
            UiIconKind.Settings => "IconSettings",
            UiIconKind.Connection => "IconConnection",
            UiIconKind.Subscription => "IconLink",
            UiIconKind.Theme => "IconTheme",
            UiIconKind.Logs => "IconLogs",
            UiIconKind.Info => "IconInfo",
            UiIconKind.Shield => "IconShield",
            UiIconKind.Grid => "IconGrid",
            UiIconKind.Close => "IconClose",
            _ => "IconGlobe"
        };

        IconPath.Data = TryFindResource(key) as Geometry;
    }
}
