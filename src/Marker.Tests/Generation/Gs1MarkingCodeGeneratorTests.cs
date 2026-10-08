using Marker.Server.Generation;
using Marker.Server.Generation.Gs1;
using Xunit;

namespace Marker.Tests.Generation;

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
    public void Generate_ReturnsCodeOfFixedLength()
    {
        // Arrange & Act
        var codes = GenerateMany();

        // Assert
        Assert.All(codes, code => Assert.Equal(CodeLength, code.Length));
    }

    [Fact]
    public void Generate_StartsWithGtinIdentifier()
    {
        // Arrange & Act
        var codes = GenerateMany();

        // Assert
        Assert.All(codes, code => Assert.StartsWith("01", code));
    }

    [Fact]
    public void Generate_HasSerialIdentifierRightAfterGtin()
    {
        // Arrange & Act
        var codes = GenerateMany();

        // Assert
        Assert.All(codes, code =>
            Assert.Equal("21", code.Substring(SerialAiStart, 2)));
    }

    [Fact]
    public void Generate_GtinConsistsOfDigitsOnly()
    {
        // Arrange & Act
        var codes = GenerateMany();

        // Assert
        Assert.All(codes, code =>
            Assert.All(code.Substring(GtinStart, GtinLength),
                c => Assert.True(char.IsAsciiDigit(c))));
    }

    [Fact]
    public void Generate_GtinHasValidCheckDigit()
    {
        // Arrange & Act
        var codes = GenerateMany();

        // Assert
        Assert.All(codes, code =>
        {
            var gtin = code.Substring(GtinStart, GtinLength);
            Assert.Equal(gtin, Gtin.Create(gtin.AsSpan(0, GtinLength - 1)));
        });
    }

    [Fact]
    public void Generate_SerialHasExpectedLength()
    {
        // Arrange & Act
        var codes = GenerateMany();

        // Assert
        Assert.All(codes, code =>
            Assert.Equal(SerialLength, code[SerialStart..].Length));
    }

    [Fact]
    public void Generate_SerialUsesLettersAndDigitsOnly()
    {
        // Arrange & Act
        var codes = GenerateMany();

        // Assert
        Assert.All(codes, code =>
            Assert.All(code[SerialStart..], c => Assert.True(char.IsAsciiLetterOrDigit(c))));
    }

    [Fact]
    public void Generate_WithSameSeed_ReturnsSameSequence()
    {
        // Arrange
        var first = new Gs1MarkingCodeGenerator(new Random(1));
        var second = new Gs1MarkingCodeGenerator(new Random(1));

        // Act & Assert
        for (var i = 0; i < 10; i++)
            Assert.Equal(first.Generate(), second.Generate());
    }

    [Fact]
    public void Generate_WithDifferentSeeds_ReturnsDifferentCodes()
    {
        // Arrange
        var generator1 = new Gs1MarkingCodeGenerator(new Random(1));
        var generator2 = new Gs1MarkingCodeGenerator(new Random(2));

        // Act
        var first = generator1.Generate();
        var second = generator2.Generate();

        // Assert
        Assert.NotEqual(first, second);
    }

    private List<string> GenerateMany() => [.. Enumerable.Range(0, Iterations).Select(_ => _sut.Generate())];
}