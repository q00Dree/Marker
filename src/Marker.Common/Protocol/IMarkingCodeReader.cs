namespace Marker.Common.Protocol;

public interface IMarkingCodeReader : IDisposable
{
    Task<string?> ReadAsync(CancellationToken ct);
}