using GameProbe.Core.Capture;
using GameProbe.Core.Diagnostics;
using GameProbe.Core.Input;
using GameProbe.Core.Models;
using GameProbe.Core.Windows;
using Xunit;

namespace GameProbe.Tests;

public sealed class PixelProbeTests
{
    [Fact]
    public void Read_ReturnsColorClassAndExpectedMatches()
    {
        using var hidden = new TestWindow("GameProbe Pixel Read", 200, 100);
        var window = Attach(hidden);
        var source = new FakePixelSource(new PixelColor(0, 160, 255));
        var probe = new PixelProbe(NullActionLogger.Instance, source);

        var result = probe.Read(
            window,
            new PixelProbeRequest
            {
                X = 10,
                Y = 20,
                Tolerance = 8,
                ExpectedClass = 1,
                ExpectedRgb = "#00A0FF"
            });

        Assert.Equal(10, result.X);
        Assert.Equal(20, result.Y);
        Assert.Equal(new PixelColor(0, 160, 255), result.Color);
        Assert.Equal(PixelColorClass.Blue, result.ColorClass);
        Assert.True(result.MatchesExpectedClass);
        Assert.True(result.MatchesExpectedRgb);
        Assert.True(result.IsExactRgbMatch);
        Assert.Equal(1, source.CallCount);
        Assert.Equal(10, source.LastX);
        Assert.Equal(20, source.LastY);
    }

    [Fact]
    public void Read_ExpectedRgbWithinTolerance_IsNotExact()
    {
        using var hidden = new TestWindow("GameProbe Pixel Tolerance", 200, 100);
        var window = Attach(hidden);
        var source = new FakePixelSource(new PixelColor(0, 165, 250));
        var probe = new PixelProbe(NullActionLogger.Instance, source);

        var result = probe.Read(
            window,
            new PixelProbeRequest
            {
                X = 10,
                Y = 20,
                Tolerance = 8,
                ExpectedRgb = "#00A0FF"
            });

        Assert.True(result.MatchesExpectedRgb);
        Assert.False(result.IsExactRgbMatch);
    }

    [Fact]
    public void Read_ProbeSpec_ResolvesAnchorCoordinates()
    {
        using var hidden = new TestWindow("GameProbe Pixel Spec", 200, 100);
        var window = Attach(hidden);
        var source = new FakePixelSource(new PixelColor(255, 255, 0));
        var probe = new PixelProbe(NullActionLogger.Instance, source);
        var spec = new PixelProbeSpec
        {
            Id = "yellow",
            X = CoordinateSpec.Anchored(Anchor.Center, 0),
            Y = CoordinateSpec.Anchored(Anchor.Bottom, -10),
            Tolerance = 8,
            ExpectedClass = 2
        };

        var result = probe.Read(window, spec);

        Assert.Equal(100, result.X);
        Assert.Equal(90, result.Y);
        Assert.Equal(PixelColorClass.Yellow, result.ColorClass);
        Assert.True(result.MatchesExpectedClass);
    }

    [Fact]
    public void Read_OutOfBounds_ThrowsWithoutCallingSource()
    {
        using var hidden = new TestWindow("GameProbe Pixel Bounds", 200, 100);
        var window = Attach(hidden);
        var source = new FakePixelSource(new PixelColor(1, 2, 3));
        var probe = new PixelProbe(NullActionLogger.Instance, source);

        var exception = Assert.Throws<GameProbeException>(() =>
            probe.Read(window, new PixelProbeRequest { X = 500, Y = 20 }));

        Assert.Equal(GameProbeErrorKind.OutOfBounds, exception.Kind);
        Assert.Equal(0, source.CallCount);
    }

    [Fact]
    public void Read_InvalidExpectedClass_Throws()
    {
        using var hidden = new TestWindow("GameProbe Pixel Class", 200, 100);
        var window = Attach(hidden);
        var probe = new PixelProbe(
            NullActionLogger.Instance,
            new FakePixelSource(new PixelColor(1, 2, 3)));

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            probe.Read(window, new PixelProbeRequest
            {
                X = 10,
                Y = 20,
                ExpectedClass = 9
            }));
    }

    [Fact]
    public void Read_InvalidTolerance_Throws()
    {
        using var hidden = new TestWindow("GameProbe Pixel Tolerance Invalid", 200, 100);
        var window = Attach(hidden);
        var probe = new PixelProbe(
            NullActionLogger.Instance,
            new FakePixelSource(new PixelColor(1, 2, 3)));

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            probe.Read(window, new PixelProbeRequest
            {
                X = 10,
                Y = 20,
                Tolerance = 200
            }));
    }

    private static GameWindow Attach(TestWindow hidden)
    {
        var locator = new WindowLocator(NullActionLogger.Instance);
        return GameWindow.AttachByHandle(locator, hidden.Handle)
            ?? throw new InvalidOperationException("Could not attach to test window.");
    }

    private sealed class FakePixelSource : IPixelSource
    {
        private readonly PixelColor _color;

        public FakePixelSource(PixelColor color)
        {
            _color = color;
        }

        public int CallCount { get; private set; }

        public int LastX { get; private set; }

        public int LastY { get; private set; }

        public PixelCapture Capture(IntPtr windowHandle, int x, int y)
        {
            CallCount++;
            LastX = x;
            LastY = y;
            return new PixelCapture(_color, PixelCaptureMethod.BitBlt);
        }
    }
}
