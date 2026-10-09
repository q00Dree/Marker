using System.Threading.Channels;

namespace Marker.Client.Networking;

public interface IMarkingClient : IAsyncDisposable
{
    ChannelReader<string> Codes { get; }

    event Action? Connected;
    event Action<Exception?>? Disconnected;
    event Action<Exception>? ConnectFailed;
    event Action? RetriesExhausted;

    void Start();
}