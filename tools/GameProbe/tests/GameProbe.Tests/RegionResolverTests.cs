using GameProbe.Core.Diagnostics;
using GameProbe.Core.Input;
using GameProbe.Core.Models;
using GameProbe.Core.Windows;
using Xunit;

namespace GameProbe.Tests;

public sealed class RegionResolverTests
{
    [Fact]
    public void InsideRegion_Resolves()
    {
        var info = new WindowInfo { ClientWidth = 100, ClientHeight = 50 };
        var region = new RegionSpec
        {
            Id = "price",
            X = CoordinateSpec.Client(10),
            Y = CoordinateSpec.Client(5),
            Width = 20,
            Height = 10
        };

        var resolved = RegionResolver.Resolve(region, info);

        Assert.False(resolved.IsOutsideClientArea);
        Assert.Equal(10, resolved.X);
        Assert.Equal(5, resolved.Y);
        Assert.Equal(20, resolved.Width);
        Assert.Equal(10, resolved.Height);
    }

    [Fact]
    public void RegionExtendingPastRightEdge_IsOutside()
    {
        var info = new WindowInfo { ClientWidth = 100, ClientHeight = 50 };
        var region = new RegionSpec
        {
            Id = "price",
            X = CoordinateSpec.Client(90),
            Y = CoordinateSpec.Client(5),
            Width = 20,
            Height = 10
        };

        var resolved = RegionResolver.Resolve(region, info);

        Assert.True(resolved.IsOutsideClientArea);
    }

    [Fact]
    public void NegativeOrigin_IsOutside()
    {
        var info = new WindowInfo { ClientWidth = 100, ClientHeight = 50 };
        var region = new RegionSpec
        {
            Id = "price",
            X = CoordinateSpec.Client(-1),
            Y = CoordinateSpec.Client(5),
            Width = 20,
            Height = 10
        };

        var resolved = RegionResolver.Resolve(region, info);

        Assert.True(resolved.IsOutsideClientArea);
    }

    [Fact]
    public void ZeroSize_Throws()
    {
        var info = new WindowInfo { ClientWidth = 100, ClientHeight = 50 };
        var region = new RegionSpec
        {
            Id = "price",
            X = CoordinateSpec.Client(10),
            Y = CoordinateSpec.Client(5),
            Width = 0,
            Height = 10
        };

        var exception = Assert.Throws<GameProbeException>(
            () => RegionResolver.Resolve(region, info));
        Assert.Equal(GameProbeErrorKind.InvalidConfiguration, exception.Kind);
    }
}
