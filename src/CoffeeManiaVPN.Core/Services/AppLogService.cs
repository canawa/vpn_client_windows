using System.Text;

namespace CoffeeManiaVPN.Core.Services;

public sealed class AppLogService
{
    private const int MaxBufferLength = 200_000;
    private readonly StringBuilder _buffer = new();
    private readonly object _gate = new();

    public event EventHandler<string>? LineAppended;

    public static string LogDirectory =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "CoffeeManiaVPN",
            "logs");

    public string LogFilePath => Path.Combine(LogDirectory, "app.log");

    public void Append(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message.Trim()}";

        lock (_gate)
        {
            if (_buffer.Length > MaxBufferLength)
                _buffer.Clear();

            _buffer.AppendLine(line);
            WriteToFile(line);
        }

        LineAppended?.Invoke(this, line);
    }

    public string GetText()
    {
        lock (_gate)
            return _buffer.Length == 0 ? string.Empty : _buffer.ToString();
    }

    public bool HasLogs
    {
        get
        {
            lock (_gate)
                return _buffer.Length > 0;
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            _buffer.Clear();
            try
            {
                Directory.CreateDirectory(LogDirectory);
                File.WriteAllText(LogFilePath, string.Empty);
            }
            catch
            {
                // ignored
            }
        }

        LineAppended?.Invoke(this, string.Empty);
    }

    public void ExportTo(string path)
    {
        var content = GetText();
        File.WriteAllText(path, content, Encoding.UTF8);
    }

    public void LoadExisting()
    {
        try
        {
            if (!File.Exists(LogFilePath))
                return;

            var text = File.ReadAllText(LogFilePath);
            if (string.IsNullOrWhiteSpace(text))
                return;

            lock (_gate)
            {
                _buffer.Clear();
                _buffer.Append(text.TrimEnd());
                if (!_buffer.ToString().EndsWith(Environment.NewLine, StringComparison.Ordinal))
                    _buffer.AppendLine();
            }

            LineAppended?.Invoke(this, "__reload__");
        }
        catch
        {
            // ignored
        }
    }

    private void WriteToFile(string line)
    {
        try
        {
            Directory.CreateDirectory(LogDirectory);
            File.AppendAllText(LogFilePath, line + Environment.NewLine, Encoding.UTF8);
        }
        catch
        {
            // ignored
        }
    }
}
