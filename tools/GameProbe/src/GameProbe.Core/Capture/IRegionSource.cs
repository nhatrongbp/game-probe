namespace GameProbe.Core.Capture;

public interface IRegionSource
{
    RegionCaptureResult Capture(IntPtr windowHandle, int x, int y, int width, int height);
}
