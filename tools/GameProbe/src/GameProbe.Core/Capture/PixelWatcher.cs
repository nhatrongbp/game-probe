using System.Diagnostics;
using GameProbe.Core.Diagnostics;
using GameProbe.Core.Input;
using GameProbe.Core.Models;
using GameProbe.Core.Windows;

namespace GameProbe.Core.Capture;

public sealed class PixelWatcher
{
    public const int MinimumIntervalMs = 50;

    private readonly PixelProbe _probe;
    private readonly IActionLogger _logger;

    public PixelWatcher(PixelProbe probe, IActionLogger? logger = null)
    {
        _probe = probe ?? throw new ArgumentNullException(nameof(probe));
        _logger = logger ?? NullActionLogger.Instance;
    }

    public PixelWatchSummary Run(
        GameWindow window,
        PixelProbeSpec probe,
        int intervalMs,
        TimeSpan? duration,
        Action<PixelWatchSample> onSample,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(probe);
        return Run(
            () => _probe.Read(window, probe),
            intervalMs,
            duration,
            onSample,
            cancellationToken,
            $"probe '{probe.Id}'");
    }

    public PixelWatchSummary Run(
        GameWindow window,
        PixelProbeRequest request,
        int intervalMs,
        TimeSpan? duration,
        Action<PixelWatchSample> onSample,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(request);
        return Run(
            () => _probe.Read(window, request),
            intervalMs,
            duration,
            onSample,
            cancellationToken,
            $"point ({request.X},{request.Y})");
    }

    private PixelWatchSummary Run(
        Func<PixelReadResult> read,
        int intervalMs,
        TimeSpan? duration,
        Action<PixelWatchSample> onSample,
        CancellationToken cancellationToken,
        string targetDescription)
    {
        ArgumentNullException.ThrowIfNull(read);
        ArgumentNullException.ThrowIfNull(onSample);

        if (intervalMs < MinimumIntervalMs)
        {
            throw new ArgumentOutOfRangeException(
                nameof(intervalMs),
                $"Watch interval phải >= {MinimumIntervalMs} ms.");
        }

        if (intervalMs > 600000)
        {
            throw new ArgumentOutOfRangeException(
                nameof(intervalMs),
                "Watch interval phải <= 600000 ms.");
        }

        if (duration is { } requestedDuration && requestedDuration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(duration), "Duration phải > 0.");
        }

        _logger.Info(
            "pixel.watch.start",
            "Pixel watch started",
            new Dictionary<string, object?>
            {
                ["target"] = targetDescription,
                ["intervalMs"] = intervalMs,
                ["durationSec"] = duration?.TotalSeconds
            });

        var stopwatch = Stopwatch.StartNew();
        var samples = 0;
        var changes = 0;
        var canceled = false;
        PixelReadResult? previous = null;

        while (true)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
            }
            catch (OperationCanceledException)
            {
                canceled = true;
                break;
            }

            var current = read();
            samples++;

            var changed = previous is null
                || current.Color != previous.Color
                || current.ColorClass != previous.ColorClass;

            var sample = new PixelWatchSample(
                DateTimeOffset.Now,
                current,
                previous,
                changed);

            if (changed)
            {
                changes++;
                onSample(sample);

                _logger.Info(
                    "pixel.watch.change",
                    "Pixel color changed",
                    new Dictionary<string, object?>
                    {
                        ["x"] = current.X,
                        ["y"] = current.Y,
                        ["previous"] = previous?.Hex,
                        ["current"] = current.Hex,
                        ["previousClass"] = previous?.ColorClass,
                        ["currentClass"] = current.ColorClass,
                        ["method"] = current.CaptureMethod.ToString()
                    });
            }

            previous = current;

            if (duration is { } limit && stopwatch.Elapsed >= limit)
            {
                break;
            }

            if (cancellationToken.WaitHandle.WaitOne(intervalMs))
            {
                canceled = true;
                break;
            }
        }

        stopwatch.Stop();

        _logger.Info(
            "pixel.watch.stop",
            "Pixel watch stopped",
            new Dictionary<string, object?>
            {
                ["target"] = targetDescription,
                ["samples"] = samples,
                ["changes"] = changes,
                ["durationMs"] = Math.Round(stopwatch.Elapsed.TotalMilliseconds, 1),
                ["canceled"] = canceled
            });

        return new PixelWatchSummary(samples, changes, stopwatch.Elapsed, canceled);
    }
}
