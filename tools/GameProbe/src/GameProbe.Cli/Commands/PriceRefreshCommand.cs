using System.Globalization;
using System.Text.Json;
using GameProbe.Core.Capture;
using GameProbe.Core.Diagnostics;
using GameProbe.Core.Input;
using GameProbe.Core.Profiles;
using GameProbe.Core.Scenario;
using GameProbe.Core.Windows;

namespace GameProbe.Cli.Commands;

internal static class PriceRefreshCommand
{
    public static int Execute(string[] args, IActionLogger logger)
    {
        try
        {
            return ExecuteCore(args, logger);
        }
        catch (CliUsageException ex)
        {
            Console.Error.WriteLine(ex.Message);
            HelpText.PrintPriceRefresh();
            return ExitCodes.Usage;
        }
    }

    private static int ExecuteCore(string[] args, IActionLogger logger)
    {
        var target = new TargetSelection();
        var confirmBuy = false;
        var dryRun = false;
        var json = false;
        int? maxAttempts = null;
        int? attemptDelayMs = null;
        int? startDelayMs = null;
        string? outDir = null;

        for (var i = 0; i < args.Length; i++)
        {
            if (target.TryConsume(args, ref i))
            {
                continue;
            }

            switch (args[i])
            {
                case "-h":
                case "--help":
                    HelpText.PrintPriceRefresh();
                    return ExitCodes.Success;

                case "--confirm-buy":
                    confirmBuy = true;
                    break;

                case "--dry-run":
                    dryRun = true;
                    break;

                case "--max-attempts":
                    maxAttempts = ParseInt(
                        RequireValue(args, ref i, "--max-attempts"),
                        "--max-attempts");
                    break;

                case "--delay":
                    attemptDelayMs = ParseInt(
                        RequireValue(args, ref i, "--delay"),
                        "--delay");
                    break;

                case "--start-delay":
                    startDelayMs = ParseInt(
                        RequireValue(args, ref i, "--start-delay"),
                        "--start-delay");
                    break;

                case "--out-dir":
                    outDir = RequireValue(args, ref i, "--out-dir");
                    break;

                case "--json":
                    json = true;
                    break;

                default:
                    throw new CliUsageException($"Option không hợp lệ: {args[i]}");
            }
        }

        if (confirmBuy && dryRun)
        {
            throw new CliUsageException("Không dùng --confirm-buy cùng lúc với --dry-run.");
        }

        if (target.ProfilePath is null)
        {
            throw new CliUsageException("Thiếu --profile <path>.");
        }

        var profile = new ProfileStore().Load(target.ProfilePath);
        var options = profile.PriceRefresh
            ?? throw new CliUsageException(
                $"Profile '{profile.Name}' không có cấu hình 'priceRefresh'.");

        if (maxAttempts is { } maxAttemptsValue)
        {
            options.MaxAttempts = maxAttemptsValue;
        }

        if (attemptDelayMs is { } delayValue)
        {
            options.AttemptDelayMs = delayValue;
        }

        if (startDelayMs is { } startDelayValue)
        {
            options.StartDelayMs = startDelayValue;
        }

        ProfileValidator.ThrowIfInvalid(profile);

        var locator = new WindowLocator(logger);
        var window = target.ResolveGameWindow(locator, profile);

        logger.Info(
            "price.cli_start",
            "Starting price refresh scenario",
            new Dictionary<string, object?>
            {
                ["hwnd"] = window.Info.HandleHex,
                ["pid"] = window.Info.ProcessId,
                ["confirmBuy"] = confirmBuy,
                ["maxAttempts"] = options.MaxAttempts,
                ["startDelayMs"] = options.StartDelayMs,
                ["attemptDelayMs"] = options.AttemptDelayMs
            });

        Console.Error.WriteLine(
            confirmBuy
                ? "Chế độ: MUA THẬT — sẽ click giá mới + Mua cầu thủ khi phát hiện giá đổi."
                : "Chế độ: DRY-RUN — chỉ phát hiện giá đổi, không click Mua.");
        Console.Error.WriteLine(
            $"Bắt đầu sau {options.StartDelayMs} ms; giữ cửa sổ game visible. Ctrl+C để huỷ.");

        using var cancellation = new ConsoleCancellation();
        var scenario = new PriceRefreshScenario(
            new ClickService(logger),
            new PixelProbe(logger),
            new GdiRegionSource(),
            ThreadSleeper.Instance,
            logger);

        var result = scenario.Run(
            window,
            profile,
            confirmBuy,
            cancellation.Token);

        var captureDirectory = outDir is null
            ? Path.Combine(
                GameProbePaths.CapturesDirectory,
                $"price-refresh-{DateTime.Now:yyyyMMdd-HHmmss-fff}")
            : Path.GetFullPath(outDir);

        Directory.CreateDirectory(captureDirectory);

        string? baselinePath = null;
        string? currentPath = null;

        if (result.Baseline is not null)
        {
            baselinePath = Path.Combine(captureDirectory, "baseline.bmp");
            BmpWriter.Write32Bgra(baselinePath, result.Baseline);
        }

        if (result.LastCurrent is not null)
        {
            currentPath = Path.Combine(captureDirectory, "current.bmp");
            BmpWriter.Write32Bgra(currentPath, result.LastCurrent);
        }

        logger.Info(
            "price.cli_done",
            "Price refresh scenario finished",
            new Dictionary<string, object?>
            {
                ["outcome"] = result.Outcome.ToString(),
                ["attempts"] = result.Attempts,
                ["durationMs"] = Math.Round(result.Duration.TotalMilliseconds, 1),
                ["baseline"] = baselinePath,
                ["current"] = currentPath
            });

        WriteOutput(
            window.Info,
            result,
            confirmBuy,
            captureDirectory,
            baselinePath,
            currentPath,
            json);

        return result.Outcome == PriceRefreshOutcome.MaxAttemptsReached
            ? ExitCodes.Timeout
            : ExitCodes.Success;
    }

    private static void WriteOutput(
        WindowInfo info,
        PriceRefreshResult result,
        bool confirmBuy,
        string captureDirectory,
        string? baselinePath,
        string? currentPath,
        bool json)
    {
        if (json)
        {
            var output = new PriceRefreshOutput(
                info.HandleHex,
                info.ProcessId,
                info.ProcessName,
                info.Title,
                info.ClientWidth,
                info.ClientHeight,
                result.Outcome.ToString(),
                result.Attempts,
                confirmBuy,
                result.Baseline?.HashHex,
                result.LastCurrent?.HashHex,
                result.LastDiff?.IsDifferent,
                result.LastDiff?.ChangedPixels,
                result.LastDiff?.MaxChannelDelta,
                result.LastDiff?.BoundsText,
                Math.Round(result.Duration.TotalMilliseconds, 1),
                result.Message,
                captureDirectory,
                baselinePath,
                currentPath);
            Console.WriteLine(JsonSerializer.Serialize(output, CommandJson.Pretty));
            return;
        }

        Console.WriteLine($"Kết quả: {result.Outcome}");
        Console.WriteLine($"  Attempts:    {result.Attempts}");
        Console.WriteLine($"  Confirm buy: {(confirmBuy ? "yes" : "no (dry-run)")}");
        Console.WriteLine(
            $"  Baseline:    {result.Baseline?.HashHex ?? "(chưa có)"}");
        Console.WriteLine(
            $"  Last:        {result.LastCurrent?.HashHex ?? "(chưa có)"}");

        if (result.LastDiff is { } diff)
        {
            Console.WriteLine(
                $"  Diff:        changed={diff.ChangedPixels}, " +
                $"maxDelta={diff.MaxChannelDelta}, bounds={diff.BoundsText}, " +
                $"different={diff.IsDifferent}");
        }

        Console.WriteLine($"  Duration:    {result.Duration.TotalMilliseconds:0.0} ms");
        Console.WriteLine($"  Captures:    {captureDirectory}");
        if (baselinePath is not null)
        {
            Console.WriteLine($"  Baseline:    {baselinePath}");
        }

        if (currentPath is not null)
        {
            Console.WriteLine($"  Current:     {currentPath}");
        }

        Console.WriteLine($"  Message:     {result.Message}");

        if (result.Outcome == PriceRefreshOutcome.DryRunChanged)
        {
            Console.WriteLine();
            Console.WriteLine(
                "Dry-run: giá đã đổi nhưng chưa click giá/Mua. " +
                "Chạy lại với --confirm-buy để mua.");
        }
    }

    private static string RequireValue(string[] args, ref int index, string option)
    {
        if (index + 1 >= args.Length)
        {
            throw new CliUsageException($"Thiếu giá trị cho {option}.");
        }

        var value = args[++index];
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new CliUsageException($"Giá trị cho {option} không được để trống.");
        }

        return value;
    }

    private static int ParseInt(string value, string option)
    {
        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            throw new CliUsageException($"Giá trị không hợp lệ cho {option}: {value}");
        }

        return parsed;
    }
}

internal sealed record PriceRefreshOutput(
    string Hwnd,
    uint Pid,
    string Process,
    string Title,
    int ClientWidth,
    int ClientHeight,
    string Outcome,
    int Attempts,
    bool ConfirmBuy,
    string? BaselineHash,
    string? CurrentHash,
    bool? IsDifferent,
    int? ChangedPixels,
    int? MaxChannelDelta,
    string? Bounds,
    double DurationMs,
    string Message,
    string CaptureDirectory,
    string? BaselinePath,
    string? CurrentPath);
