namespace GameProbe.Core.Models;

/// <summary>
/// Specifies how one axis of a click/pixel coordinate is resolved.
/// Percent values are fractions of the client size (0..1); anchor offsets are pixels.
/// </summary>
public sealed class CoordinateSpec
{
    public CoordinateMode Mode { get; set; } = CoordinateMode.Client;

    /// <summary>Absolute client coordinate or percent fraction (0..1).</summary>
    public double Value { get; set; }

    /// <summary>Anchor used by <see cref="CoordinateMode.Anchor"/>.</summary>
    public Anchor? Anchor { get; set; }

    /// <summary>Pixel offset added to the anchor point.</summary>
    public double Offset { get; set; }

    public static CoordinateSpec Client(double value) => new()
    {
        Mode = CoordinateMode.Client,
        Value = value
    };

    public static CoordinateSpec Percent(double value) => new()
    {
        Mode = CoordinateMode.Percent,
        Value = value
    };

    public static CoordinateSpec Anchored(Anchor anchor, double offset = 0) => new()
    {
        Mode = CoordinateMode.Anchor,
        Anchor = anchor,
        Offset = offset
    };
}
