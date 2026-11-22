using GameProbe.Core.Diagnostics;
using GameProbe.Core.Input;
using GameProbe.Core.Models;
using GameProbe.Core.Windows;
using Xunit;

namespace GameProbe.Tests;

public sealed class CoordinateResolverTests
{
    [Fact]
    public void ClientMode_ReturnsConfiguredPixel()
    {
        var resolved = CoordinateResolver.Resolve(
            CoordinateSpec.Client(568),
            CoordinateSpec.Client(124),
            1280,
            720);

        Assert.Equal(568, resolved.X);
        Assert.Equal(124, resolved.Y);
        Assert.True(resolved.IsInsideClientArea);
    }

    [Fact]
    public void PercentMode_ScalesAndAddsOffset()
    {
        var resolved = CoordinateResolver.Resolve(
            new CoordinateSpec { Mode = CoordinateMode.Percent, Value = 0.25, Offset = 10 },
            new CoordinateSpec { Mode = CoordinateMode.Percent, Value = 0.5, Offset = -5 },
            1000,
            500);

        Assert.Equal(260, resolved.X);
        Assert.Equal(245, resolved.Y);
        Assert.True(resolved.IsInsideClientArea);
    }

    [Fact]
    public void AnchorCenterAndBottom_MatchesFfzikFormula()
    {
        var resolved = CoordinateResolver.Resolve(
            CoordinateSpec.Anchored(Anchor.Center, 30),
            CoordinateSpec.Anchored(Anchor.Bottom, -67),
            1280,
            720);

        Assert.Equal(670, resolved.X);
        Assert.Equal(653, resolved.Y);
        Assert.True(resolved.IsInsideClientArea);
    }

    [Fact]
    public void AnchorLeftTop_ReturnsZero()
    {
        var resolved = CoordinateResolver.Resolve(
            CoordinateSpec.Anchored(Anchor.Left, 0),
            CoordinateSpec.Anchored(Anchor.Top, 0),
            100,
            50);

        Assert.Equal(0, resolved.X);
        Assert.Equal(0, resolved.Y);
    }

    [Fact]
    public void AnchorRightBottom_AtZeroOffset_IsOutsideAndClamped()
    {
        var resolved = CoordinateResolver.Resolve(
            CoordinateSpec.Anchored(Anchor.Right, 0),
            CoordinateSpec.Anchored(Anchor.Bottom, 0),
            100,
            50);

        Assert.True(resolved.IsOutsideClientArea);
        Assert.Equal(99, resolved.X);
        Assert.Equal(49, resolved.Y);
    }

    [Fact]
    public void PercentOne_IsOutsideAndClamped()
    {
        var resolved = CoordinateResolver.Resolve(
            CoordinateSpec.Percent(1.0),
            CoordinateSpec.Percent(1.0),
            100,
            50);

        Assert.True(resolved.IsOutsideClientArea);
        Assert.Equal(99, resolved.X);
        Assert.Equal(49, resolved.Y);
    }

    [Fact]
    public void NegativeCoordinates_AreOutsideAndClamped()
    {
        var resolved = CoordinateResolver.Resolve(
            CoordinateSpec.Client(-1),
            CoordinateSpec.Client(-5),
            100,
            50);

        Assert.True(resolved.IsOutsideClientArea);
        Assert.Equal(0, resolved.X);
        Assert.Equal(0, resolved.Y);
    }

    [Fact]
    public void FractionalCoordinates_RoundAwayFromZero()
    {
        var resolved = CoordinateResolver.Resolve(
            new CoordinateSpec { Mode = CoordinateMode.Percent, Value = 0.335 },
            new CoordinateSpec { Mode = CoordinateMode.Percent, Value = 0.334 },
            100,
            100);

        Assert.Equal(34, resolved.X);
        Assert.Equal(33, resolved.Y);
    }

    [Fact]
    public void ZeroClientSize_ThrowsWindowUnavailable()
    {
        var exception = Assert.Throws<GameProbeException>(() =>
            CoordinateResolver.Resolve(
                CoordinateSpec.Client(1),
                CoordinateSpec.Client(1),
                0,
                100));

        Assert.Equal(GameProbeErrorKind.WindowUnavailable, exception.Kind);
    }

    [Fact]
    public void InvalidAnchorForAxis_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            CoordinateResolver.Resolve(
                CoordinateSpec.Anchored(Anchor.Top, 0),
                CoordinateSpec.Anchored(Anchor.Center, 0),
                100,
                100));
    }

    [Fact]
    public void ResolveClickPointAndPixelProbe_UseClientSize()
    {
        var window = new WindowInfo
        {
            Handle = new IntPtr(1),
            ClientWidth = 800,
            ClientHeight = 600
        };

        var click = new ClickPoint
        {
            X = CoordinateSpec.Client(10),
            Y = CoordinateSpec.Client(20)
        };

        var pixel = new PixelProbeSpec
        {
            X = CoordinateSpec.Percent(0.5),
            Y = CoordinateSpec.Anchored(Anchor.Bottom, -10)
        };

        var clickPoint = CoordinateResolver.Resolve(click, window);
        var pixelPoint = CoordinateResolver.Resolve(pixel, window);

        Assert.Equal(10, clickPoint.X);
        Assert.Equal(20, clickPoint.Y);
        Assert.Equal(400, pixelPoint.X);
        Assert.Equal(590, pixelPoint.Y);
    }
}
