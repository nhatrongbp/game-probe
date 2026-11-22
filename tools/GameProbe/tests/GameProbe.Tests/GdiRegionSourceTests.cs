using GameProbe.Core.Capture;
using GameProbe.Core.Interop;
using Xunit;

namespace GameProbe.Tests;

public sealed class GdiRegionSourceTests
{
    [Fact]
    public void TryCaptureFromDc_ReadsTopDownBgra()
    {
        var screenDc = NativeMethods.GetDC(IntPtr.Zero);
        Assert.NotEqual(IntPtr.Zero, screenDc);

        var memoryDc = NativeMethods.CreateCompatibleDC(screenDc);
        Assert.NotEqual(IntPtr.Zero, memoryDc);

        var bitmap = NativeMethods.CreateCompatibleBitmap(screenDc, 8, 4);
        Assert.NotEqual(IntPtr.Zero, bitmap);

        var previousBitmap = NativeMethods.SelectObject(memoryDc, bitmap);
        Assert.NotEqual(IntPtr.Zero, previousBitmap);

        try
        {
            for (var y = 0; y < 4; y++)
            {
                for (var x = 0; x < 8; x++)
                {
                    NativeMethods.SetPixel(memoryDc, x, y, 0x00FFFFFFu);
                }
            }

            // COLORREF 0x00BBGGRR.
            NativeMethods.SetPixel(memoryDc, 2, 1, 0x000000FFu); // red
            NativeMethods.SetPixel(memoryDc, 3, 2, 0x00FF0000u); // blue

            var captured = GdiRegionSource.TryCaptureFromDc(
                memoryDc,
                1,
                1,
                4,
                2,
                out var pixels,
                out var error);

            Assert.True(captured, $"GDI region capture failed (Win32 {error}).");
            Assert.Equal(4 * 2 * 4, pixels.Length);

            // Captured region is x=1..4, y=1..2. The red source pixel (2,1)
            // becomes (1,0) in the capture.
            var redOffset = (0 * 4 + 1) * 4;
            Assert.Equal(0, pixels[redOffset]);
            Assert.Equal(0, pixels[redOffset + 1]);
            Assert.Equal(255, pixels[redOffset + 2]);

            // The blue source pixel (3,2) becomes (2,1) in the capture.
            var blueOffset = (1 * 4 + 2) * 4;
            Assert.Equal(255, pixels[blueOffset]);
            Assert.Equal(0, pixels[blueOffset + 1]);
            Assert.Equal(0, pixels[blueOffset + 2]);

            // White background pixel (0,0).
            Assert.Equal(255, pixels[0]);
            Assert.Equal(255, pixels[1]);
            Assert.Equal(255, pixels[2]);
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
