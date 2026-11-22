namespace GameProbe.Core.Capture;

/// <summary>
/// A top-down BGRA capture of a client-area rectangle. The stride is always
/// <see cref="Width"/> * 4; the alpha byte is not used for comparison.
/// </summary>
public sealed class RegionCaptureResult
{
    public RegionCaptureResult(
        int x,
        int y,
        int width,
        int height,
        byte[] pixels,
        RegionCaptureMethod method,
        ulong hash,
        DateTimeOffset capturedAt)
    {
        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Width phải > 0.");
        }

        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height), "Height phải > 0.");
        }

        ArgumentNullException.ThrowIfNull(pixels);

        var expectedLength = checked(width * height * 4);
        if (pixels.Length != expectedLength)
        {
            throw new ArgumentException(
                $"Pixel buffer phải có {expectedLength} byte (BGRA), nhận {pixels.Length}.",
                nameof(pixels));
        }

        X = x;
        Y = y;
        Width = width;
        Height = height;
        Pixels = pixels;
        Method = method;
        Hash = hash;
        CapturedAt = capturedAt;
    }

    public int X { get; }

    public int Y { get; }

    public int Width { get; }

    public int Height { get; }

    public int Stride => Width * 4;

    public int PixelCount => Width * Height;

    public byte[] Pixels { get; }

    public RegionCaptureMethod Method { get; }

    public ulong Hash { get; }

    public DateTimeOffset CapturedAt { get; }

    public string HashHex => $"0x{Hash:X16}";
}
