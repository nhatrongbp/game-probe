namespace GameProbe.Core.Capture;

public sealed record PixelReadResult(
    int X,
    int Y,
    PixelColor Color,
    int ColorClass,
    PixelCaptureMethod CaptureMethod,
    int Tolerance,
    int? ExpectedClass,
    bool? MatchesExpectedClass,
    string? ExpectedRgb,
    bool? MatchesExpectedRgb,
    bool? IsExactRgbMatch,
    DateTimeOffset Timestamp)
{
    public string Hex => Color.Hex;

    public string RgbText => Color.RgbText;

    public string ColorClassName => PixelColorClass.Describe(ColorClass);
}
