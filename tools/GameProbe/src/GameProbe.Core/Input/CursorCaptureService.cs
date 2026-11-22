using System.Diagnostics;
using System.Runtime.InteropServices;
using GameProbe.Core.Diagnostics;
using GameProbe.Core.Interop;
using GameProbe.Core.Windows;

namespace GameProbe.Core.Input;

public sealed class CursorCaptureService
{
    private const int DefaultVirtualKey = NativeMethods.VkF8;
    private const int PollIntervalMs = 25;

    private readonly IActionLogger _logger;

    public CursorCaptureService(IActionLogger? logger = null)
    {
        _logger = logger ?? NullActionLogger.Instance;
    }

    /// <summary>Reads the current cursor position and converts it to client coordinates.</summary>
    public CursorCaptureResult Capture(GameWindow window)
    {
        ArgumentNullException.ThrowIfNull(window);

        var info = RefreshAndValidate(window);

        if (!NativeMethods.GetCursorPos(out var screenPoint))
        {
            var error = Marshal.GetLastWin32Error();
            throw new GameProbeException(
                error == 5 ? GameProbeErrorKind.AccessDenied : GameProbeErrorKind.CaptureFailed,
                $"GetCursorPos thất bại: {Win32ErrorText.Format(error)}.",
                error);
        }

        var clientPoint = new NativePoint(screenPoint.X, screenPoint.Y);
        if (!NativeMethods.ScreenToClient(info.Handle, ref clientPoint))
        {
            var error = Marshal.GetLastWin32Error();
            throw new GameProbeException(
                error == 5 ? GameProbeErrorKind.AccessDenied : GameProbeErrorKind.CaptureFailed,
                $"ScreenToClient thất bại: {Win32ErrorText.Format(error)}.",
                error);
        }

        var resolved = CoordinateResolver.FromRaw(
            clientPoint.X,
            clientPoint.Y,
            info.ClientWidth,
            info.ClientHeight);

        _logger.Info(
            "cursor.capture",
            "Cursor position captured",
            new Dictionary<string, object?>
            {
                ["hwnd"] = info.HandleHex,
                ["pid"] = info.ProcessId,
                ["screenX"] = screenPoint.X,
                ["screenY"] = screenPoint.Y,
                ["clientX"] = clientPoint.X,
                ["clientY"] = clientPoint.Y,
                ["inside"] = resolved.IsInsideClientArea
            });

        return new CursorCaptureResult(info, screenPoint.X, screenPoint.Y, resolved);
    }

    /// <summary>
    /// Waits until the configured virtual key is pressed, then captures the cursor.
    /// Only reads global key state; it does not send input.
    /// </summary>
    public CursorCaptureResult WaitForHotkey(
        GameWindow window,
        TimeSpan timeout,
        CancellationToken cancellationToken = default,
        int virtualKey = DefaultVirtualKey)
    {
        ArgumentNullException.ThrowIfNull(window);

        if (timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout), "Timeout phải > 0.");
        }

        if (virtualKey is < 1 or > 255)
        {
            throw new ArgumentOutOfRangeException(nameof(virtualKey), "Virtual key phải nằm trong khoảng 1..255.");
        }

        _ = RefreshAndValidate(window);

        var stopwatch = Stopwatch.StartNew();
        var waitForRelease = IsKeyDown(virtualKey);

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!window.IsAlive)
            {
                throw new GameProbeException(
                    GameProbeErrorKind.WindowNotFound,
                    "Cửa sổ target đã đóng trong lúc chờ hotkey.");
            }

            var keyDown = IsKeyDown(virtualKey);

            if (waitForRelease)
            {
                if (!keyDown)
                {
                    waitForRelease = false;
                }
            }
            else if (keyDown)
            {
                return Capture(window);
            }

            if (stopwatch.Elapsed >= timeout)
            {
                throw new GameProbeException(
                    GameProbeErrorKind.Timeout,
                    $"Hết thời gian chờ hotkey F8 ({timeout.TotalSeconds:0} giây).");
            }

            if (cancellationToken.WaitHandle.WaitOne(PollIntervalMs))
            {
                cancellationToken.ThrowIfCancellationRequested();
            }
        }
    }

    private static WindowInfo RefreshAndValidate(GameWindow window)
    {
        var expectedPid = window.Info.ProcessId;

        if (!window.Refresh())
        {
            throw new GameProbeException(
                GameProbeErrorKind.WindowNotFound,
                "Cửa sổ target không còn tồn tại.");
        }

        if (expectedPid != 0 && window.Info.ProcessId != expectedPid)
        {
            throw new GameProbeException(
                GameProbeErrorKind.WindowNotFound,
                $"HWND đã được dùng lại bởi process khác (PID {window.Info.ProcessId}, " +
                $"trước đó {expectedPid}); từ chối capture.");
        }

        if (window.Info.IsMinimized)
        {
            throw new GameProbeException(
                GameProbeErrorKind.WindowUnavailable,
                "Cửa sổ đang minimized; không thể capture tọa độ.");
        }

        if (window.Info.ClientWidth <= 0 || window.Info.ClientHeight <= 0)
        {
            throw new GameProbeException(
                GameProbeErrorKind.WindowUnavailable,
                "Client area có kích thước bằng 0; không thể capture tọa độ.");
        }

        return window.Info;
    }

    private static bool IsKeyDown(int virtualKey)
    {
        var state = NativeMethods.GetAsyncKeyState(virtualKey);
        return (unchecked((ushort)state) & 0x8000) != 0;
    }
}
