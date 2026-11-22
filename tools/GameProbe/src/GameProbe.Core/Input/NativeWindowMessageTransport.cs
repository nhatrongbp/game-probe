using System.Runtime.InteropServices;
using GameProbe.Core.Interop;

namespace GameProbe.Core.Input;

public sealed class NativeWindowMessageTransport : IWindowMessageTransport
{
    public WindowMessageSendResult PostMessage(
        IntPtr hWnd,
        uint message,
        IntPtr wParam,
        IntPtr lParam)
    {
        var sent = NativeMethods.PostMessageW(hWnd, message, wParam, lParam);
        return sent
            ? WindowMessageSendResult.Ok
            : WindowMessageSendResult.Fail(Marshal.GetLastWin32Error());
    }

    public WindowMessageSendResult SendMessage(
        IntPtr hWnd,
        uint message,
        IntPtr wParam,
        IntPtr lParam,
        TimeSpan timeout)
    {
        var timeoutMs = (uint)Math.Clamp(
            Math.Ceiling(timeout.TotalMilliseconds),
            1,
            int.MaxValue);

        var sent = NativeMethods.SendMessageTimeoutW(
            hWnd,
            message,
            wParam,
            lParam,
            NativeMethods.SmtoAbortIfHung,
            timeoutMs,
            out _);

        if (sent != IntPtr.Zero)
        {
            return WindowMessageSendResult.Ok;
        }

        var error = Marshal.GetLastWin32Error();
        return WindowMessageSendResult.Fail(error == 0 ? 1460 : error);
    }
}
