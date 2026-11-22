using System.Runtime.InteropServices;
using GameProbe.Core.Diagnostics;
using GameProbe.Core.Interop;

namespace GameProbe.Core.Capture;

/// <summary>
/// Captures a client-area rectangle using BitBlt + GetDIBits. If the window DC
/// returns an all-black frame, a desktop DC fallback is attempted because some
/// hardware-rendered windows do not expose their surface through GetDC(hwnd).
/// </summary>
public sealed class GdiRegionSource : IRegionSource
{
    private static readonly IntPtr HdgiError = new(-1);

    private const int MaximumDimension = 4096;

    private readonly bool _fallbackToDesktopOnBlack;

    public GdiRegionSource(bool fallbackToDesktopOnBlack = true)
    {
        _fallbackToDesktopOnBlack = fallbackToDesktopOnBlack;
    }

    public RegionCaptureResult Capture(IntPtr windowHandle, int x, int y, int width, int height)
    {
        if (windowHandle == IntPtr.Zero)
        {
            throw new GameProbeException(
                GameProbeErrorKind.WindowNotFound,
                "HWND không hợp lệ để chụp vùng ảnh.");
        }

        ValidateArguments(x, y, width, height);

        var windowDc = NativeMethods.GetDC(windowHandle);
        if (windowDc == IntPtr.Zero)
        {
            var error = Marshal.GetLastWin32Error();
            throw CreateCaptureException(windowHandle, x, y, width, height, error);
        }

        try
        {
            if (TryCaptureFromDc(windowDc, x, y, width, height, out var pixels, out var error))
            {
                if (!_fallbackToDesktopOnBlack || !IsAllBlack(pixels))
                {
                    return CreateResult(x, y, width, height, pixels, RegionCaptureMethod.BitBlt);
                }

                EnsureDesktopCaptureNotObscured(windowHandle, x, y, width, height);

                if (TryCaptureFromDesktop(
                        windowHandle,
                        x,
                        y,
                        width,
                        height,
                        out var desktopPixels,
                        out _))
                {
                    return CreateResult(
                        x,
                        y,
                        width,
                        height,
                        desktopPixels,
                        RegionCaptureMethod.DesktopBitBlt);
                }

                // A black window-DC capture may be legitimate; keep it when the
                // desktop fallback is unavailable instead of failing hard.
                return CreateResult(x, y, width, height, pixels, RegionCaptureMethod.BitBlt);
            }

            EnsureDesktopCaptureNotObscured(windowHandle, x, y, width, height);

            if (TryCaptureFromDesktop(
                    windowHandle,
                    x,
                    y,
                    width,
                    height,
                    out var fallbackPixels,
                    out var fallbackError))
            {
                return CreateResult(
                    x,
                    y,
                    width,
                    height,
                    fallbackPixels,
                    RegionCaptureMethod.DesktopBitBlt);
            }

            var finalError = error != 0 ? error : fallbackError;
            throw CreateCaptureException(windowHandle, x, y, width, height, finalError);
        }
        finally
        {
            NativeMethods.ReleaseDC(windowHandle, windowDc);
        }
    }

    internal static bool TryCaptureFromDc(
        IntPtr sourceDc,
        int sourceX,
        int sourceY,
        int width,
        int height,
        out byte[] pixels,
        out int error)
    {
        pixels = Array.Empty<byte>();
        error = 0;

        if (sourceDc == IntPtr.Zero)
        {
            error = 6; // ERROR_INVALID_HANDLE
            return false;
        }

        var memoryDc = IntPtr.Zero;
        var bitmap = IntPtr.Zero;
        var previousBitmap = IntPtr.Zero;
        var bitmapSelected = false;

        try
        {
            memoryDc = NativeMethods.CreateCompatibleDC(sourceDc);
            if (memoryDc == IntPtr.Zero)
            {
                error = Marshal.GetLastWin32Error();
                return false;
            }

            bitmap = NativeMethods.CreateCompatibleBitmap(sourceDc, width, height);
            if (bitmap == IntPtr.Zero)
            {
                error = Marshal.GetLastWin32Error();
                return false;
            }

            previousBitmap = NativeMethods.SelectObject(memoryDc, bitmap);
            if (previousBitmap == IntPtr.Zero || previousBitmap == HdgiError)
            {
                error = Marshal.GetLastWin32Error();
                return false;
            }

            bitmapSelected = true;

            if (!NativeMethods.BitBlt(
                    memoryDc,
                    0,
                    0,
                    width,
                    height,
                    sourceDc,
                    sourceX,
                    sourceY,
                    NativeMethods.SrcCopy))
            {
                error = Marshal.GetLastWin32Error();
                return false;
            }

            NativeMethods.SelectObject(memoryDc, previousBitmap);
            bitmapSelected = false;

            var imageSize = checked(width * height * 4);
            var bitmapInfo = new BitmapInfo
            {
                Header = new BitmapInfoHeader
                {
                    Size = (uint)Marshal.SizeOf<BitmapInfoHeader>(),
                    Width = width,
                    Height = -height,
                    Planes = 1,
                    BitCount = 32,
                    Compression = 0,
                    SizeImage = (uint)imageSize
                }
            };

            var buffer = new byte[imageSize];
            var scanLines = NativeMethods.GetDIBits(
                memoryDc,
                bitmap,
                0,
                (uint)height,
                buffer,
                ref bitmapInfo,
                NativeMethods.DibRgbColors);

            if (scanLines == 0)
            {
                error = Marshal.GetLastWin32Error();
                return false;
            }

            pixels = buffer;
            return true;
        }
        finally
        {
            if (bitmapSelected)
            {
                NativeMethods.SelectObject(memoryDc, previousBitmap);
            }

            if (bitmap != IntPtr.Zero)
            {
                NativeMethods.DeleteObject(bitmap);
            }

            if (memoryDc != IntPtr.Zero)
            {
                NativeMethods.DeleteDC(memoryDc);
            }
        }
    }

    private static void EnsureDesktopCaptureNotObscured(
        IntPtr windowHandle,
        int x,
        int y,
        int width,
        int height)
    {
        var origin = new NativePoint(x, y);
        if (!NativeMethods.ClientToScreen(windowHandle, ref origin))
        {
            var error = Marshal.GetLastWin32Error();
            throw CreateCaptureException(windowHandle, x, y, width, height, error);
        }

        NativeMethods.GetWindowThreadProcessId(windowHandle, out var targetProcessId);

        // Check the center and four near-corner points. A different top-level
        // window at any of them means the desktop capture would include a
        // foreign overlay, which can cause a false price change.
        var points = new[]
        {
            new NativePoint(origin.X + width / 2, origin.Y + height / 2),
            new NativePoint(origin.X + 1, origin.Y + 1),
            new NativePoint(origin.X + width - 2, origin.Y + 1),
            new NativePoint(origin.X + 1, origin.Y + height - 2),
            new NativePoint(origin.X + width - 2, origin.Y + height - 2)
        };

        foreach (var point in points)
        {
            var topWindow = NativeMethods.WindowFromPoint(point);
            if (topWindow == IntPtr.Zero)
            {
                throw new GameProbeException(
                    GameProbeErrorKind.CaptureFailed,
                    $"Không xác định được cửa sổ tại ({point.X},{point.Y}) " +
                    "trước khi dùng desktop capture.");
            }

            NativeMethods.GetWindowThreadProcessId(topWindow, out var topProcessId);
            if (topProcessId != targetProcessId)
            {
                throw new GameProbeException(
                    GameProbeErrorKind.WindowUnavailable,
                    $"Vùng cần chụp đang bị cửa sổ khác che tại ({point.X},{point.Y}): " +
                    $"PID {topProcessId} thay vì {targetProcessId}. " +
                    "Đưa cửa sổ game lên trước hoặc không bị che rồi chạy lại.");
            }
        }
    }

    private static bool TryCaptureFromDesktop(
        IntPtr windowHandle,
        int x,
        int y,
        int width,
        int height,
        out byte[] pixels,
        out int error)
    {
        pixels = Array.Empty<byte>();
        error = 0;

        var screenDc = NativeMethods.GetDC(IntPtr.Zero);
        if (screenDc == IntPtr.Zero)
        {
            error = Marshal.GetLastWin32Error();
            return false;
        }

        try
        {
            var origin = new NativePoint(x, y);
            if (!NativeMethods.ClientToScreen(windowHandle, ref origin))
            {
                error = Marshal.GetLastWin32Error();
                return false;
            }

            return TryCaptureFromDc(
                screenDc,
                origin.X,
                origin.Y,
                width,
                height,
                out pixels,
                out error);
        }
        finally
        {
            NativeMethods.ReleaseDC(IntPtr.Zero, screenDc);
        }
    }

    private static RegionCaptureResult CreateResult(
        int x,
        int y,
        int width,
        int height,
        byte[] pixels,
        RegionCaptureMethod method)
    {
        return new RegionCaptureResult(
            x,
            y,
            width,
            height,
            pixels,
            method,
            ComputeHash(pixels),
            DateTimeOffset.Now);
    }

    private static ulong ComputeHash(ReadOnlySpan<byte> pixels)
    {
        const ulong offsetBasis = 14695981039346656037UL;
        const ulong prime = 1099511628211UL;

        var hash = offsetBasis;
        foreach (var value in pixels)
        {
            hash ^= value;
            hash *= prime;
        }

        return hash;
    }

    private static bool IsAllBlack(ReadOnlySpan<byte> pixels)
    {
        for (var i = 0; i < pixels.Length; i += 4)
        {
            if (pixels[i] != 0 || pixels[i + 1] != 0 || pixels[i + 2] != 0)
            {
                return false;
            }
        }

        return true;
    }

    private static void ValidateArguments(int x, int y, int width, int height)
    {
        if (x < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(x), "x phải >= 0.");
        }

        if (y < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(y), "y phải >= 0.");
        }

        if (width is < 1 or > MaximumDimension)
        {
            throw new ArgumentOutOfRangeException(
                nameof(width),
                $"width phải nằm trong khoảng 1..{MaximumDimension}.");
        }

        if (height is < 1 or > MaximumDimension)
        {
            throw new ArgumentOutOfRangeException(
                nameof(height),
                $"height phải nằm trong khoảng 1..{MaximumDimension}.");
        }
    }

    private static GameProbeException CreateCaptureException(
        IntPtr windowHandle,
        int x,
        int y,
        int width,
        int height,
        int error)
    {
        var kind = error == 5
            ? GameProbeErrorKind.AccessDenied
            : GameProbeErrorKind.CaptureFailed;

        return new GameProbeException(
            kind,
            $"Không chụp được vùng ({x},{y}) {width}x{height} của HWND " +
            $"0x{windowHandle.ToInt64():X}: {Win32ErrorText.Format(error)}.",
            error);
    }
}
