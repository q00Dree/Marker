namespace GS1Marker.Server.Generation;

public static class Gtin
{
    private const int NormalizedLength = 14;
    private const int NormalizedBodyLength = NormalizedLength - 1;

    private const int LightWeight = 1;
    private const int HeavyWeight = 3;

    private const int Radix = 10;
    private const char Zero = '0';

    private static readonly int[] SupportedLengths = [8, 12, 13, 14];

    public static string Create(ReadOnlySpan<char> body)
    {
        int gtinLength = body.Length + 1;
        ThrowIfUnsupportedLength(gtinLength, nameof(body));

        Span<char> buffer = stackalloc char[NormalizedLength];
        Span<char> normalizedBody = buffer[..NormalizedBodyLength];

        Normalize(body, normalizedBody);
        buffer[^1] = ComputeCheckDigit(normalizedBody);

        return buffer.ToString();
    }

    private static void Normalize(ReadOnlySpan<char> body, Span<char> destination)
    {
        int padding = destination.Length - body.Length;
        destination[..padding].Fill(Zero);
        body.CopyTo(destination[padding..]);
    }

    private static char ComputeCheckDigit(ReadOnlySpan<char> normalizedBody)
    {
        int sum = 0;
        for (int i = 0; i < normalizedBody.Length; i++)
            sum += ToDigit(normalizedBody[i]) * GetWeight(i);

        int checkDigit = CheckDigitFor(sum);
        return ToChar(checkDigit);
    }

    private static int CheckDigitFor(int sum) => (Radix - sum % Radix) % Radix;
    private static int GetWeight(int index)
        => index % 2 == 0
            ? HeavyWeight
            : LightWeight;

    private static bool IsSupportedLength(int length) => length is 8 or 12 or 13 or 14;
    private static void ThrowIfUnsupportedLength(int length, string paramName)
    {
        if (!IsSupportedLength(length))
            throw new ArgumentException(
                $"Unsupported GTIN length {length}. Expected 8, 12, 13 or 14.",
                paramName);
    }

    private static char ToChar(int digit) => (char)(Zero + digit);
    private static int ToDigit(char c)
        => char.IsAsciiDigit(c)
            ? c - Zero
            : throw new FormatException($"'{c}' is not a digit.");
}