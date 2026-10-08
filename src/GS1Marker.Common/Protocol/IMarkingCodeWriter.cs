namespace GS1Marker.Common.Protocol;

public interface IMarkingCodeWriter : IAsyncDisposable
{
    Task WriteAsync(string code, CancellationToken ct);
}