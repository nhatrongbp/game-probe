namespace GameProbe.Core.Capture;

public static class PixelColorClass
{
    public const int WhiteOrGray = 0;
    public const int Blue = 1;
    public const int Yellow = 2;
    public const int Other = 3;

    public static string Describe(int colorClass)
    {
        return colorClass switch
        {
            WhiteOrGray => "trắng/xám",
            Blue => "xanh dương",
            Yellow => "vàng",
            Other => "khác/đen",
            _ => "không xác định"
        };
    }
}

public static class ColorClassifier
{
    /// <summary>
    /// FFZik-compatible classification:
    /// 0 = white/gray (R≈G≈B, R &gt; 0), 1 = blue (R≈G, B &gt; G),
    /// 2 = yellow (R≈G, B &lt; G), 3 = everything else.
    /// A relaxed blue fallback handles gradient UI colors such as #00A0FF.
    /// </summary>
    public static int Classify(int r, int g, int b, int tolerance = 8)
    {
        if (tolerance is < 0 or > 255)
        {
            throw new ArgumentOutOfRangeException(nameof(tolerance), "Tolerance phải nằm trong khoảng 0..255.");
        }

        var rg = Math.Abs(r - g);
        var gb = Math.Abs(g - b);

        if (rg <= tolerance && gb <= tolerance && r > 0)
        {
            return PixelColorClass.WhiteOrGray;
        }

        if (rg <= tolerance && b - g > tolerance)
        {
            return PixelColorClass.Blue;
        }

        if (rg <= tolerance && g - b > tolerance)
        {
            return PixelColorClass.Yellow;
        }

        // Relaxed fallback for gradient/AA blue buttons such as #00A0FF, where
        // green is much higher than red but blue still dominates both channels.
        if (b - Math.Max(r, g) > tolerance)
        {
            return PixelColorClass.Blue;
        }

        return PixelColorClass.Other;
    }

    public static int Classify(PixelColor color, int tolerance = 8) =>
        Classify(color.R, color.G, color.B, tolerance);

    public static bool IsClose(PixelColor actual, PixelColor expected, int tolerance)
    {
        if (tolerance is < 0 or > 255)
        {
            throw new ArgumentOutOfRangeException(nameof(tolerance), "Tolerance phải nằm trong khoảng 0..255.");
        }

        return Math.Abs(actual.R - expected.R) <= tolerance
            && Math.Abs(actual.G - expected.G) <= tolerance
            && Math.Abs(actual.B - expected.B) <= tolerance;
    }
}
