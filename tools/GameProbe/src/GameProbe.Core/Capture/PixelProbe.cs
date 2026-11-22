using GameProbe.Core.Diagnostics;
using GameProbe.Core.Input;
using GameProbe.Core.Models;
using GameProbe.Core.Windows;

namespace GameProbe.Core.Capture;

public sealed class PixelProbe : IPixelReader
{
    private readonly IActionLogger _logger;
    private readonly IPixelSource _source;

    public PixelProbe(IActionLogger? logger = null, IPixelSource? source = null)
    {
        _logger = logger ?? NullActionLogger.Instance;
        _source = source ?? new GdiPixelSource();
    }

    public PixelReadResult Read(GameWindow window, PixelProbeSpec probe)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(probe);

        var info = PrepareWindow(window);
        var resolved = CoordinateResolver.Resolve(probe, info);
        return ReadResolved(
            info,
            resolved,
            probe.Tolerance,
            probe.ExpectedClass,
            probe.ExpectedRgb);
    }

    public PixelReadResult Read(
        GameWindow window,
        int x,
        int y,
        int tolerance = 8,
        int? expectedClass = null,
        string? expectedRgb = null)
    {
        return Read(
            window,
            new PixelProbeRequest
            {
                X = x,
                Y = y,
                Tolerance = tolerance,
                ExpectedClass = expectedClass,
                ExpectedRgb = expectedRgb
            });
    }

    public PixelReadResult Read(GameWindow window, PixelProbeRequest request)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(request);

        var info = PrepareWindow(window);
        var resolved = CoordinateResolver.FromRaw(
            request.X,
            request.Y,
            info.ClientWidth,
            info.ClientHeight);

        return ReadResolved(
            info,
            resolved,
            request.Tolerance,
            request.ExpectedClass,
            request.ExpectedRgb);
    }

    private PixelReadResult ReadResolved(
        WindowInfo info,
        ResolvedPoint resolved,
        int tolerance,
        int? expectedClass,
        string? expectedRgb)
    {
        if (resolved.IsOutsideClientArea)
        {
            throw new GameProbeException(
                GameProbeErrorKind.OutOfBounds,
                $"Tọa độ ({resolved.RawX:0.##},{resolved.RawY:0.##}) nằm ngoài client area " +
                $"{info.ClientSizeText}.");
        }

        if (tolerance is < 0 or > 128)
        {
            throw new ArgumentOutOfRangeException(nameof(tolerance), "Tolerance phải nằm trong khoảng 0..128.");
        }

        if (expectedClass is < 0 or > 3)
        {
            throw new ArgumentOutOfRangeException(
                nameof(expectedClass),
                "Expected class phải nằm trong khoảng 0..3.");
        }

        PixelColor? expectedColor = null;
        if (!string.IsNullOrWhiteSpace(expectedRgb))
        {
            try
            {
                expectedColor = PixelColor.FromHex(expectedRgb);
            }
            catch (FormatException ex)
            {
                throw new GameProbeException(
                    GameProbeErrorKind.InvalidConfiguration,
                    ex.Message,
                    innerException: ex);
            }
        }

        PixelCapture capture;
        try
        {
            capture = _source.Capture(info.Handle, resolved.X, resolved.Y);
        }
        catch (GameProbeException ex)
        {
            _logger.Error(
                "pixel.read",
                ex.Message,
                new Dictionary<string, object?>
                {
                    ["hwnd"] = info.HandleHex,
                    ["pid"] = info.ProcessId,
                    ["x"] = resolved.X,
                    ["y"] = resolved.Y,
                    ["kind"] = ex.Kind.ToString(),
                    ["win32"] = ex.Win32Error
                });
            throw;
        }

        var colorClass = ColorClassifier.Classify(capture.Color, tolerance);
        var matchesClass = expectedClass is null
            ? (bool?)null
            : colorClass == expectedClass.Value;

        bool? matchesRgb = null;
        bool? exactRgb = null;
        if (expectedColor is { } expected)
        {
            matchesRgb = ColorClassifier.IsClose(capture.Color, expected, tolerance);
            exactRgb = capture.Color == expected;
        }

        return new PixelReadResult(
            resolved.X,
            resolved.Y,
            capture.Color,
            colorClass,
            capture.Method,
            tolerance,
            expectedClass,
            matchesClass,
            expectedRgb,
            matchesRgb,
            exactRgb,
            DateTimeOffset.Now);
    }

    private WindowInfo PrepareWindow(GameWindow window)
    {
        var expectedPid = window.Info.ProcessId;

        if (!window.Refresh())
        {
            throw new GameProbeException(
                GameProbeErrorKind.WindowNotFound,
                "Cửa sổ target không còn tồn tại.");
        }

        if (expectedPid != 0 && window.Info.ProcessId != expectedPid)
        {
            throw new GameProbeException(
                GameProbeErrorKind.WindowNotFound,
                $"HWND đã được dùng lại bởi process khác (PID {window.Info.ProcessId}, " +
                $"trước đó {expectedPid}); từ chối đọc pixel.");
        }

        if (window.Info.IsMinimized)
        {
            throw new GameProbeException(
                GameProbeErrorKind.WindowUnavailable,
                "Cửa sổ đang minimized; không thể đọc pixel.");
        }

        if (window.Info.ClientWidth <= 0 || window.Info.ClientHeight <= 0)
        {
            throw new GameProbeException(
                GameProbeErrorKind.WindowUnavailable,
                "Client area có kích thước bằng 0; không thể đọc pixel.");
        }

        return window.Info;
    }
}
