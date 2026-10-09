namespace Marker.Client.Networking;

public sealed class MarkingClientOptions
{
    public string? Host { get; set; }
    public int Port { get; set; }
    public int BufferCapacity { get; set; }
    public TimeSpan? IdleTimeout { get; set; }
    public ResilienceOptions Resilience { get; } = new();

    internal void Validate()
    {
        if (string.IsNullOrWhiteSpace(Host))
            throw new InvalidOperationException($"{nameof(Host)} is not set.");
        if (Port is < 1 or > 65535)
            throw new InvalidOperationException($"{nameof(Port)} is out of range.");
        if (BufferCapacity < 1)
            throw new InvalidOperationException($"{nameof(BufferCapacity)} must be positive.");

        if (IdleTimeout <= TimeSpan.Zero)
            throw new InvalidOperationException($"{nameof(IdleTimeout)} must be positive.");

        Resilience.Validate();
    }
}