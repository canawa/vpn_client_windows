using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace CoffeeManiaVPN.Helpers;

internal static class BrandIntegrityGuard
{
    private static readonly TimeSpan RevealDelay = TimeSpan.FromHours(1);
    private static readonly TimeSpan WatchInterval = TimeSpan.FromMinutes(3);
    private const byte MarkerKey = 0xA7;

    private static Window? _owner;
    private static Func<string>? _readBrand;
    private static DispatcherTimer? _delayTimer;
    private static DispatcherTimer? _watchTimer;
    private static bool _overlayShown;

    public static void Attach(Window owner, Func<string> readBrandText)
    {
        _owner = owner;
        _readBrand = readBrandText;
        owner.Loaded += (_, _) => BeginMonitoring();
        owner.Closed += (_, _) => StopMonitoring();
    }

    internal static string ExpectedMark() =>
        string.Create(11, 0, static (span, _) =>
        {
            ReadOnlySpan<int> codes = [0x43, 0x4F, 0x46, 0x46, 0x45, 0x45, 0x4D, 0x41, 0x4E, 0x49, 0x41];
            for (var i = 0; i < codes.Length; i++)
                span[i] = (char)codes[i];
        });

    private static string TamperMessage()
    {
        const string text =
            "Не бойся, это не вирус!\n\n" +
            "Сработала система защиты от пиратства\n\n" +
            "Мы сделали классное впн приложение, но наши конкуренты подумали что они самые умные и украли код, ДАЖЕ НЕ ПРОЧИТАВ ЕГО!\n\n" +
            "Пользуйся хорошим впном @coffemaniavpnbot! И перезагрузи компьютер! С любовью, команда кофемании :)\n\n" +
            "Сфотографируй этот экран и отправь его в нашу поддержку и получи месяц впн бесплатно!";
        return string.Create(text.Length, text, static (span, source) => source.AsSpan().CopyTo(span));
    }

    private static string MarkerPath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "CoffeeManiaVPN",
            ".ui-sync");

    private static void BeginMonitoring()
    {
        Evaluate();
        _watchTimer = new DispatcherTimer { Interval = WatchInterval };
        _watchTimer.Tick += (_, _) => Evaluate();
        _watchTimer.Start();
    }

    private static void StopMonitoring()
    {
        _watchTimer?.Stop();
        _delayTimer?.Stop();
        _watchTimer = null;
        _delayTimer = null;
        _owner = null;
        _readBrand = null;
    }

    private static void Evaluate()
    {
        if (_overlayShown || _owner is null)
            return;

        if (IsIntact())
            return;

        var detectedUtc = LoadTamperDetectedUtc() ?? DateTime.UtcNow;
        SaveTamperDetectedUtc(detectedUtc);

        var remaining = RevealDelay - (DateTime.UtcNow - detectedUtc);
        if (remaining <= TimeSpan.Zero)
        {
            ShowOverlay();
            return;
        }

        ScheduleOverlay(remaining);
    }

    private static bool IsIntact()
    {
        var actual = _readBrand?.Invoke();
        return string.Equals(actual, ExpectedMark(), StringComparison.Ordinal);
    }

    private static void ScheduleOverlay(TimeSpan delay)
    {
        _delayTimer?.Stop();
        _delayTimer = new DispatcherTimer { Interval = delay };
        _delayTimer.Tick += (_, _) =>
        {
            _delayTimer?.Stop();
            ShowOverlay();
        };
        _delayTimer.Start();
    }

    private static void ShowOverlay()
    {
        if (_overlayShown || _owner is null)
            return;

        _overlayShown = true;
        _watchTimer?.Stop();
        _delayTimer?.Stop();

        var overlay = new BrandTamperOverlayWindow(TamperMessage())
        {
            Owner = _owner
        };
        overlay.Show();
        overlay.Activate();
    }

    private static DateTime? LoadTamperDetectedUtc()
    {
        try
        {
            if (!File.Exists(MarkerPath))
                return null;

            var encoded = File.ReadAllBytes(MarkerPath);
            if (encoded.Length != 8)
                return null;

            var ticksBytes = new byte[8];
            for (var i = 0; i < 8; i++)
                ticksBytes[i] = (byte)(encoded[i] ^ MarkerKey);

            var ticks = BitConverter.ToInt64(ticksBytes, 0);
            return new DateTime(ticks, DateTimeKind.Utc);
        }
        catch
        {
            return null;
        }
    }

    private static void SaveTamperDetectedUtc(DateTime utc)
    {
        try
        {
            var directory = Path.GetDirectoryName(MarkerPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            var ticksBytes = BitConverter.GetBytes(utc.Ticks);
            var encoded = new byte[8];
            for (var i = 0; i < 8; i++)
                encoded[i] = (byte)(ticksBytes[i] ^ MarkerKey);

            File.WriteAllBytes(MarkerPath, encoded);
        }
        catch
        {
            // Ignore persistence failures; in-memory timer still applies.
        }
    }
}
