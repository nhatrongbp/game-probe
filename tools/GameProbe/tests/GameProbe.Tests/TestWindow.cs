using System.Runtime.InteropServices;

namespace GameProbe.Tests;

internal sealed class TestWindow : IDisposable
{
    private const uint WsPopup = 0x80000000;

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateWindowExW(
        uint dwExStyle,
        string lpClassName,
        string lpWindowName,
        uint dwStyle,
        int x,
        int y,
        int nWidth,
        int nHeight,
        IntPtr hWndParent,
        IntPtr hMenu,
        IntPtr hInstance,
        IntPtr lpParam);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyWindow(IntPtr hWnd);

    private IntPtr _handle;

    public TestWindow(string title, int width, int height)
    {
        _handle = CreateWindowExW(
            0,
            "STATIC",
            title,
            WsPopup,
            0,
            0,
            width,
            height,
            IntPtr.Zero,
            IntPtr.Zero,
            IntPtr.Zero,
            IntPtr.Zero);

        if (_handle == IntPtr.Zero)
        {
            throw new InvalidOperationException(
                $"CreateWindowExW failed (Win32 {Marshal.GetLastWin32Error()}).");
        }
    }

    public IntPtr Handle => _handle;

    public void Dispose()
    {
        if (_handle != IntPtr.Zero)
        {
            DestroyWindow(_handle);
            _handle = IntPtr.Zero;
        }
    }
}
