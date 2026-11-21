namespace GameProbe.Core.Models;

public sealed class PixelProbeSpec
{
    public string Id { get; set; } = "";

    public string Name { get; set; } = "";

    public CoordinateSpec X { get; set; } = new();

    public CoordinateSpec Y { get; set; } = new();

    /// <summary>Expected FFZik-style color class 0..3, or null to only read.</summary>
    public int? ExpectedClass { get; set; }

    /// <summary>Optional #RRGGBB reference color.</summary>
    public string? ExpectedRgb { get; set; }

    public int Tolerance { get; set; } = 8;

    public int WatchIntervalMs { get; set; } = 200;
}
