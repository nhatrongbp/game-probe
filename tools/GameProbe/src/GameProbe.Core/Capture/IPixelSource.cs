namespace GameProbe.Core.Capture;

public readonly record struct PixelCapture(PixelColor Color, PixelCaptureMethod Method);

public interface IPixelSource
{
    PixelCapture Capture(IntPtr windowHandle, int x, int y);
}
