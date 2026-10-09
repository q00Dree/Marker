using Marker.Common.Protocol;
using System.Net.Sockets;
using System.Threading.Channels;

namespace Marker.Client.Networking;

public sealed class MarkingClient : IMarkingClient
{
    public ChannelReader<string> Codes => _codes.Reader;

    public event Action? Connected;
    public event Action<Exception?>? Disconnected;
    public event Action<Exception>? ConnectFailed;
    public event Action? RetriesExhausted;

    private readonly IMarkingCodeProtocol _protocol;
    private readonly MarkingClientOptions _options;
    private readonly Channel<string> _codes;

    private readonly CancellationTokenSource _shutdown;
    private Task _reconnectLoop = Task.CompletedTask;
    private int _started;

    internal MarkingClient(MarkingClientOptions options, IMarkingCodeProtocol protocol)
    {
        _protocol = protocol;
        _options = options;
        _codes = Channel.CreateBounded<string>(new BoundedChannelOptions(options.BufferCapacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleWriter = true
        });
        _shutdown = new CancellationTokenSource();
    }

    public void Start()
    {
        ObjectDisposedException.ThrowIf(_shutdown.IsCancellationRequested, this);
        if (Interlocked.Exchange(ref _started, 1) == 1)
            throw new InvalidOperationException("Client is already started.");

        _reconnectLoop = ReconnectLoopAsync(_shutdown.Token);
    }

    private async Task ReconnectLoopAsync(CancellationToken ct)
    {
        var attempt = 0;
        try
        {
            while (true)
            {
                if (await RunSessionAsync(ct))
                    attempt = 0;

                if (_options.Resilience.GetDelay(++attempt) is not { } delay)
                {
                    RetriesExhausted?.Invoke();
                    return;
                }

                await Task.Delay(delay, ct);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
        finally
        {
            _codes.Writer.TryComplete();
        }
    }

    private async Task<bool> RunSessionAsync(CancellationToken ct)
    {
        using var client = new TcpClient { NoDelay = true };

        try
        {
            await client.ConnectAsync(_options.Host!, _options.Port, ct);
        }
        catch (SocketException ex)
        {
            ConnectFailed?.Invoke(ex);
            return false;
        }

        Connected?.Invoke();

        Exception? reason = null;
        try
        {
            await using var reader = _protocol.CreateReader(client.GetStream());
            while (await reader.ReadAsync(ct) is { } code)
                await _codes.Writer.WriteAsync(code, ct);
        }
        catch (Exception ex) when (!(ex is OperationCanceledException && ct.IsCancellationRequested))
        {
            reason = ex;
        }
        finally
        {
            Disconnected?.Invoke(reason);
        }

        return true;
    }

    public async ValueTask DisposeAsync()
    {
        if (_shutdown.IsCancellationRequested) return;

        await _shutdown.CancelAsync();
        await _reconnectLoop;
        _shutdown.Dispose();

        Connected = null;
        Disconnected = null;
        ConnectFailed = null;
        RetriesExhausted = null;
    }
}
