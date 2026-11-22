namespace GameProbe.Core.Diagnostics;

public enum GameProbeErrorKind
{
    Unknown,
    InvalidConfiguration,
    WindowNotFound,
    WindowUnavailable,
    AccessDenied,
    OutOfBounds,
    SendFailed,
    Timeout,
    Canceled,
    CaptureFailed,
    Unsupported
}
