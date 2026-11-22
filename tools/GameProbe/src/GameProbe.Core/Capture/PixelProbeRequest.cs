namespace GameProbe.Core.Capture;

public sealed record PixelProbeRequest
{
    public int X { get; init; }

    public int Y { get; init; }

    public int Tolerance { get; init; } = 8;

    public int? ExpectedClass { get; init; }

    public string? ExpectedRgb { get; init; }
}
