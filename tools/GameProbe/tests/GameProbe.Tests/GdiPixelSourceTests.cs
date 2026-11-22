using GameProbe.Core.Capture;
using GameProbe.Core.Interop;
using Xunit;

namespace GameProbe.Tests;

public sealed class GdiPixelSourceTests
{
    [Fact]
    public void TryCaptureFromDc_ReadsKnownPixel()
    {
        var screenDc = NativeMethods.GetDC(IntPtr.Zero);
        Assert.NotEqual(IntPtr.Zero, screenDc);

        var memoryDc = NativeMethods.CreateCompatibleDC(screenDc);
        Assert.NotEqual(IntPtr.Zero, memoryDc);

        var bitmap = NativeMethods.CreateCompatibleBitmap(screenDc, 1, 1);
        Assert.NotEqual(IntPtr.Zero, bitmap);

        var previousBitmap = NativeMethods.SelectObject(memoryDc, bitmap);
        Assert.NotEqual(IntPtr.Zero, previousBitmap);

        try
        {
            // COLORREF is 0x00BBGGRR: R=0, G=160, B=255.
            var setResult = NativeMethods.SetPixel(memoryDc, 0, 0, 0x00FFA000u);
            Assert.NotEqual(uint.MaxValue, setResult);

            var captured = GdiPixelSource.TryCaptureFromDc(
                memoryDc,
                0,
                0,
                out var color,
                out var error);

            Assert.True(captured, $"GDI capture failed (Win32 {error}).");
            Assert.Equal(new PixelColor(0, 160, 255), color);
        }
        finally
        {
            NativeMethods.SelectObject(memoryDc, previousBitmap);
            NativeMethods.DeleteObject(bitmap);
            NativeMethods.DeleteDC(memoryDc);
            NativeMethods.ReleaseDC(IntPtr.Zero, screenDc);
        }
    }
}
