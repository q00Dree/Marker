namespace Marker.Common.Protocol;

public interface IMarkingCodeProtocol
{
    IMarkingCodeReader CreateReader(Stream stream);
    IMarkingCodeWriter CreateWriter(Stream stream);
}