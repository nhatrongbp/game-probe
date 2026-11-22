using GameProbe.Core.Capture;
using GameProbe.Core.Diagnostics;
using GameProbe.Core.Windows;
using Xunit;

namespace GameProbe.Tests;

public sealed class PixelWatcherTests
{
    [Fact]
    public void Run_ReportsFirstSampleAndChanges()
    {
        using var hidden = new TestWindow("GameProbe Pixel Watch", 200, 100);
        var window = Attach(hidden);
        var source = new SequencePixelSource(
            new PixelColor(0, 0, 255),
            new PixelColor(0, 0, 255),
            new PixelColor(255, 255, 0),
            new PixelColor(255, 255, 0));
        var probe = new PixelProbe(NullActionLogger.Instance, source);
        var watcher = new PixelWatcher(probe, NullActionLogger.Instance);
        var samples = new List<PixelWatchSample>();

        var summary = watcher.Run(
            window,
            new PixelProbeRequest { X = 10, Y = 20 },
            intervalMs: 50,
            duration: TimeSpan.FromMilliseconds(250),
            samples.Add,
            CancellationToken.None);

        Assert.True(summary.SampleCount >= 2, $"Samples: {summary.SampleCount}");
        Assert.True(summary.ChangeCount >= 1, $"Changes: {summary.ChangeCount}");
        Assert.False(summary.Canceled);
        Assert.NotEmpty(samples);
        Assert.True(samples[0].Changed);
        Assert.Null(samples[0].Previous);
        Assert.Contains(samples, sample => sample.Result.ColorClass == PixelColorClass.Yellow);
    }

    [Fact]
    public void Run_PreCanceled_ReturnsCanceledWithoutReading()
    {
        using var hidden = new TestWindow("GameProbe Pixel Watch Cancel", 200, 100);
        var window = Attach(hidden);
        var source = new SequencePixelSource(new PixelColor(1, 2, 3));
        var probe = new PixelProbe(NullActionLogger.Instance, source);
        var watcher = new PixelWatcher(probe, NullActionLogger.Instance);

        var summary = watcher.Run(
            window,
            new PixelProbeRequest { X = 10, Y = 20 },
            intervalMs: 50,
            duration: null,
            _ => { },
            new CancellationToken(canceled: true));

        Assert.True(summary.Canceled);
        Assert.Equal(0, summary.SampleCount);
        Assert.Equal(0, summary.ChangeCount);
        Assert.Equal(0, source.CallCount);
    }

    [Fact]
    public void Run_IntervalBelowMinimum_Throws()
    {
        using var hidden = new TestWindow("GameProbe Pixel Watch Interval", 200, 100);
        var window = Attach(hidden);
        var probe = new PixelProbe(
            NullActionLogger.Instance,
            new SequencePixelSource(new PixelColor(1, 2, 3)));
        var watcher = new PixelWatcher(probe, NullActionLogger.Instance);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            watcher.Run(
                window,
                new PixelProbeRequest { X = 10, Y = 20 },
                intervalMs: 10,
                duration: TimeSpan.FromMilliseconds(100),
                _ => { },
                CancellationToken.None));
    }

    private static GameWindow Attach(TestWindow hidden)
    {
        var locator = new WindowLocator(NullActionLogger.Instance);
        return GameWindow.AttachByHandle(locator, hidden.Handle)
            ?? throw new InvalidOperationException("Could not attach to test window.");
    }

    private sealed class SequencePixelSource : IPixelSource
    {
        private readonly PixelColor[] _colors;
        private int _index;

        public SequencePixelSource(params PixelColor[] colors)
        {
            _colors = colors;
        }

        public int CallCount => _index;

        public PixelCapture Capture(IntPtr windowHandle, int x, int y)
        {
            var index = Math.Min(_index, _colors.Length - 1);
            _index++;
            return new PixelCapture(_colors[index], PixelCaptureMethod.BitBlt);
        }
    }
}
