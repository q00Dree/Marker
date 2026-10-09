using System.Net;

namespace Marker.Server.Networking;

public sealed class MarkingServerOptions
{
    public IPAddress Address { get; set; } = null!;
    public int Port { get; set; }
    public TimeSpan GenerationDelay { get; set; }

    internal IPEndPoint ToEndPoint()
    {
        if (Address is null)
            throw new InvalidOperationException($"{nameof(Address)} is not set.");
        if (Port is < IPEndPoint.MinPort or > IPEndPoint.MaxPort)
            throw new InvalidOperationException($"{nameof(Port)} is out of range.");
        if (GenerationDelay <= TimeSpan.Zero)
            throw new InvalidOperationException($"{nameof(GenerationDelay)} must be positive.");

        return new IPEndPoint(Address, Port);
    }
}