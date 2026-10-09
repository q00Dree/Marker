using Marker.Common.Protocol;
using Marker.Server.Generation;
using System.Collections.Concurrent;
using System.Net.Sockets;

namespace Marker.Server.Networking;

public sealed class MarkingServer : IMarkingServer
{
    public event Action<Exception>? ConnectionFaulted;

    private readonly IMarkingCodeProtocol _protocol;
    private readonly IMarkingCodeGenerator _generator;
    private readonly MarkingServerOptions _options;

    private readonly TcpListener _listener;
    private readonly CancellationTokenSource _shutdown;
    private readonly ConcurrentDictionary<Task, byte> _connections;
    private Task _acceptLoop = Task.CompletedTask;
    private int _started;

    internal MarkingServer(MarkingServerOptions options, IMarkingCodeProtocol protocol, IMarkingCodeGenerator generator)
    {
        _protocol = protocol;
        _generator = generator;
        _options = options;

        _listener = new TcpListener(_options.ToEndPoint());
        _shutdown = new CancellationTokenSource();
        _connections = new ConcurrentDictionary<Task, byte>();
    }

    public void Start()
    {
        ObjectDisposedException.ThrowIf(_shutdown.IsCancellationRequested, this);
        if (Interlocked.Exchange(ref _started, 1) == 1)
            throw new InvalidOperationException("Server is already started.");

        _listener.Start();
        _acceptLoop = AcceptLoopAsync(_shutdown.Token);
    }

    private async Task AcceptLoopAsync(CancellationToken ct)
    {
        try
        {
            while (true)
            {
                var client = await _listener.AcceptTcpClientAsync(ct);
                Track(ServeAsync(client, ct));
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
    }

    private async Task ServeAsync(TcpClient client, CancellationToken ct)
    {
        using (client)
        {
            client.NoDelay = true;

            await using var writer = _protocol.CreateWriter(client.GetStream());

            try
            {
                using var timer = new PeriodicTimer(_options.GenerationDelay);
                do
                {
                    await writer.WriteAsync(_generator.Generate(), ct);
                }
                while (await timer.WaitForNextTickAsync(ct));
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
            }
            catch (Exception ex) when (ex is IOException or SocketException)
            {
            }
            catch (Exception ex)
            {
                ConnectionFaulted?.Invoke(ex);
            }
        }
    }

    private void Track(Task connection)
    {
        _connections.TryAdd(connection, 0);
        connection.ContinueWith(
            t => _connections.TryRemove(t, out _),
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    public async ValueTask DisposeAsync()
    {
        if (_shutdown.IsCancellationRequested) return;

        await _shutdown.CancelAsync();
        _listener.Stop();
        await _acceptLoop;
        await Task.WhenAll(_connections.Keys);
        _shutdown.Dispose();

        ConnectionFaulted = null;
    }
}