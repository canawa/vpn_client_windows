using System.Diagnostics;
using System.Text;
using CoffeeManiaVPN.Core.Models;
using CoffeeManiaVPN.Core.Services;

namespace CoffeeManiaVPN.Core.Xray;

public sealed class XrayRunner : IDisposable
{
    private const int StopTimeoutMs = 5000;
    private const int AdapterReleaseDelayMs = 1200;
    private const int StartupVerifyDelayMs = 800;

    private readonly SemaphoreSlim _operationLock = new(1, 1);
    private Process? _process;
    private string? _configPath;
    private readonly StringBuilder _recentLogs = new();

    public bool IsRunning => _process is { HasExited: false };

    public event EventHandler<string>? LogReceived;
    public event EventHandler<int>? Exited;

    public async Task StartAsync(
        ProxyNode node,
        string xrayDirectory,
        AppSettings? settings = null,
        CancellationToken cancellationToken = default)
    {
        await _operationLock.WaitAsync(cancellationToken);
        try
        {
            await KillOrphanXrayProcessesAsync(xrayDirectory, cancellationToken);
            await StopInternalAsync(cancellationToken);
            await Task.Delay(AdapterReleaseDelayMs, cancellationToken);

            if (node.IsPlaceholder)
                throw new InvalidOperationException("Выберите рабочий сервер из списка.");

            if (!Directory.Exists(xrayDirectory))
                throw new DirectoryNotFoundException($"Не найдена папка Xray: {xrayDirectory}");

            var xrayExe = Path.Combine(xrayDirectory, "xray.exe");
            if (!File.Exists(xrayExe))
                throw new FileNotFoundException("Не найден xray.exe.", xrayExe);

            Directory.CreateDirectory(Path.Combine(xrayDirectory, "configs"));
            _configPath = Path.Combine(xrayDirectory, "configs", "active.json");

            var rawConfig = node.HasFullConfig
                ? node.XrayConfigJson!
                : XrayConfigBuilder.Build(node, xrayDirectory);
            var config = XrayTunConfigurator.Apply(rawConfig, settings);
            File.WriteAllText(_configPath, config);

            _recentLogs.Clear();
            Exception? lastError = null;

            for (var attempt = 1; attempt <= 3; attempt++)
            {
                if (attempt > 1)
                    await Task.Delay(AdapterReleaseDelayMs, cancellationToken);

                try
                {
                    StartProcess(xrayExe, _configPath);
                    await Task.Delay(StartupVerifyDelayMs, cancellationToken);

                    if (_process is { HasExited: false })
                        return;

                    lastError = new InvalidOperationException(GetStartupErrorMessage(_process?.ExitCode));
                }
                catch (Exception ex)
                {
                    lastError = ex;
                }

                await StopInternalAsync(cancellationToken);
            }

            throw lastError ?? new InvalidOperationException("Не удалось запустить Xray.");
        }
        finally
        {
            _operationLock.Release();
        }
    }

    public void Start(ProxyNode node, string xrayDirectory, AppSettings? settings = null) =>
        StartAsync(node, xrayDirectory, settings).GetAwaiter().GetResult();

    private void StartProcess(string xrayExe, string configPath)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = xrayExe,
            Arguments = $"run -c \"{configPath}\"",
            WorkingDirectory = Path.GetDirectoryName(xrayExe)!,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        _process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        _process.OutputDataReceived += OnLogData;
        _process.ErrorDataReceived += OnLogData;
        _process.Exited += (_, _) => Exited?.Invoke(this, _process?.ExitCode ?? -1);

        if (!_process.Start())
            throw new InvalidOperationException("Не удалось запустить xray.exe.");

        _process.BeginOutputReadLine();
        _process.BeginErrorReadLine();
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        await _operationLock.WaitAsync(cancellationToken);
        try
        {
            await StopInternalAsync(cancellationToken);
        }
        finally
        {
            _operationLock.Release();
        }
    }

    public void Stop() => StopAsync().GetAwaiter().GetResult();

    private async Task StopInternalAsync(CancellationToken cancellationToken = default)
    {
        if (_process is null)
            return;

        var process = _process;
        _process = null;

        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(StopTimeoutMs);
                await process.WaitForExitAsync(cts.Token);
            }
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch
        {
            // ignored
        }
        finally
        {
            process.Dispose();
            await Task.Delay(AdapterReleaseDelayMs, cancellationToken);
        }
    }

    private void OnLogData(object sender, DataReceivedEventArgs args)
    {
        if (string.IsNullOrWhiteSpace(args.Data))
            return;

        AppendLog(args.Data);
        LogReceived?.Invoke(this, args.Data);
    }

    private void AppendLog(string line)
    {
        if (_recentLogs.Length > 4000)
            _recentLogs.Clear();

        _recentLogs.AppendLine(line);
    }

    private string GetStartupErrorMessage(int? exitCode)
    {
        var logs = _recentLogs.ToString();
        if (logs.Contains("already exists", StringComparison.OrdinalIgnoreCase) ||
            logs.Contains("0x800700B7", StringComparison.OrdinalIgnoreCase))
        {
            return "TUN-адаптер занят. Подождите пару секунд и попробуйте снова.";
        }

        if (!string.IsNullOrWhiteSpace(logs))
        {
            var lastLine = logs.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
            if (!string.IsNullOrWhiteSpace(lastLine))
                return lastLine;
        }

        return exitCode is null or 0
            ? "Xray завершился сразу после запуска."
            : $"Xray завершился с кодом {exitCode}.";
    }

    private static async Task KillOrphanXrayProcessesAsync(string xrayDirectory, CancellationToken cancellationToken)
    {
        var xrayExe = Path.GetFullPath(Path.Combine(xrayDirectory, "xray.exe"));

        foreach (var process in Process.GetProcessesByName("xray"))
        {
            try
            {
                if (process.HasExited)
                    continue;

                var processPath = process.MainModule?.FileName;
                if (processPath is null ||
                    !string.Equals(Path.GetFullPath(processPath), xrayExe, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync(cancellationToken);
            }
            catch
            {
                // ignored
            }
            finally
            {
                process.Dispose();
            }
        }
    }

    public void Dispose()
    {
        Stop();

        if (_configPath is not null && File.Exists(_configPath))
        {
            try
            {
                File.Delete(_configPath);
            }
            catch
            {
                // ignored
            }
        }

        _operationLock.Dispose();
    }
}
