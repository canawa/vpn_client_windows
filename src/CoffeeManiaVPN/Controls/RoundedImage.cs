using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace CoffeeManiaVPN.Controls;

public class RoundedImage : Image
{
    public static readonly DependencyProperty CornerRadiusProperty =
        DependencyProperty.Register(
            nameof(CornerRadius),
            typeof(CornerRadius),
            typeof(RoundedImage),
            new PropertyMetadata(new CornerRadius(24), OnCornerRadiusChanged));

    public CornerRadius CornerRadius
    {
        get => (CornerRadius)GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    public RoundedImage()
    {
        UseLayoutRounding = true;
        SizeChanged += (_, _) => UpdateMask();
    }

    private static void OnCornerRadiusChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is RoundedImage image)
            image.UpdateMask();
    }

    private void UpdateMask()
    {
        if (ActualWidth <= 0 || ActualHeight <= 0)
            return;

        var radius = GetEffectiveRadius();
        var width = ActualWidth;
        var height = ActualHeight;

        OpacityMask = new VisualBrush
        {
            Stretch = Stretch.Fill,
            ViewboxUnits = BrushMappingMode.Absolute,
            ViewportUnits = BrushMappingMode.Absolute,
            Viewbox = new Rect(0, 0, width, height),
            Viewport = new Rect(0, 0, width, height),
            Visual = new Border
            {
                Background = Brushes.Black,
                Width = width,
                Height = height,
                CornerRadius = new CornerRadius(radius)
            }
        };
    }

    private double GetEffectiveRadius()
    {
        var requested = CornerRadius.TopLeft;
        var maxRadius = Math.Min(ActualWidth, ActualHeight) / 2.0;
        return Math.Min(requested, maxRadius);
    }
}
