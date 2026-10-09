namespace Marker.Server.Networking;

public interface IMarkingServer : IAsyncDisposable
{
    void Start();
    event Action<Exception>? ConnectionFaulted;
}