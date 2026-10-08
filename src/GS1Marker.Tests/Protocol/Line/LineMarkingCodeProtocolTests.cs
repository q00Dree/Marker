using GS1Marker.Common.Protocol.Line;
using Xunit;

namespace GS1Marker.Tests.Protocol.Line;

public class LineMarkingCodeProtocolTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    [Fact]
    public async Task CreatedWriterAndReader_RoundTrip_ReturnSameCodes()
    {
        var codes = new[] { "0104601234567893215AbCdEfGhIjK", "0100000000000000210000000000A" };
        var protocol = new LineMarkingCodeProtocol();
        using var stream = new MemoryStream();

        using (var writer = protocol.CreateWriter(stream))
        {
            foreach (var code in codes)
                await writer.WriteAsync(code, Ct);
        }

        stream.Position = 0;
        using var reader = protocol.CreateReader(stream);

        foreach (var code in codes)
            Assert.Equal(code, await reader.ReadAsync(Ct));

        Assert.Null(await reader.ReadAsync(Ct));
    }

    [Fact]
    public void CreateReader_ReturnsReader() =>
        Assert.NotNull(new LineMarkingCodeProtocol().CreateReader(new MemoryStream()));

    [Fact]
    public void CreateWriter_ReturnsWriter() =>
        Assert.NotNull(new LineMarkingCodeProtocol().CreateWriter(new MemoryStream()));
}