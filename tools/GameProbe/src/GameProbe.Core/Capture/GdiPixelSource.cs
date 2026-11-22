using System.Runtime.InteropServices;
using GameProbe.Core.Diagnostics;
using GameProbe.Core.Interop;

namespace GameProbe.Core.Capture;

/// <summary>
/// Reads one client-area pixel using BitBlt + GetDIBits, with a GetPixel fallback
/// for windows that do not expose their content through a window DC.
/// </summary>
public sealed class GdiPixelSource : IPixelSource
{
    private static readonly IntPtr HdgiError = new(-1);

    private readonly bool _fallbackToGetPixelOnBlack;

    public GdiPixelSource(bool fallbackToGetPixelOnBlack = true)
    {
        _fallbackToGetPixelOnBlack = fallbackToGetPixelOnBlack;
    }

    public PixelCapture Capture(IntPtr windowHandle, int x, int y)
    {
        if (windowHandle == IntPtr.Zero)
        {
            throw new GameProbeException(
                GameProbeErrorKind.WindowNotFound,
                "HWND không hợp lệ để đọc pixel.");
        }

        var hasBitBlt = TryCaptureWithBitBlt(
            windowHandle,
            x,
            y,
            out var bitBltColor,
            out var bitBltError);

        if (hasBitBlt && (!_fallbackToGetPixelOnBlack || !bitBltColor.IsBlack))
        {
            return new PixelCapture(bitBltColor, PixelCaptureMethod.BitBlt);
        }

        var hasGetPixel = TryCaptureWithGetPixel(
            windowHandle,
            x,
            y,
            out var getPixelColor,
            out var getPixelError);

        if (hasGetPixel && (!hasBitBlt || !getPixelColor.IsBlack))
        {
            return new PixelCapture(getPixelColor, PixelCaptureMethod.GetPixel);
        }

        if (hasBitBlt)
        {
            return new PixelCapture(bitBltColor, PixelCaptureMethod.BitBlt);
        }

        var error = bitBltError != 0 ? bitBltError : getPixelError;
        var kind = error == 5 ? GameProbeErrorKind.AccessDenied : GameProbeErrorKind.CaptureFailed;
        throw new GameProbeException(
            kind,
            $"Không đọc được pixel tại ({x},{y}) của HWND 0x{windowHandle.ToInt64():X}: " +
            $"{Win32ErrorText.Format(error)}.",
            error);
    }

    private static bool TryCaptureWithBitBlt(
        IntPtr windowHandle,
        int x,
        int y,
        out PixelColor color,
        out int error)
    {
        color = default;
        error = 0;

        var windowDc = NativeMethods.GetDC(windowHandle);
        if (windowDc == IntPtr.Zero)
        {
            error = Marshal.GetLastWin32Error();
            return false;
        }

        try
        {
            return TryCaptureFromDc(windowDc, x, y, out color, out error);
        }
        finally
        {
            NativeMethods.ReleaseDC(windowHandle, windowDc);
        }
    }

    internal static bool TryCaptureFromDc(
        IntPtr sourceDc,
        int x,
        int y,
        out PixelColor color,
        out int error)
    {
        color = default;
        error = 0;

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

            bitmap = NativeMethods.CreateCompatibleBitmap(sourceDc, 1, 1);
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
                    1,
                    1,
                    sourceDc,
                    x,
                    y,
                    NativeMethods.SrcCopy))
            {
                error = Marshal.GetLastWin32Error();
                return false;
            }

            NativeMethods.SelectObject(memoryDc, previousBitmap);
            bitmapSelected = false;

            var bitmapInfo = new BitmapInfo
            {
                Header = new BitmapInfoHeader
                {
                    Size = (uint)Marshal.SizeOf<BitmapInfoHeader>(),
                    Width = 1,
                    Height = -1,
                    Planes = 1,
                    BitCount = 32,
                    Compression = 0
                }
            };

            var pixels = new byte[4];
            var scanLines = NativeMethods.GetDIBits(
                memoryDc,
                bitmap,
                0,
                1,
                pixels,
                ref bitmapInfo,
                NativeMethods.DibRgbColors);

            if (scanLines == 0)
            {
                error = Marshal.GetLastWin32Error();
                return false;
            }

            color = new PixelColor(pixels[2], pixels[1], pixels[0]);
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

    private static bool TryCaptureWithGetPixel(
        IntPtr windowHandle,
        int x,
        int y,
        out PixelColor color,
        out int error)
    {
        color = default;
        error = 0;

        var dc = NativeMethods.GetDC(windowHandle);
        if (dc == IntPtr.Zero)
        {
            error = Marshal.GetLastWin32Error();
            return false;
        }

        try
        {
            var value = NativeMethods.GetPixel(dc, x, y);
            if (value == uint.MaxValue)
            {
                error = Marshal.GetLastWin32Error();
                return false;
            }

            color = new PixelColor(
                (byte)(value & 0xFF),
                (byte)((value >> 8) & 0xFF),
                (byte)((value >> 16) & 0xFF));
            return true;
        }
        finally
        {
            NativeMethods.ReleaseDC(windowHandle, dc);
        }
    }
}
