using GameProbe.Core.Capture;
using Xunit;

namespace GameProbe.Tests;

public sealed class RegionComparerTests
{
    [Fact]
    public void IdenticalRegions_AreEqual()
    {
        var baseline = Create(4, 3);
        var current = Create(4, 3);

        var diff = RegionComparer.Compare(baseline, current);

        Assert.False(diff.IsDifferent);
        Assert.Equal(0, diff.ChangedPixels);
        Assert.Equal(0, diff.MaxChannelDelta);
        Assert.Equal("none", diff.BoundsText);
    }

    [Fact]
    public void OnePixelDiff_DetectsChangeAndBounds()
    {
        var baseline = Create(4, 3);
        var current = Create(4, 3, (2, 1, (byte)200, (byte)10, (byte)10));

        var diff = RegionComparer.Compare(baseline, current);

        Assert.True(diff.IsDifferent);
        Assert.Equal(1, diff.ChangedPixels);
        Assert.Equal(242, diff.MaxChannelDelta);
        Assert.Equal(2, diff.MinX);
        Assert.Equal(1, diff.MinY);
        Assert.Equal(2, diff.MaxX);
        Assert.Equal(1, diff.MaxY);
    }

    [Fact]
    public void AlphaOnlyDiff_IsIgnored()
    {
        var baseline = Create(2, 2);
        var current = Create(2, 2);
        current.Pixels[3] = 0; // alpha of pixel (0,0)

        var diff = RegionComparer.Compare(baseline, current);

        Assert.False(diff.IsDifferent);
        Assert.Equal(0, diff.ChangedPixels);
    }

    [Fact]
    public void ChannelTolerance_AllowsSmallDelta()
    {
        var baseline = Create(2, 1);
        var current = Create(2, 1, (0, 0, (byte)250, (byte)250, (byte)245));

        Assert.False(RegionComparer.Compare(baseline, current, channelTolerance: 2).IsDifferent);
        Assert.True(RegionComparer.Compare(baseline, current, channelTolerance: 1).IsDifferent);
    }

    [Fact]
    public void MinChangedPixels_RequiresThreshold()
    {
        var baseline = Create(3, 1);
        var current = Create(
            3,
            1,
            (0, 0, (byte)252, (byte)252, (byte)247),
            (1, 0, (byte)10, (byte)10, (byte)10));

        Assert.False(
            RegionComparer.Compare(baseline, current, minChangedPixels: 2).IsDifferent);
        Assert.True(
            RegionComparer.Compare(baseline, current, minChangedPixels: 1).IsDifferent);
    }

    [Fact]
    public void DifferentSizes_Throw()
    {
        var baseline = Create(2, 2);
        var current = Create(3, 2);

        Assert.Throws<ArgumentException>(() => RegionComparer.Compare(baseline, current));
    }

    [Fact]
    public void InvalidParameters_Throw()
    {
        var baseline = Create(2, 2);
        var current = Create(2, 2);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => RegionComparer.Compare(baseline, current, channelTolerance: -1));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => RegionComparer.Compare(baseline, current, minChangedPixels: 0));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => RegionComparer.Compare(baseline, current, minChangedPixels: 5));
    }

    private static RegionCaptureResult Create(
        int width,
        int height,
        params (int X, int Y, byte R, byte G, byte B)[] pixels)
    {
        var data = new byte[width * height * 4];
        for (var i = 0; i < data.Length; i += 4)
        {
            data[i] = 0xF7;
            data[i + 1] = 0xFC;
            data[i + 2] = 0xFC;
            data[i + 3] = 0xFF;
        }

        foreach (var pixel in pixels)
        {
            var offset = (pixel.Y * width + pixel.X) * 4;
            data[offset] = pixel.B;
            data[offset + 1] = pixel.G;
            data[offset + 2] = pixel.R;
            data[offset + 3] = 0xFF;
        }

        return new RegionCaptureResult(
            0,
            0,
            width,
            height,
            data,
            RegionCaptureMethod.BitBlt,
            0,
            DateTimeOffset.UnixEpoch);
    }
}
