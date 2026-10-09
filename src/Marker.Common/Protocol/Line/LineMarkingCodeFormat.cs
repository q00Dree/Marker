using System.Text;

namespace Marker.Common.Protocol.Line;

internal static class LineMarkingCodeFormat
{
    public const char Delimiter = '\n';
    public const char OptionalCarriageReturn = '\r';
    public const int DefaultMaxLineBytes = 1024;

    public static readonly Encoding Encoding = 
        new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
}