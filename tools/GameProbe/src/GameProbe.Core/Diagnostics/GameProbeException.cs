namespace GameProbe.Core.Diagnostics;

public sealed class GameProbeException : Exception
{
    public GameProbeException(
        GameProbeErrorKind kind,
        string message,
        int win32Error = 0,
        Exception? innerException = null)
        : base(message, innerException)
    {
        Kind = kind;
        Win32Error = win32Error;
    }

    public GameProbeErrorKind Kind { get; }

    public int Win32Error { get; }
}
