using System.Diagnostics;
using GameProbe.Core.Diagnostics;
using GameProbe.Core.Models;
using GameProbe.Core.Windows;

namespace GameProbe.Core.Input;

public sealed class ClickService : IClickExecutor
{
    private readonly IActionLogger _logger;
    private readonly IWindowMessageTransport _transport;
    private readonly ClickServiceOptions _options;
    private readonly object _clickGate = new();

    private long _lastClickStarted;
    private bool _hasClicked;

    public ClickService(
        IActionLogger? logger = null,
        IWindowMessageTransport? transport = null,
        ClickServiceOptions? options = null)
    {
        _logger = logger ?? NullActionLogger.Instance;
        _transport = transport ?? new NativeWindowMessageTransport();
        _options = options ?? new ClickServiceOptions();

        if (_options.MinimumClickSpacingMs < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "MinimumClickSpacingMs phải >= 0.");
        }

        if (_options.SendTimeoutMs < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "SendTimeoutMs phải >= 1.");
        }
    }

    public ClickExecutionResult Click(
        GameWindow window,
        ClickPoint point,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(point);
        ValidatePoint(point);
        EnsureSupportedSyncMode(point);

        lock (_clickGate)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var stopwatch = Stopwatch.StartNew();

            var (resolved, lParam) = PrepareTarget(window, point);

            WaitForRateLimit(cancellationToken);

            var messages = ClickMessages.For(point.Button);
            var downWParam = new IntPtr(ClickMessages.GetDownWParam(point.Button, point.WParamMode));
            var intervalMs = Math.Max(point.IntervalMs, _options.MinimumClickSpacingMs);

            _logger.Info(
                "click.start",
                "Click sequence started",
                new Dictionary<string, object?>
                {
                    ["hwnd"] = window.Info.HandleHex,
                    ["pid"] = window.Info.ProcessId,
                    ["point"] = point.Id,
                    ["x"] = resolved.X,
                    ["y"] = resolved.Y,
                    ["button"] = point.Button.ToString().ToLowerInvariant(),
                    ["count"] = point.Count,
                    ["mode"] = point.SyncMode.ToString().ToLowerInvariant(),
                    ["hoverSettleMs"] = point.HoverSettleMs
                });

            try
            {
                for (var i = 0; i < point.Count; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (point.Hover)
                    {
                        SendMessage(
                            window,
                            point.SyncMode,
                            ClickMessages.WmMouseMove,
                            IntPtr.Zero,
                            lParam,
                            cancellationToken);

                        // Some game UIs only enable row buttons after the hover
                        // state has been rendered. Give the UI one or more frames
                        // before delivering the button-down message.
                        Sleep(point.HoverSettleMs, cancellationToken);
                    }

                    SendMessage(
                        window,
                        point.SyncMode,
                        messages.Down,
                        downWParam,
                        lParam,
                        cancellationToken);

                    Sleep(point.HoldMs, cancellationToken);

                    SendMessage(
                        window,
                        point.SyncMode,
                        messages.Up,
                        IntPtr.Zero,
                        lParam,
                        cancellationToken);

                    if (i < point.Count - 1)
                    {
                        Sleep(intervalMs, cancellationToken);
                    }
                }
            }
            catch (GameProbeException ex)
            {
                _logger.Error(
                    "click.failed",
                    ex.Message,
                    new Dictionary<string, object?>
                    {
                        ["hwnd"] = window.Info.HandleHex,
                        ["pid"] = window.Info.ProcessId,
                        ["point"] = point.Id,
                        ["kind"] = ex.Kind.ToString(),
                        ["win32"] = ex.Win32Error
                    });
                throw;
            }
            catch (OperationCanceledException)
            {
                _logger.Warn(
                    "click.canceled",
                    "Click sequence canceled",
                    new Dictionary<string, object?>
                    {
                        ["hwnd"] = window.Info.HandleHex,
                        ["point"] = point.Id
                    });
                throw;
            }

            stopwatch.Stop();

            _logger.Info(
                "click.done",
                "Click sequence completed",
                new Dictionary<string, object?>
                {
                    ["hwnd"] = window.Info.HandleHex,
                    ["pid"] = window.Info.ProcessId,
                    ["point"] = point.Id,
                    ["x"] = resolved.X,
                    ["y"] = resolved.Y,
                    ["sent"] = point.Count,
                    ["durationMs"] = Math.Round(stopwatch.Elapsed.TotalMilliseconds, 1)
                });

            return new ClickExecutionResult(
                resolved.X,
                resolved.Y,
                point.Count,
                stopwatch.Elapsed);
        }
    }

    public HoverExecutionResult Hover(
        GameWindow window,
        ClickPoint point,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(point);
        ValidatePoint(point);
        EnsureSupportedSyncMode(point);

        lock (_clickGate)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var stopwatch = Stopwatch.StartNew();

            var (resolved, lParam) = PrepareTarget(window, point);

            _logger.Info(
                "hover.start",
                "Hover sequence started",
                new Dictionary<string, object?>
                {
                    ["hwnd"] = window.Info.HandleHex,
                    ["pid"] = window.Info.ProcessId,
                    ["point"] = point.Id,
                    ["x"] = resolved.X,
                    ["y"] = resolved.Y,
                    ["mode"] = point.SyncMode.ToString().ToLowerInvariant(),
                    ["settleMs"] = point.HoverSettleMs
                });

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                SendMessage(
                    window,
                    point.SyncMode,
                    ClickMessages.WmMouseMove,
                    IntPtr.Zero,
                    lParam,
                    cancellationToken);

                Sleep(point.HoverSettleMs, cancellationToken);
            }
            catch (GameProbeException ex)
            {
                _logger.Error(
                    "hover.failed",
                    ex.Message,
                    new Dictionary<string, object?>
                    {
                        ["hwnd"] = window.Info.HandleHex,
                        ["pid"] = window.Info.ProcessId,
                        ["point"] = point.Id,
                        ["kind"] = ex.Kind.ToString(),
                        ["win32"] = ex.Win32Error
                    });
                throw;
            }
            catch (OperationCanceledException)
            {
                _logger.Warn(
                    "hover.canceled",
                    "Hover sequence canceled",
                    new Dictionary<string, object?>
                    {
                        ["hwnd"] = window.Info.HandleHex,
                        ["point"] = point.Id
                    });
                throw;
            }

            stopwatch.Stop();

            _logger.Info(
                "hover.done",
                "Hover sequence completed",
                new Dictionary<string, object?>
                {
                    ["hwnd"] = window.Info.HandleHex,
                    ["pid"] = window.Info.ProcessId,
                    ["point"] = point.Id,
                    ["x"] = resolved.X,
                    ["y"] = resolved.Y,
                    ["durationMs"] = Math.Round(stopwatch.Elapsed.TotalMilliseconds, 1)
                });

            return new HoverExecutionResult(resolved.X, resolved.Y, stopwatch.Elapsed);
        }
    }

    private (ResolvedPoint Resolved, IntPtr LParam) PrepareTarget(
        GameWindow window,
        ClickPoint point)
    {
        RefreshWindow(window);

        var resolved = CoordinateResolver.Resolve(point, window.Info);
        if (resolved.IsOutsideClientArea)
        {
            throw new GameProbeException(
                GameProbeErrorKind.OutOfBounds,
                $"Point '{point.Id}' nằm ngoài client area: raw ({resolved.RawX:0.##},{resolved.RawY:0.##}), " +
                $"client {window.Info.ClientSizeText}.");
        }

        return (resolved, ClickMessages.MakeLParam(resolved.X, resolved.Y));
    }

    private static void EnsureSupportedSyncMode(ClickPoint point)
    {
        if (point.SyncMode == ClickSendMode.SendInput)
        {
            throw new GameProbeException(
                GameProbeErrorKind.Unsupported,
                "Chế độ sendInput chưa được bật trong P2. Dùng post hoặc send; sendInput sẽ được thêm sau khi có xác nhận rõ ràng.");
        }
    }

    private void RefreshWindow(GameWindow window)
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
                $"trước đó {expectedPid}); từ chối click.");
        }

        if (window.Info.IsMinimized)
        {
            throw new GameProbeException(
                GameProbeErrorKind.WindowUnavailable,
                "Cửa sổ đang minimized; không thể click.");
        }

        if (window.Info.ClientWidth <= 0 || window.Info.ClientHeight <= 0)
        {
            throw new GameProbeException(
                GameProbeErrorKind.WindowUnavailable,
                "Client area có kích thước bằng 0; không thể click.");
        }

        if (window.Info.IsHung)
        {
            _logger.Warn(
                "click.hung",
                "Target window reports as hung",
                new Dictionary<string, object?>
                {
                    ["hwnd"] = window.Info.HandleHex,
                    ["pid"] = window.Info.ProcessId
                });
        }
    }

    private void SendMessage(
        GameWindow window,
        ClickSendMode mode,
        uint message,
        IntPtr wParam,
        IntPtr lParam,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var result = mode switch
        {
            ClickSendMode.Post => _transport.PostMessage(
                window.Info.Handle,
                message,
                wParam,
                lParam),
            ClickSendMode.Send => _transport.SendMessage(
                window.Info.Handle,
                message,
                wParam,
                lParam,
                TimeSpan.FromMilliseconds(_options.SendTimeoutMs)),
            _ => throw new GameProbeException(
                GameProbeErrorKind.Unsupported,
                $"Send mode '{mode}' không được hỗ trợ.")
        };

        if (result.Success)
        {
            return;
        }

        var errorText = Win32ErrorText.Format(result.ErrorCode);
        throw new GameProbeException(
            result.ErrorCode == 1460 ? GameProbeErrorKind.Timeout : GameProbeErrorKind.SendFailed,
            $"{ClickMessages.GetMessageName(message)} thất bại bằng {mode} mode: {errorText}.",
            result.ErrorCode);
    }

    private void WaitForRateLimit(CancellationToken cancellationToken)
    {
        if (!_hasClicked || _options.MinimumClickSpacingMs == 0)
        {
            _hasClicked = true;
            _lastClickStarted = Stopwatch.GetTimestamp();
            return;
        }

        var elapsedMs = (Stopwatch.GetTimestamp() - _lastClickStarted) * 1000.0 / Stopwatch.Frequency;
        var remainingMs = _options.MinimumClickSpacingMs - elapsedMs;

        if (remainingMs > 0)
        {
            Sleep((int)Math.Ceiling(remainingMs), cancellationToken);
        }

        _lastClickStarted = Stopwatch.GetTimestamp();
    }

    private static void Sleep(int milliseconds, CancellationToken cancellationToken)
    {
        if (milliseconds <= 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return;
        }

        if (cancellationToken.WaitHandle.WaitOne(milliseconds))
        {
            cancellationToken.ThrowIfCancellationRequested();
        }
    }

    private static void ValidatePoint(ClickPoint point)
    {
        if (point.Count is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(point), "count phải nằm trong khoảng 1..100.");
        }

        if (point.HoldMs is < 0 or > 10000)
        {
            throw new ArgumentOutOfRangeException(nameof(point), "holdMs phải nằm trong khoảng 0..10000.");
        }

        if (point.IntervalMs is < 0 or > 600000)
        {
            throw new ArgumentOutOfRangeException(nameof(point), "intervalMs phải nằm trong khoảng 0..600000.");
        }

        if (point.HoverSettleMs is < 0 or > 10000)
        {
            throw new ArgumentOutOfRangeException(nameof(point), "hoverSettleMs phải nằm trong khoảng 0..10000.");
        }
    }
}
