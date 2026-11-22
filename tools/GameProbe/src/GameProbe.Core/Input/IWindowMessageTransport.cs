namespace GameProbe.Core.Input;

public readonly record struct WindowMessageSendResult(bool Success, int ErrorCode)
{
    public static WindowMessageSendResult Ok { get; } = new(true, 0);

    public static WindowMessageSendResult Fail(int errorCode) => new(false, errorCode);
}

public interface IWindowMessageTransport
{
    WindowMessageSendResult PostMessage(
        IntPtr hWnd,
        uint message,
        IntPtr wParam,
        IntPtr lParam);

    WindowMessageSendResult SendMessage(
        IntPtr hWnd,
        uint message,
        IntPtr wParam,
        IntPtr lParam,
        TimeSpan timeout);
}
