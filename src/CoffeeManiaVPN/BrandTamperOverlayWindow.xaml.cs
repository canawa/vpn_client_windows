using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace CoffeeManiaVPN;

public partial class BrandTamperOverlayWindow : Window
{
    private readonly MediaPlayer _notificationPlayer = new();
    private bool _notificationPlayed;
    private bool _allowClose;

    public BrandTamperOverlayWindow(string message)
    {
        InitializeComponent();
        MessageTextBlock.Text = message;
        Loaded += OnLoaded;
        Closing += (_, e) =>
        {
            if (!_allowClose)
                e.Cancel = true;
        };
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Left = SystemParameters.VirtualScreenLeft;
        Top = SystemParameters.VirtualScreenTop;
        Width = SystemParameters.VirtualScreenWidth;
        Height = SystemParameters.VirtualScreenHeight;
        WindowState = WindowState.Normal;
        Activate();
        Focus();
        PlayNotificationSoundOnce();
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (!IsAltNine(e))
            return;

        _allowClose = true;
        Close();
        e.Handled = true;
    }

    private static bool IsAltNine(KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        return key == Key.D9 && (Keyboard.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt;
    }

    private void PlayNotificationSoundOnce()
    {
        if (_notificationPlayed)
            return;

        _notificationPlayed = true;

        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Views", "notification_sound.mp3");
            if (!File.Exists(path))
                return;

            _notificationPlayer.Open(new Uri(path, UriKind.Absolute));
            _notificationPlayer.Play();
        }
        catch
        {
            // Ignore playback failures.
        }
    }
}
