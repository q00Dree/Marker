using GS1Marker.Server.Generation;
using Xunit;

namespace GS1Marker.Tests.Generation;

public class GtinTests
{
    private const int NormalizedLength = 14;

    [Theory]
    [InlineData("0460123456789", "04601234567893")]
    [InlineData("460123456789", "04601234567893")]
    [InlineData("4601234", "00000046012340")]
    [InlineData("9638507", "00000096385074")]
    [InlineData("03600029145", "00036000291452")]
    [InlineData("400638133393", "04006381333931")]
    public void Create_ValidBody_ReturnsNormalizedGtin(string body, string expected) =>
        Assert.Equal(expected, Gtin.Create(body));

    [Theory]
    [InlineData("0000000", "00000000000000")]
    [InlineData("0000000000000", "00000000000000")]
    public void Create_ZeroBody_ReturnsAllZeros(string body, string expected) =>
        Assert.Equal(expected, Gtin.Create(body));

    [Theory]
    [InlineData(7)]
    [InlineData(11)]
    [InlineData(12)]
    [InlineData(13)]
    public void Create_SupportedBodyLength_ReturnsFourteenDigits(int bodyLength)
    {
        var gtin = Gtin.Create(new string('7', bodyLength));

        Assert.Equal(NormalizedLength, gtin.Length);
        Assert.All(gtin, c => Assert.True(char.IsAsciiDigit(c)));
    }

    [Fact]
    public void Create_ShortBody_PadsWithLeadingZerosAndKeepsBody()
    {
        var gtin = Gtin.Create("4601234");

        Assert.Equal("000000", gtin[..6]);
        Assert.Equal("4601234", gtin[6..13]);
    }

    [Theory]
    [InlineData("")]
    [InlineData("123456")]
    [InlineData("1234567890")]
    [InlineData("12345678901234")]
    public void Create_UnsupportedLength_ThrowsArgumentException(string body)
    {
        var ex = Assert.Throws<ArgumentException>(() => Gtin.Create(body));
        Assert.Equal("body", ex.ParamName);
    }

    [Theory]
    [InlineData("46012345678ab")]
    [InlineData("4601234567 89")]
    public void Create_NonAsciiDigit_ThrowsFormatException(string body) =>
        Assert.Throws<FormatException>(() => Gtin.Create(body));
}