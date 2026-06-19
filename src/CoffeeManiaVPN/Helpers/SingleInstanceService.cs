using System.IO;
using System.IO.Pipes;
using System.Text;

namespace CoffeeManiaVPN.Helpers;

public sealed class SingleInstanceService : IDisposable
{
    public const string ActivateMessage = "__activate__";

    private const string MutexName = "Global\\CoffeeManiaVPN_SingleInstance";
    private const string PipeName = "CoffeeManiaVPN_UrlPipe";

    private readonly Mutex _mutex;
    private readonly bool _isPrimary;
    private CancellationTokenSource? _listenCts;

    private SingleInstanceService(Mutex mutex, bool isPrimary)
    {
        _mutex = mutex;
        _isPrimary = isPrimary;
    }

    public bool IsPrimary => _isPrimary;

    public static SingleInstanceService Create()
    {
        var mutex = new Mutex(true, MutexName, out var createdNew);
        return new SingleInstanceService(mutex, createdNew);
    }

    public void StartListening(Action<string> onMessage)
    {
        if (!_isPrimary)
            return;

        _listenCts = new CancellationTokenSource();
        var token = _listenCts.Token;

        _ = Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                await using var server = new NamedPipeServerStream(
                    PipeName,
                    PipeDirection.In,
                    1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);

                try
                {
                    await server.WaitForConnectionAsync(token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                string message;
                using (var reader = new StreamReader(server, Encoding.UTF8, leaveOpen: false))
                    message = await reader.ReadToEndAsync(token);

                if (!string.IsNullOrWhiteSpace(message))
                    onMessage(message.Trim());
            }
        }, token);
    }

    public bool TrySendToPrimary(string message)
    {
        try
        {
            using var client = new NamedPipeClientStream(
                ".",
                PipeName,
                PipeDirection.Out,
                PipeOptions.None,
                System.Security.Principal.TokenImpersonationLevel.Impersonation);

            client.Connect(3000);
            using var writer = new StreamWriter(client, Encoding.UTF8, leaveOpen: false) { AutoFlush = true };
            writer.Write(message);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static bool TryForwardToRunningInstance(string message)
    {
        try
        {
            using var client = new NamedPipeClientStream(
                ".",
                PipeName,
                PipeDirection.Out,
                PipeOptions.None,
                System.Security.Principal.TokenImpersonationLevel.Impersonation);

            client.Connect(500);
            using var writer = new StreamWriter(client, Encoding.UTF8, leaveOpen: false) { AutoFlush = true };
            writer.Write(message);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public void Dispose()
    {
        _listenCts?.Cancel();
        _listenCts?.Dispose();

        if (_isPrimary)
            _mutex.ReleaseMutex();

        _mutex.Dispose();
    }
}
