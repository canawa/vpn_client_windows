using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using CoffeeManiaVPN.Helpers;

namespace CoffeeManiaVPN.Controls;

public partial class EmojiTextBlock : TextBlock
{
    public static readonly DependencyProperty TextBrushProperty =
        DependencyProperty.Register(
            nameof(TextBrush),
            typeof(Brush),
            typeof(EmojiTextBlock),
            new PropertyMetadata(Brushes.White, OnTextOrBrushChanged));

    public static readonly DependencyProperty SourceTextProperty =
        DependencyProperty.Register(
            nameof(SourceText),
            typeof(string),
            typeof(EmojiTextBlock),
            new PropertyMetadata(string.Empty, OnTextOrBrushChanged));

    public string SourceText
    {
        get => (string)GetValue(SourceTextProperty);
        set => SetValue(SourceTextProperty, value);
    }

    public Brush TextBrush
    {
        get => (Brush)GetValue(TextBrushProperty);
        set => SetValue(TextBrushProperty, value);
    }

    public EmojiTextBlock()
    {
        InitializeComponent();
        FontFamily = new FontFamily("Segoe UI");
        Loaded += (_, _) => EmojiImageHelper.EmojiCached += OnEmojiCached;
        Unloaded += (_, _) => EmojiImageHelper.EmojiCached -= OnEmojiCached;
    }

    private void OnEmojiCached(object? sender, string emoji)
    {
        if (string.IsNullOrEmpty(SourceText) || !SourceText.Contains(emoji, StringComparison.Ordinal))
            return;

        Dispatcher.Invoke(RebuildInlines);
    }

    private static void OnTextOrBrushChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is EmojiTextBlock block)
            block.RebuildInlines();
    }

    private void RebuildInlines()
    {
        Inlines.Clear();

        var text = SourceText ?? string.Empty;
        if (text.Length == 0)
            return;

        foreach (var (segment, isEmoji) in Segment(text))
        {
            if (isEmoji)
            {
                AddEmojiInline(segment);
                continue;
            }

            var run = new Run(segment) { Foreground = TextBrush };
            Inlines.Add(run);
        }
    }

    private void AddEmojiInline(string emoji)
    {
        var size = Math.Max(14, FontSize * 1.1);
        var source = EmojiImageHelper.TryGetImage(emoji);

        if (source is null)
        {
            Inlines.Add(new Run(emoji)
            {
                FontFamily = new FontFamily("Segoe UI Emoji"),
                FontSize = size
            });
            return;
        }

        var image = new Image
        {
            Source = source,
            Width = size,
            Height = size,
            Stretch = Stretch.Uniform,
            VerticalAlignment = VerticalAlignment.Center
        };
        RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);

        Inlines.Add(new InlineUIContainer(image)
        {
            BaselineAlignment = BaselineAlignment.Center
        });
    }

    private static IEnumerable<(string Text, bool IsEmoji)> Segment(string text)
    {
        var buffer = new StringBuilder();
        bool? isEmoji = null;
        var index = 0;

        while (index < text.Length)
        {
            var codePoint = char.ConvertToUtf32(text, index);
            var emoji = IsEmojiCodePoint(codePoint);
            var length = char.IsSurrogatePair(text, index) ? 2 : 1;
            var chunk = text.Substring(index, length);

            if (isEmoji.HasValue && isEmoji.Value != emoji)
            {
                yield return (buffer.ToString(), isEmoji.Value);
                buffer.Clear();
            }

            isEmoji = emoji;
            buffer.Append(chunk);
            index += length;
        }

        if (buffer.Length > 0 && isEmoji.HasValue)
            yield return (buffer.ToString(), isEmoji.Value);
    }

    private static bool IsEmojiCodePoint(int codePoint) =>
        codePoint is >= 0x1F300 and <= 0x1FAFF
            or >= 0x1F600 and <= 0x1F64F
            or >= 0x1F680 and <= 0x1F6FF
            or >= 0x1F900 and <= 0x1F9FF
            or >= 0x2600 and <= 0x26FF
            or >= 0x2700 and <= 0x27BF
            or >= 0x2300 and <= 0x23FF
            or 0xFE0F
            or 0x200D;
}
