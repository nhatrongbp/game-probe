using System.Diagnostics;
using GameProbe.Core.Capture;
using GameProbe.Core.Diagnostics;
using GameProbe.Core.Input;
using GameProbe.Core.Models;
using GameProbe.Core.Windows;

namespace GameProbe.Core.Scenario;

/// <summary>
/// Hardcoded-coordinate flow:
///   1. click relist, wait for the red buy button, capture the max-price region, cancel;
///   2. repeat until the max-price region differs from the first capture;
///   3. click the price value, then click buy (unless dry-run).
/// Only the price region is compared; no OCR and no game memory access.
/// </summary>
public sealed class PriceRefreshScenario
{
    private readonly IClickExecutor _click;
    private readonly IPixelReader _pixel;
    private readonly IRegionSource _regionSource;
    private readonly ISleeper _sleeper;
    private readonly IActionLogger _logger;
    private bool _warnedDesktopCapture;

    public PriceRefreshScenario(
        IClickExecutor click,
        IPixelReader pixel,
        IRegionSource regionSource,
        ISleeper? sleeper = null,
        IActionLogger? logger = null)
    {
        _click = click ?? throw new ArgumentNullException(nameof(click));
        _pixel = pixel ?? throw new ArgumentNullException(nameof(pixel));
        _regionSource = regionSource ?? throw new ArgumentNullException(nameof(regionSource));
        _sleeper = sleeper ?? ThreadSleeper.Instance;
        _logger = logger ?? NullActionLogger.Instance;
    }

    public PriceRefreshResult Run(
        GameWindow window,
        Profile profile,
        bool confirmBuy,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(profile);

        var options = profile.PriceRefresh
            ?? throw new GameProbeException(
                GameProbeErrorKind.InvalidConfiguration,
                "Profile không có cấu hình 'priceRefresh'.");

        var relist = RequireClickPoint(profile, options.RelistPointId, "relistPointId");
        var pricePoint = RequireClickPoint(profile, options.PricePointId, "pricePointId");
        var cancelPoint = RequireClickPoint(profile, options.CancelPointId, "cancelPointId");
        var buyPoint = RequireClickPoint(profile, options.BuyPointId, "buyPointId");
        var popupProbe = RequirePixelProbe(profile, options.PopupProbeId, "popupProbeId");
        var priceRegion = RequireRegion(profile, options.PriceRegionId, "priceRegionId");
        var guardProbe = string.IsNullOrWhiteSpace(options.RelistGuardProbeId)
            ? null
            : RequirePixelProbe(profile, options.RelistGuardProbeId, "relistGuardProbeId");

        RequireExpectedRgb(popupProbe, "popupProbeId");
        if (guardProbe is not null)
        {
            RequireExpectedRgb(guardProbe, "relistGuardProbeId");
        }

        var expectedPid = window.Info.ProcessId;
        var stopwatch = Stopwatch.StartNew();
        var attempts = 0;
        RegionCaptureResult? baseline = null;
        RegionCaptureResult? lastCurrent = null;
        RegionDiff? lastDiff = null;

        try
        {
            PrepareWindow(window, options, expectedPid);
            ClosePopupIfOpenAtStart(
                window,
                popupProbe,
                cancelPoint,
                options,
                cancellationToken);

            if (options.StartDelayMs > 0)
            {
                _logger.Info(
                    "price.start_delay",
                    "Waiting before the first relist click",
                    new Dictionary<string, object?>
                    {
                        ["delayMs"] = options.StartDelayMs
                    });
                _sleeper.Sleep(options.StartDelayMs, cancellationToken);
            }

            PrepareWindow(window, options, expectedPid);
            CheckRelistGuard(window, relist, guardProbe, options, cancellationToken);

            _logger.Info(
                "price.start",
                "Price refresh scenario started",
                new Dictionary<string, object?>
                {
                    ["hwnd"] = window.Info.HandleHex,
                    ["pid"] = window.Info.ProcessId,
                    ["maxAttempts"] = options.MaxAttempts,
                    ["confirmBuy"] = confirmBuy,
                    ["relist"] = relist.Id,
                    ["pricePoint"] = pricePoint.Id,
                    ["cancelPoint"] = cancelPoint.Id,
                    ["buyPoint"] = buyPoint.Id,
                    ["priceRegion"] = priceRegion.Id,
                    ["popupProbe"] = popupProbe.Id
                });

            Click(window, relist, cancellationToken);
            attempts = 1;

            WaitForPopup(window, popupProbe, options, cancellationToken);
            _sleeper.Sleep(options.SettleMs, cancellationToken);

            var resolvedRegion = ResolveRegion(window, priceRegion);
            baseline = CaptureStable(window, resolvedRegion, options, cancellationToken);
            _logger.Info(
                "price.baseline",
                "Captured baseline max-price region",
                CaptureLogData(window, baseline, attempts));

            CancelPopup(
                window,
                popupProbe,
                cancelPoint,
                options,
                cancellationToken);

            for (var attempt = 2; attempt <= options.MaxAttempts; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                _sleeper.Sleep(options.AttemptDelayMs, cancellationToken);
                PrepareWindow(window, options, expectedPid);
                CheckRelistGuard(window, relist, guardProbe, options, cancellationToken);

                Click(window, relist, cancellationToken);
                attempts = attempt;

                WaitForPopup(window, popupProbe, options, cancellationToken);
                _sleeper.Sleep(options.SettleMs, cancellationToken);

                lastCurrent = CaptureStable(
                    window,
                    resolvedRegion,
                    options,
                    cancellationToken);

                if (lastCurrent.Method != baseline.Method)
                {
                    throw new GameProbeException(
                        GameProbeErrorKind.CaptureFailed,
                        $"Capture method đổi từ {baseline.Method} sang {lastCurrent.Method}; " +
                        "không so sánh pixel để tránh false positive. " +
                        "Giữ cửa sổ game visible và không bị che rồi chạy lại.");
                }

                lastDiff = RegionComparer.Compare(
                    baseline,
                    lastCurrent,
                    options.ChannelTolerance,
                    options.MinChangedPixels);

                if (!lastDiff.Value.IsDifferent)
                {
                    _logger.Info(
                        "price.same",
                        "Max price matches the baseline",
                        CaptureLogData(window, lastCurrent, attempt, lastDiff));

                    CancelPopup(
                        window,
                        popupProbe,
                        cancelPoint,
                        options,
                        cancellationToken);
                    continue;
                }

                _logger.Info(
                    "price.changed",
                    "Max price changed",
                    CaptureLogData(window, lastCurrent, attempt, lastDiff));

                if (!confirmBuy)
                {
                    return new PriceRefreshResult(
                        PriceRefreshOutcome.DryRunChanged,
                        attempts,
                        baseline,
                        lastCurrent,
                        lastDiff,
                        stopwatch.Elapsed,
                        "Phát hiện giá tối đa đổi; dry-run nên chưa click giá/Mua.");
                }

                Click(window, pricePoint, cancellationToken);
                _sleeper.Sleep(options.PriceClickDelayMs, cancellationToken);
                Click(window, buyPoint, cancellationToken);

                _logger.Info(
                    "price.buy_clicked",
                    "Clicked the changed price and the buy button",
                    new Dictionary<string, object?>
                    {
                        ["hwnd"] = window.Info.HandleHex,
                        ["attempt"] = attempt,
                        ["changedPixels"] = lastDiff.Value.ChangedPixels,
                        ["maxChannelDelta"] = lastDiff.Value.MaxChannelDelta,
                        ["bounds"] = lastDiff.Value.BoundsText
                    });

                return new PriceRefreshResult(
                    PriceRefreshOutcome.Bought,
                    attempts,
                    baseline,
                    lastCurrent,
                    lastDiff,
                    stopwatch.Elapsed,
                    "Đã click giá mới và Mua cầu thủ.");
            }

            var message =
                $"Đã thử {attempts} lần nhưng giá tối đa chưa đổi " +
                $"(maxAttempts={options.MaxAttempts}).";

            _logger.Warn(
                "price.max_attempts",
                message,
                new Dictionary<string, object?>
                {
                    ["hwnd"] = window.Info.HandleHex,
                    ["attempts"] = attempts,
                    ["maxAttempts"] = options.MaxAttempts
                });

            return new PriceRefreshResult(
                PriceRefreshOutcome.MaxAttemptsReached,
                attempts,
                baseline,
                lastCurrent,
                lastDiff,
                stopwatch.Elapsed,
                message);
        }
        catch (OperationCanceledException)
        {
            _logger.Warn(
                "price.canceled",
                "Price refresh scenario canceled",
                new Dictionary<string, object?>
                {
                    ["hwnd"] = window.Info.HandleHex,
                    ["attempts"] = attempts
                });
            throw;
        }
        catch (GameProbeException ex)
        {
            _logger.Error(
                "price.failed",
                ex.Message,
                new Dictionary<string, object?>
                {
                    ["hwnd"] = window.Info.HandleHex,
                    ["attempts"] = attempts,
                    ["kind"] = ex.Kind.ToString(),
                    ["win32"] = ex.Win32Error
                });
            throw;
        }
    }

    private void PrepareWindow(
        GameWindow window,
        PriceRefreshOptions options,
        uint expectedPid)
    {
        if (!window.Refresh())
        {
            throw new GameProbeException(
                GameProbeErrorKind.WindowNotFound,
                "Cửa sổ target không còn tồn tại.");
        }

        var info = window.Info;

        if (expectedPid != 0 && info.ProcessId != expectedPid)
        {
            throw new GameProbeException(
                GameProbeErrorKind.WindowNotFound,
                $"HWND đã được dùng lại bởi process khác (PID {info.ProcessId}, " +
                $"trước đó {expectedPid}); từ chối chạy scenario.");
        }

        if (info.IsMinimized)
        {
            throw new GameProbeException(
                GameProbeErrorKind.WindowUnavailable,
                "Cửa sổ đang minimized; không thể chạy scenario.");
        }

        if (info.ClientWidth <= 0 || info.ClientHeight <= 0)
        {
            throw new GameProbeException(
                GameProbeErrorKind.WindowUnavailable,
                "Client area có kích thước bằng 0; không thể chạy scenario.");
        }

        if (options.ExpectedClientWidth > 0
            && info.ClientWidth != options.ExpectedClientWidth)
        {
            throw new GameProbeException(
                GameProbeErrorKind.InvalidConfiguration,
                $"Client width hiện tại {info.ClientWidth} khác cấu hình " +
                $"{options.ExpectedClientWidth}; cập nhật tọa độ/profile trước khi chạy.");
        }

        if (options.ExpectedClientHeight > 0
            && info.ClientHeight != options.ExpectedClientHeight)
        {
            throw new GameProbeException(
                GameProbeErrorKind.InvalidConfiguration,
                $"Client height hiện tại {info.ClientHeight} khác cấu hình " +
                $"{options.ExpectedClientHeight}; cập nhật tọa độ/profile trước khi chạy.");
        }

        if (options.ExpectedDpi != 0 && info.Dpi != options.ExpectedDpi)
        {
            throw new GameProbeException(
                GameProbeErrorKind.InvalidConfiguration,
                $"DPI hiện tại {info.Dpi} khác cấu hình {options.ExpectedDpi}; " +
                "tọa độ client có thể đã lệch.");
        }
    }

    private void ClosePopupIfOpenAtStart(
        GameWindow window,
        PixelProbeSpec popupProbe,
        ClickPoint cancelPoint,
        PriceRefreshOptions options,
        CancellationToken cancellationToken)
    {
        if (!IsPopupOpen(window, popupProbe))
        {
            return;
        }

        _logger.Warn(
            "price.popup_left_open",
            "Popup Mua cầu thủ đang mở sẵn; đóng trước khi bắt đầu",
            new Dictionary<string, object?>
            {
                ["hwnd"] = window.Info.HandleHex
            });

        CancelPopup(
            window,
            popupProbe,
            cancelPoint,
            options,
            cancellationToken);
    }

    private void CheckRelistGuard(
        GameWindow window,
        ClickPoint relist,
        PixelProbeSpec? guardProbe,
        PriceRefreshOptions options,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (guardProbe is null)
        {
            return;
        }

        // The green "Đăng ký lại" text is rendered only while the row is
        // hovered. Send a hover pulse before reading the guard, otherwise a
        // headless run (real cursor elsewhere) always sees the dark background.
        _click.Hover(window, relist, cancellationToken);

        var stopwatch = Stopwatch.StartNew();
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var result = _pixel.Read(window, guardProbe);
            if (result.MatchesExpectedRgb == true)
            {
                return;
            }

            if (stopwatch.ElapsedMilliseconds >= options.RelistGuardTimeoutMs)
            {
                throw new GameProbeException(
                    GameProbeErrorKind.InvalidConfiguration,
                    $"Guard '{guardProbe.Id}' không khớp tại ({result.X},{result.Y}) sau " +
                    $"{options.RelistGuardTimeoutMs} ms kể từ hover: " +
                    $"đọc được {result.Hex}, kỳ vọng {guardProbe.ExpectedRgb}. " +
                    "Có thể hàng 'Đăng ký lại' đã đổi hoặc màn hình không phải TTCT.");
            }

            _sleeper.Sleep(options.PollIntervalMs, cancellationToken);
        }
    }

    private bool IsPopupOpen(GameWindow window, PixelProbeSpec popupProbe)
    {
        var result = _pixel.Read(window, popupProbe);
        return result.MatchesExpectedRgb == true;
    }

    private void WaitForPopup(
        GameWindow window,
        PixelProbeSpec popupProbe,
        PriceRefreshOptions options,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var result = _pixel.Read(window, popupProbe);
            if (result.MatchesExpectedRgb == true)
            {
                _logger.Debug(
                    "price.popup_open",
                    "Buy dialog detected",
                    new Dictionary<string, object?>
                    {
                        ["hwnd"] = window.Info.HandleHex,
                        ["hex"] = result.Hex,
                        ["x"] = result.X,
                        ["y"] = result.Y,
                        ["elapsedMs"] = Math.Round(stopwatch.Elapsed.TotalMilliseconds, 1)
                    });
                return;
            }

            if (stopwatch.ElapsedMilliseconds >= options.PopupTimeoutMs)
            {
                throw new GameProbeException(
                    GameProbeErrorKind.Timeout,
                    $"Không thấy popup Mua cầu thủ sau {options.PopupTimeoutMs} ms. " +
                    $"Pixel tại ({result.X},{result.Y}) hiện là {result.Hex}, " +
                    $"kỳ vọng {popupProbe.ExpectedRgb}.");
            }

            _sleeper.Sleep(options.PollIntervalMs, cancellationToken);
        }
    }

    private bool WaitForPopupClosed(
        GameWindow window,
        PixelProbeSpec popupProbe,
        int timeoutMs,
        int pollIntervalMs,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!IsPopupOpen(window, popupProbe))
            {
                return true;
            }

            if (stopwatch.ElapsedMilliseconds >= timeoutMs)
            {
                return false;
            }

            _sleeper.Sleep(pollIntervalMs, cancellationToken);
        }
    }

    private void CancelPopup(
        GameWindow window,
        PixelProbeSpec popupProbe,
        ClickPoint cancelPoint,
        PriceRefreshOptions options,
        CancellationToken cancellationToken)
    {
        if (!IsPopupOpen(window, popupProbe))
        {
            return;
        }

        Click(window, cancelPoint, cancellationToken);
        if (WaitForPopupClosed(
                window,
                popupProbe,
                options.CloseTimeoutMs,
                options.PollIntervalMs,
                cancellationToken))
        {
            _logger.Info(
                "price.cancel",
                "Buy dialog canceled",
                new Dictionary<string, object?>
                {
                    ["hwnd"] = window.Info.HandleHex
                });
            return;
        }

        _logger.Warn(
            "price.cancel_retry",
            "Popup vẫn mở sau lần Huỷ đầu; thử lại một lần",
            new Dictionary<string, object?>
            {
                ["hwnd"] = window.Info.HandleHex
            });

        Click(window, cancelPoint, cancellationToken);
        if (WaitForPopupClosed(
                window,
                popupProbe,
                options.CloseTimeoutMs,
                options.PollIntervalMs,
                cancellationToken))
        {
            _logger.Info(
                "price.cancel",
                "Buy dialog canceled after retry",
                new Dictionary<string, object?>
                {
                    ["hwnd"] = window.Info.HandleHex
                });
            return;
        }

        throw new GameProbeException(
            GameProbeErrorKind.Timeout,
            $"Popup Mua cầu thủ không đóng sau {options.CloseTimeoutMs} ms " +
            "dù đã click Huỷ hai lần.");
    }

    private ResolvedRegion ResolveRegion(GameWindow window, RegionSpec region)
    {
        var resolved = RegionResolver.Resolve(region, window.Info);
        if (resolved.IsOutsideClientArea)
        {
            throw new GameProbeException(
                GameProbeErrorKind.OutOfBounds,
                $"Region '{region.Id}' nằm ngoài client area: {resolved}, " +
                $"client {window.Info.ClientSizeText}.");
        }

        return resolved;
    }

    private RegionCaptureResult CaptureStable(
        GameWindow window,
        ResolvedRegion region,
        PriceRefreshOptions options,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        RegionCaptureResult? previous = null;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var current = _regionSource.Capture(
                window.Handle,
                region.X,
                region.Y,
                region.Width,
                region.Height);

            if (current.Method == RegionCaptureMethod.DesktopBitBlt
                && !_warnedDesktopCapture)
            {
                _warnedDesktopCapture = true;
                _logger.Warn(
                    "price.desktop_capture",
                    "Window DC trả frame đen; đang dùng desktop capture. " +
                    "Giữ cửa sổ game visible và không bị cửa sổ khác che.");
            }

            if (!HasPriceContent(current))
            {
                previous = null;
            }
            else if (previous is not null
                     && RegionComparer.AreEqual(previous, current))
            {
                return current;
            }
            else
            {
                previous = current;
            }

            if (stopwatch.ElapsedMilliseconds >= options.StableTimeoutMs)
            {
                throw new GameProbeException(
                    GameProbeErrorKind.Timeout,
                    $"Vùng '{region}' không ổn định hoặc chưa có nội dung text sau " +
                    $"{options.StableTimeoutMs} ms; có thể popup còn animation, " +
                    "vùng crop sai, hoặc giá chưa được load.");
            }

            _sleeper.Sleep(options.StableFrameGapMs, cancellationToken);
        }
    }

    private static bool HasPriceContent(RegionCaptureResult capture)
    {
        // The max-price popup is light (#FCFCF7) with dark gray text. Requiring both
        // dark text pixels and a light background avoids capturing an all-black frame,
        // an empty placeholder, or a region that has not loaded yet.
        var darkPixels = 0;
        var lightPixels = 0;
        var minimumLightPixels = Math.Max(4, capture.PixelCount / 4);

        for (var i = 0; i < capture.Pixels.Length; i += 4)
        {
            var b = capture.Pixels[i];
            var g = capture.Pixels[i + 1];
            var r = capture.Pixels[i + 2];
            var brightest = Math.Max(r, Math.Max(g, b));
            var darkest = Math.Min(r, Math.Min(g, b));

            if (brightest < 160)
            {
                darkPixels++;
            }
            else if (darkest > 180)
            {
                lightPixels++;
            }

            if (darkPixels >= 10 && lightPixels >= minimumLightPixels)
            {
                return true;
            }
        }

        return false;
    }

    private void Click(
        GameWindow window,
        ClickPoint point,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _click.Click(window, point, cancellationToken);
    }

    private static ClickPoint RequireClickPoint(
        Profile profile,
        string id,
        string optionName)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new GameProbeException(
                GameProbeErrorKind.InvalidConfiguration,
                $"priceRefresh.{optionName} chưa được đặt.");
        }

        return profile.ClickPoints.FirstOrDefault(
                   item => item.Id.Equals(id, StringComparison.OrdinalIgnoreCase))
               ?? throw new GameProbeException(
                   GameProbeErrorKind.InvalidConfiguration,
                   $"priceRefresh.{optionName}='{id}' không có trong clickPoints.");
    }

    private static PixelProbeSpec RequirePixelProbe(
        Profile profile,
        string id,
        string optionName)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new GameProbeException(
                GameProbeErrorKind.InvalidConfiguration,
                $"priceRefresh.{optionName} chưa được đặt.");
        }

        return profile.PixelProbes.FirstOrDefault(
                   item => item.Id.Equals(id, StringComparison.OrdinalIgnoreCase))
               ?? throw new GameProbeException(
                   GameProbeErrorKind.InvalidConfiguration,
                   $"priceRefresh.{optionName}='{id}' không có trong pixelProbes.");
    }

    private static RegionSpec RequireRegion(
        Profile profile,
        string id,
        string optionName)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new GameProbeException(
                GameProbeErrorKind.InvalidConfiguration,
                $"priceRefresh.{optionName} chưa được đặt.");
        }

        return profile.Regions.FirstOrDefault(
                   item => item.Id.Equals(id, StringComparison.OrdinalIgnoreCase))
               ?? throw new GameProbeException(
                   GameProbeErrorKind.InvalidConfiguration,
                   $"priceRefresh.{optionName}='{id}' không có trong regions.");
    }

    private static void RequireExpectedRgb(PixelProbeSpec probe, string optionName)
    {
        if (string.IsNullOrWhiteSpace(probe.ExpectedRgb))
        {
            throw new GameProbeException(
                GameProbeErrorKind.InvalidConfiguration,
                $"priceRefresh.{optionName}='{probe.Id}' phải có expectedRgb.");
        }
    }

    private static Dictionary<string, object?> CaptureLogData(
        GameWindow window,
        RegionCaptureResult capture,
        int attempt,
        RegionDiff? diff = null)
    {
        var data = new Dictionary<string, object?>
        {
            ["hwnd"] = window.Info.HandleHex,
            ["attempt"] = attempt,
            ["x"] = capture.X,
            ["y"] = capture.Y,
            ["width"] = capture.Width,
            ["height"] = capture.Height,
            ["method"] = capture.Method.ToString(),
            ["hash"] = capture.HashHex
        };

        if (diff is not null)
        {
            data["changedPixels"] = diff.Value.ChangedPixels;
            data["maxChannelDelta"] = diff.Value.MaxChannelDelta;
            data["bounds"] = diff.Value.BoundsText;
            data["isDifferent"] = diff.Value.IsDifferent;
        }

        return data;
    }
}
