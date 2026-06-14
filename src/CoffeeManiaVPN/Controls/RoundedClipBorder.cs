using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace CoffeeManiaVPN.Controls;

public class RoundedClipBorder : Border
{
    public RoundedClipBorder()
    {
        UseLayoutRounding = true;
        SizeChanged += (_, _) => UpdateMask();
    }

    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);

        if (e.Property == CornerRadiusProperty)
            UpdateMask();
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
