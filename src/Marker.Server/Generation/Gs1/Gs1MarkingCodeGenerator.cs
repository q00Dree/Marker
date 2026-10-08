namespace Marker.Server.Generation.Gs1;

public sealed class Gs1MarkingCodeGenerator : IMarkingCodeGenerator
{
    private const string AiGtin = "01";
    private static readonly int[] GtinBodyLengths = [7, 11, 12, 13];
    private const string Digits = "0123456789";

    private const string AiSerial = "21";
    private const int SerialLength = 13;
    private const string SerialAlphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";

    private readonly Random _random;

    public Gs1MarkingCodeGenerator(Random random) => _random = random;
    public Gs1MarkingCodeGenerator() : this(Random.Shared) { }

    public string Generate() => $"{AiGtin}{GenerateGtin()}{AiSerial}{GenerateSerial()}";

    private string GenerateGtin()
    {
        int bodyLength = GtinBodyLengths[_random.Next(GtinBodyLengths.Length)];

        Span<char> body = stackalloc char[bodyLength];
        _random.GetItems(Digits.AsSpan(), body);

        return Gtin.Create(body);
    }

    private string GenerateSerial()
    {
        Span<char> serial = stackalloc char[SerialLength];
        _random.GetItems(SerialAlphabet.AsSpan(), serial);

        return serial.ToString();
    }
}