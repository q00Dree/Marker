using System.Text;

namespace Marker.Common.Protocol.Line;

internal static class LineMarkingCodeFormat
{
    public const string Delimiter = "\n";

    public static readonly Encoding Encoding = 
        new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
}