namespace GS1Marker.Common.Protocol;

public interface IMarkingCodeWriter : IDisposable
{
    Task WriteAsync(string code, CancellationToken ct);
}