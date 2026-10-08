namespace GS1Marker.Common.Protocol.Line;

public sealed class LineMarkingCodeProtocol : IMarkingCodeProtocol
{
    public IMarkingCodeReader CreateReader(Stream stream) => new LineMarkingCodeReader(stream);
    public IMarkingCodeWriter CreateWriter(Stream stream) => new LineMarkingCodeWriter(stream);
}