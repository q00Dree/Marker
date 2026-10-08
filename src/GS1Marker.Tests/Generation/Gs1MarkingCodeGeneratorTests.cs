using GS1Marker.Server.Generation;
using Xunit;

namespace GS1Marker.Tests.Generation;

public class Gs1MarkingCodeGeneratorTests
{
    private const int Iterations = 1000;

    private const int GtinStart = 2;
    private const int GtinLength = 14;
    private const int SerialAiStart = GtinStart + GtinLength;   // 16
    private const int SerialStart = SerialAiStart + 2;          // 18
    private const int SerialLength = 13;
    private const int CodeLength = SerialStart + SerialLength;  // 32

    private readonly Gs1MarkingCodeGenerator _sut = new(new Random(42));

    [Fact]
    public void Generate_ReturnsCodeOfFixedLength() =>
        Assert.All(GenerateMany(), code => Assert.Equal(CodeLength, code.Length));

    [Fact]
    public void Generate_StartsWithGtinIdentifier() =>
        Assert.All(GenerateMany(), code =>
            Assert.StartsWith("01", code));

    [Fact]
    public void Generate_HasSerialIdentifierRightAfterGtin() =>
        Assert.All(GenerateMany(), code =>
            Assert.Equal("21", code.Substring(SerialAiStart, 2)));

    [Fact]
    public void Generate_GtinConsistsOfDigitsOnly() =>
        Assert.All(GenerateMany(), code =>
            Assert.All(code.Substring(GtinStart, GtinLength),
                c => Assert.True(char.IsAsciiDigit(c))));

    [Fact]
    public void Generate_GtinHasValidCheckDigit() =>
        Assert.All(GenerateMany(), code =>
        {
            var gtin = code.Substring(GtinStart, GtinLength);
            Assert.Equal(gtin, Gtin.Create(gtin.AsSpan(0, GtinLength - 1)));
        });

    [Fact]
    public void Generate_SerialHasExpectedLength() =>
        Assert.All(GenerateMany(), code =>
            Assert.Equal(SerialLength, code[SerialStart..].Length));

    [Fact]
    public void Generate_SerialUsesLettersAndDigitsOnly() =>
        Assert.All(GenerateMany(), code =>
            Assert.All(code[SerialStart..], c => Assert.True(char.IsAsciiLetterOrDigit(c))));

    [Fact]
    public void Generate_WithSameSeed_ReturnsSameSequence()
    {
        var first = new Gs1MarkingCodeGenerator(new Random(1));
        var second = new Gs1MarkingCodeGenerator(new Random(1));

        for (var i = 0; i < 10; i++)
            Assert.Equal(first.Generate(), second.Generate());
    }

    [Fact]
    public void Generate_WithDifferentSeeds_ReturnsDifferentCodes()
    {
        var first = new Gs1MarkingCodeGenerator(new Random(1)).Generate();
        var second = new Gs1MarkingCodeGenerator(new Random(2)).Generate();

        Assert.NotEqual(first, second);
    }

    private List<string> GenerateMany() => [.. Enumerable.Range(0, Iterations).Select(_ => _sut.Generate())];
}