using System.Globalization;

namespace GameProbe.Core.Capture;

public readonly record struct PixelColor(byte R, byte G, byte B)
{
    public string Hex => $"#{R:X2}{G:X2}{B:X2}";

    public string RgbText => $"({R},{G},{B})";

    public bool IsBlack => R == 0 && G == 0 && B == 0;

    public int Classify(int tolerance = 8) => ColorClassifier.Classify(this, tolerance);

    public static PixelColor FromHex(string hex)
    {
        if (string.IsNullOrWhiteSpace(hex))
        {
            throw new FormatException("Màu hex không được để trống.");
        }

        var text = hex.Trim();
        if (text.StartsWith('#'))
        {
            text = text[1..];
        }

        if (text.Length != 6
            || !uint.TryParse(
                text,
                NumberStyles.HexNumber,
                CultureInfo.InvariantCulture,
                out var value))
        {
            throw new FormatException($"Màu hex không hợp lệ: '{hex}' (cần #RRGGBB).");
        }

        return new PixelColor(
            (byte)(value >> 16),
            (byte)(value >> 8),
            (byte)value);
    }

    public override string ToString() => Hex;
}
