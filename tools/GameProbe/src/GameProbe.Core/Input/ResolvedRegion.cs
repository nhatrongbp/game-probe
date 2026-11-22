namespace GameProbe.Core.Input;

public readonly record struct ResolvedRegion(
    int X,
    int Y,
    int Width,
    int Height,
    bool IsOutsideClientArea)
{
    public bool IsInsideClientArea => !IsOutsideClientArea;

    public override string ToString() => $"({X},{Y}) {Width}x{Height}";
}
