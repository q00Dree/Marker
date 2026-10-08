namespace Marker.Common.Protocol;

public interface IMarkingCodeReader : IAsyncDisposable
{
    Task<string?> ReadAsync(CancellationToken ct);
}