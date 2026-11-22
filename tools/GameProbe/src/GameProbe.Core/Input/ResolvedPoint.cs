namespace GameProbe.Core.Input;

/// <summary>
/// Result of resolving a coordinate. <see cref="X"/>/<see cref="Y"/> are clamped to the
/// client area; raw values are kept for diagnostics.
/// </summary>
public readonly record struct ResolvedPoint(
    double RawX,
    double RawY,
    int X,
    int Y,
    bool IsOutsideClientArea)
{
    public bool IsInsideClientArea => !IsOutsideClientArea;

    public override string ToString() => $"({X},{Y})";
}
