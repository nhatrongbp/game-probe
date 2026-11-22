using GameProbe.Core.Diagnostics;
using GameProbe.Core.Models;
using GameProbe.Core.Windows;

namespace GameProbe.Core.Input;

public static class RegionResolver
{
    public static ResolvedRegion Resolve(RegionSpec region, WindowInfo window)
    {
        ArgumentNullException.ThrowIfNull(region);
        ArgumentNullException.ThrowIfNull(window);

        if (region.Width <= 0 || region.Height <= 0)
        {
            throw new GameProbeException(
                GameProbeErrorKind.InvalidConfiguration,
                $"Region '{region.Id}' có kích thước không hợp lệ: " +
                $"{region.Width}x{region.Height}.");
        }

        var origin = CoordinateResolver.Resolve(region, window);
        var outside = origin.IsOutsideClientArea
            || origin.X < 0
            || origin.Y < 0
            || origin.X + region.Width > window.ClientWidth
            || origin.Y + region.Height > window.ClientHeight;

        return new ResolvedRegion(
            origin.X,
            origin.Y,
            region.Width,
            region.Height,
            outside);
    }
}
