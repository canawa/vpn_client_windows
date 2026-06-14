using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace CoffeeManiaVPN.Controls;

public partial class CountryFlag : UserControl
{
    public static readonly DependencyProperty CountryCodeProperty =
        DependencyProperty.Register(
            nameof(CountryCode),
            typeof(string),
            typeof(CountryFlag),
            new PropertyMetadata("un", OnCountryCodeChanged));

    public string CountryCode
    {
        get => (string)GetValue(CountryCodeProperty);
        set => SetValue(CountryCodeProperty, value);
    }

    public CountryFlag()
    {
        InitializeComponent();
        UpdateImage();
    }

    private static void OnCountryCodeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is CountryFlag flag)
            flag.UpdateImage();
    }

    private void UpdateImage()
    {
        var source = Helpers.CountryFlagHelper.GetImage(CountryCode);
        if (source is null)
        {
            FlagBorder.Background = (Brush)FindResource("SurfaceContainerHighestBrush");
            return;
        }

        FlagBorder.Background = new ImageBrush(source)
        {
            Stretch = Stretch.UniformToFill,
            AlignmentX = AlignmentX.Center,
            AlignmentY = AlignmentY.Center
        };
    }
}
