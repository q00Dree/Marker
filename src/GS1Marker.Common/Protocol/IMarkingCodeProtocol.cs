namespace GS1Marker.Common.Protocol;

public interface IMarkingCodeProtocol
{
    IMarkingCodeReader CreateReader(Stream stream);
    IMarkingCodeWriter CreateWriter(Stream stream);
}