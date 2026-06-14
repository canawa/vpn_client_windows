using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace CoffeeManiaVPN.Converters;

public sealed class BoolToBrushConverter : IValueConverter
{
    public Brush ActiveBrush { get; set; } = Brushes.LimeGreen;
    public Brush InactiveBrush { get; set; } = new SolidColorBrush(Color.FromArgb(0x33, 0x4A, 0x40, 0x3C));

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? ActiveBrush : InactiveBrush;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
