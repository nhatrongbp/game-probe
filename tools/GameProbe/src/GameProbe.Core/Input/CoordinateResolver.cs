using GameProbe.Core.Diagnostics;
using GameProbe.Core.Models;
using GameProbe.Core.Windows;

namespace GameProbe.Core.Input;

/// <summary>
/// Resolves configured coordinates to client-area pixels.
/// Percent values are fractions of the client size; anchor offsets are pixels.
/// </summary>
public static class CoordinateResolver
{
    public static ResolvedPoint Resolve(
        CoordinateSpec x,
        CoordinateSpec y,
        int clientWidth,
        int clientHeight)
    {
        ArgumentNullException.ThrowIfNull(x);
        ArgumentNullException.ThrowIfNull(y);

        var rawX = ResolveAxis(x, clientWidth, isXAxis: true);
        var rawY = ResolveAxis(y, clientHeight, isXAxis: false);
        return FromRaw(rawX, rawY, clientWidth, clientHeight);
    }

    public static ResolvedPoint Resolve(ClickPoint point, WindowInfo window)
    {
        ArgumentNullException.ThrowIfNull(point);
        ArgumentNullException.ThrowIfNull(window);
        return Resolve(point.X, point.Y, window.ClientWidth, window.ClientHeight);
    }

    public static ResolvedPoint Resolve(PixelProbeSpec probe, WindowInfo window)
    {
        ArgumentNullException.ThrowIfNull(probe);
        ArgumentNullException.ThrowIfNull(window);
        return Resolve(probe.X, probe.Y, window.ClientWidth, window.ClientHeight);
    }

    public static ResolvedPoint FromRaw(
        double rawX,
        double rawY,
        int clientWidth,
        int clientHeight)
    {
        if (clientWidth <= 0 || clientHeight <= 0)
        {
            throw new GameProbeException(
                GameProbeErrorKind.WindowUnavailable,
                $"Client area không hợp lệ ({clientWidth}x{clientHeight}); cửa sổ có thể đang minimized.");
        }

        var x = (int)Math.Round(rawX, MidpointRounding.AwayFromZero);
        var y = (int)Math.Round(rawY, MidpointRounding.AwayFromZero);
        var outside = x < 0 || x >= clientWidth || y < 0 || y >= clientHeight;

        return new ResolvedPoint(
            rawX,
            rawY,
            Math.Clamp(x, 0, clientWidth - 1),
            Math.Clamp(y, 0, clientHeight - 1),
            outside);
    }

    private static double ResolveAxis(CoordinateSpec spec, int clientSize, bool isXAxis)
    {
        if (clientSize <= 0)
        {
            throw new GameProbeException(
                GameProbeErrorKind.WindowUnavailable,
                "Client area có kích thước bằng 0; cửa sổ có thể đang minimized.");
        }

        return spec.Mode switch
        {
            CoordinateMode.Client => spec.Value,
            CoordinateMode.Percent => clientSize * spec.Value + spec.Offset,
            CoordinateMode.Anchor => ResolveAnchor(spec, clientSize, isXAxis),
            _ => throw new ArgumentException($"Coordinate mode không hợp lệ: {spec.Mode}.", nameof(spec))
        };
    }

    private static double ResolveAnchor(CoordinateSpec spec, int clientSize, bool isXAxis)
    {
        if (spec.Anchor is null)
        {
            throw new ArgumentException("Anchor mode yêu cầu giá trị anchor.", nameof(spec));
        }

        var anchor = spec.Anchor.Value;

        if (isXAxis)
        {
            return anchor switch
            {
                Anchor.Left => spec.Offset,
                Anchor.Center => clientSize / 2.0 + spec.Offset,
                Anchor.Right => clientSize + spec.Offset,
                _ => throw new ArgumentException(
                    $"Anchor '{anchor}' không hợp lệ cho trục X (dùng left/center/right).",
                    nameof(spec))
            };
        }

        return anchor switch
        {
            Anchor.Top => spec.Offset,
            Anchor.Center => clientSize / 2.0 + spec.Offset,
            Anchor.Bottom => clientSize + spec.Offset,
            _ => throw new ArgumentException(
                $"Anchor '{anchor}' không hợp lệ cho trục Y (dùng top/center/bottom).",
                nameof(spec))
        };
    }
}
