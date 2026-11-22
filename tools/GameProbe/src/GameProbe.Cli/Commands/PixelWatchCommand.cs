using System.Globalization;
using System.Text.Json;
using GameProbe.Core.Capture;
using GameProbe.Core.Diagnostics;
using GameProbe.Core.Profiles;
using GameProbe.Core.Windows;

namespace GameProbe.Cli.Commands;

internal static class PixelWatchCommand
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
            HelpText.PrintPixelWatch();
            return ExitCodes.Usage;
        }
    }

    private static int ExecuteCore(string[] args, IActionLogger logger)
    {
        var options = new PixelOptions();
        int? intervalOverride = null;
        int? durationSeconds = null;

        for (var i = 0; i < args.Length; i++)
        {
            if (options.TryConsume(args, ref i))
            {
                continue;
            }

            switch (args[i])
            {
                case "--interval":
                    intervalOverride = ParseInt(
                        RequireValue(args, ref i, "--interval"),
                        "--interval");
                    if (intervalOverride is < PixelWatcher.MinimumIntervalMs or > 600000)
                    {
                        throw new CliUsageException(
                            $"--interval phải nằm trong khoảng {PixelWatcher.MinimumIntervalMs}..600000 ms.");
                    }

                    break;

                case "--duration":
                    durationSeconds = ParseInt(
                        RequireValue(args, ref i, "--duration"),
                        "--duration");
                    if (durationSeconds is < 1 or > 86400)
                    {
                        throw new CliUsageException("--duration phải nằm trong khoảng 1..86400 giây.");
                    }

                    break;

                default:
                    throw new CliUsageException($"Option không hợp lệ: {args[i]}");
            }
        }

        if (options.HelpRequested)
        {
            HelpText.PrintPixelWatch();
            return ExitCodes.Success;
        }

        options.Validate();

        var profile = options.LoadProfile();
        var plan = options.BuildPlan(profile);
        var intervalMs = intervalOverride ?? plan.DefaultWatchIntervalMs;
        var duration = durationSeconds is { } seconds
            ? TimeSpan.FromSeconds(seconds)
            : (TimeSpan?)null;

        var locator = new WindowLocator(logger);
        var window = options.Target.ResolveGameWindow(locator, profile);
        var probe = new PixelProbe(logger);
        var watcher = new PixelWatcher(probe, logger);

        Console.Error.WriteLine(
            $"Watching {plan.DescribeTarget()} on {window.Info.HandleHex} " +
            $"({window.Info.ClientSizeText}) every {intervalMs} ms" +
            (duration is { } limit ? $" for {limit.TotalSeconds:0} s" : "") +
            ". Ctrl+C để dừng.");

        using var cancellation = new ConsoleCancellation();

        var summary = plan.Spec is not null
            ? watcher.Run(
                window,
                plan.Spec,
                intervalMs,
                duration,
                sample => WriteSample(window.Info, sample, options.Json),
                cancellation.Token)
            : watcher.Run(
                window,
                plan.Request!,
                intervalMs,
                duration,
                sample => WriteSample(window.Info, sample, options.Json),
                cancellation.Token);

        Console.Error.WriteLine(
            $"Đã dừng watch: {summary.SampleCount} mẫu, {summary.ChangeCount} thay đổi, " +
            $"{summary.Duration.TotalMilliseconds:0} ms" +
            (summary.Canceled ? " (Ctrl+C)." : "."));

        return ExitCodes.Success;
    }

    private static void WriteSample(WindowInfo info, PixelWatchSample sample, bool json)
    {
        if (json)
        {
            var output = new PixelWatchOutput(
                info.HandleHex,
                info.ProcessId,
                sample.Result.X,
                sample.Result.Y,
                sample.Result.Hex,
                sample.Result.Color.R,
                sample.Result.Color.G,
                sample.Result.Color.B,
                sample.Result.ColorClass,
                sample.Result.ColorClassName,
                sample.Result.CaptureMethod.ToString(),
                sample.Result.Tolerance,
                sample.Result.ExpectedClass,
                sample.Result.MatchesExpectedClass,
                sample.Result.ExpectedRgb,
                sample.Result.MatchesExpectedRgb,
                sample.Result.IsExactRgbMatch,
                sample.Changed,
                sample.Previous?.Hex,
                sample.Previous?.ColorClass,
                sample.Timestamp);

            Console.WriteLine(JsonSerializer.Serialize(output, CommandJson.Line));
            return;
        }

        var previous = sample.Previous is null
            ? ""
            : $" (previous {sample.Previous.Hex} class={sample.Previous.ColorClass})";

        Console.WriteLine(
            $"[{sample.Timestamp:HH:mm:ss.fff}] {sample.Result.Hex} " +
            $"RGB{sample.Result.RgbText} class={sample.Result.ColorClass} " +
            $"({sample.Result.ColorClassName}) via {sample.Result.CaptureMethod}{previous}");
    }

    private static int ParseInt(string text, string option)
    {
        if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
        {
            throw new CliUsageException($"Giá trị cho {option} không phải số nguyên: {text}");
        }

        return value;
    }

    private static string RequireValue(string[] args, ref int index, string option)
    {
        if (index + 1 >= args.Length)
        {
            throw new CliUsageException($"Thiếu giá trị cho {option}.");
        }

        return args[++index];
    }
}

internal sealed record PixelWatchOutput(
    string Hwnd,
    uint Pid,
    int X,
    int Y,
    string Hex,
    int R,
    int G,
    int B,
    int ColorClass,
    string ColorClassName,
    string Method,
    int Tolerance,
    int? ExpectedClass,
    bool? MatchesExpectedClass,
    string? ExpectedRgb,
    bool? MatchesExpectedRgb,
    bool? ExactRgbMatch,
    bool Changed,
    string? PreviousHex,
    int? PreviousClass,
    DateTimeOffset Timestamp);
