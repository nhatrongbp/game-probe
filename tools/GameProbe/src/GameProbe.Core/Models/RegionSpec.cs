namespace GameProbe.Core.Models;

/// <summary>
/// A rectangular client-area region used for pixel comparison.
/// <see cref="X"/> and <see cref="Y"/> are the top-left corner; width/height are pixels.
/// </summary>
public sealed class RegionSpec
{
    public string Id { get; set; } = "";

    public string Name { get; set; } = "";

    public CoordinateSpec X { get; set; } = new();

    public CoordinateSpec Y { get; set; } = new();

    public int Width { get; set; }

    public int Height { get; set; }
}
