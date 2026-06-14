using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CoffeeManiaVPN.Helpers;

namespace CoffeeManiaVPN.Controls;

public partial class EmojiIcon : UserControl
{
    public static readonly DependencyProperty EmojiProperty =
        DependencyProperty.Register(
            nameof(Emoji),
            typeof(string),
            typeof(EmojiIcon),
            new PropertyMetadata(string.Empty, OnEmojiChanged));

    public static readonly DependencyProperty IconSizeProperty =
        DependencyProperty.Register(
            nameof(IconSize),
            typeof(double),
            typeof(EmojiIcon),
            new PropertyMetadata(18.0, OnEmojiChanged));

    public string Emoji
    {
        get => (string)GetValue(EmojiProperty);
        set => SetValue(EmojiProperty, value);
    }

    public double IconSize
    {
        get => (double)GetValue(IconSizeProperty);
        set => SetValue(IconSizeProperty, value);
    }

    public EmojiIcon()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        EmojiImageHelper.EmojiCached += OnEmojiCached;
        UpdateImage();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e) =>
        EmojiImageHelper.EmojiCached -= OnEmojiCached;

    private void OnEmojiCached(object? sender, string emoji)
    {
        if (!string.Equals(emoji, Emoji, StringComparison.Ordinal))
            return;

        Dispatcher.Invoke(UpdateImage);
    }

    private static void OnEmojiChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is EmojiIcon icon)
            icon.UpdateImage();
    }

    private void UpdateImage()
    {
        Width = IconSize;
        Height = IconSize;
        EmojiImage.Width = IconSize;
        EmojiImage.Height = IconSize;

        if (string.IsNullOrWhiteSpace(Emoji))
        {
            EmojiImage.Source = null;
            return;
        }

        EmojiImage.Source = EmojiImageHelper.TryGetImage(Emoji);
    }
}
