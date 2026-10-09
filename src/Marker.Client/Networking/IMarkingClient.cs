using System.Threading.Channels;

namespace Marker.Client.Networking;

public interface IMarkingClient : IAsyncDisposable
{
    /// <summary>
    /// Received codes, in arrival order, across reconnects. The buffer is bounded: while it is full the client
    /// stops reading from the socket, so a slow consumer slows the server down instead of losing codes.
    /// Each code goes to exactly one reader. The channel completes when the client is disposed
    /// or reconnect attempts are exhausted.
    /// </summary>
    ChannelReader<string> Codes { get; }

    event Action? Connected;
    event Action<Exception?>? Disconnected;
    event Action<Exception>? ConnectFailed;
    event Action? RetriesExhausted;

    void Start();
}