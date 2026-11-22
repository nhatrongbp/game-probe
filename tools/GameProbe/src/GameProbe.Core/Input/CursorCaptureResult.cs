using GameProbe.Core.Windows;

namespace GameProbe.Core.Input;

public sealed record CursorCaptureResult(
    WindowInfo Window,
    int ScreenX,
    int ScreenY,
    ResolvedPoint Point);
