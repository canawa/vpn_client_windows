using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace CoffeeManiaVPN.Controls;

public partial class LogoMark : UserControl
{
    public static readonly DependencyProperty IconBrushProperty =
        DependencyProperty.Register(
            nameof(IconBrush),
            typeof(Brush),
            typeof(LogoMark),
            new PropertyMetadata(Brushes.White));

    public Brush IconBrush
    {
        get => (Brush)GetValue(IconBrushProperty);
        set => SetValue(IconBrushProperty, value);
    }

    public LogoMark()
    {
        InitializeComponent();
    }
}
