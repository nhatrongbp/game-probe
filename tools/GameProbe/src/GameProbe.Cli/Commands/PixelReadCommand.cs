using System.Text.Json;
using GameProbe.Core.Capture;
using GameProbe.Core.Diagnostics;
using GameProbe.Core.Profiles;
using GameProbe.Core.Windows;

namespace GameProbe.Cli.Commands;

internal static class PixelReadCommand
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
            HelpText.PrintPixel();
            return ExitCodes.Usage;
        }
    }

    private static int ExecuteCore(string[] args, IActionLogger logger)
    {
        var options = new PixelOptions();

        for (var i = 0; i < args.Length; i++)
        {
            if (options.TryConsume(args, ref i))
            {
                continue;
            }

            throw new CliUsageException($"Option không hợp lệ: {args[i]}");
        }

        if (options.HelpRequested)
        {
            HelpText.PrintPixel();
            return ExitCodes.Success;
        }

        options.Validate();

        var profile = options.LoadProfile();
        var plan = options.BuildPlan(profile);

        var locator = new WindowLocator(logger);
        var window = options.Target.ResolveGameWindow(locator, profile);
        var probe = new PixelProbe(logger);
        var result = plan.Read(probe, window);

        logger.Info(
            "pixel.read",
            "Pixel read completed",
            new Dictionary<string, object?>
            {
                ["hwnd"] = window.Info.HandleHex,
                ["pid"] = window.Info.ProcessId,
                ["x"] = result.X,
                ["y"] = result.Y,
                ["hex"] = result.Hex,
                ["class"] = result.ColorClass,
                ["method"] = result.CaptureMethod.ToString(),
                ["matchesClass"] = result.MatchesExpectedClass,
                ["matchesRgb"] = result.MatchesExpectedRgb,
                ["exactRgb"] = result.IsExactRgbMatch
            });

        WriteOutput(window.Info, plan, result, options.Json);
        return ExitCodes.Success;
    }

    private static void WriteOutput(
        WindowInfo info,
        PixelReadPlan plan,
        PixelReadResult result,
        bool json)
    {
        if (json)
        {
            var output = new PixelOutput(
                info.HandleHex,
                info.ProcessId,
                info.ProcessName,
                info.Title,
                info.ClientWidth,
                info.ClientHeight,
                result.X,
                result.Y,
                result.Color.R,
                result.Color.G,
                result.Color.B,
                result.Hex,
                result.ColorClass,
                result.ColorClassName,
                result.CaptureMethod.ToString(),
                result.Tolerance,
                result.ExpectedClass,
                result.MatchesExpectedClass,
                result.ExpectedRgb,
                result.MatchesExpectedRgb,
                result.IsExactRgbMatch,
                result.Timestamp);

            Console.WriteLine(JsonSerializer.Serialize(output, CommandJson.Pretty));
            return;
        }

        Console.WriteLine(
            $"Pixel ({result.X}, {result.Y}) — {result.Hex} RGB{result.RgbText} " +
            $"class={result.ColorClass} ({result.ColorClassName}) via {result.CaptureMethod}");
        Console.WriteLine($"  Window:      {info.HandleHex} pid {info.ProcessId} {info.ProcessName} ({info.ClientSizeText})");
        Console.WriteLine($"  Configured:  x={plan.DescribeX()}, y={plan.DescribeY()}");
        Console.WriteLine($"  Tolerance:   {result.Tolerance}");

        if (result.ExpectedClass is null && result.ExpectedRgb is null)
        {
            Console.WriteLine("  Expected:    (không đặt)");
            return;
        }

        var parts = new List<string>();
        if (result.ExpectedClass is { } expectedClass)
        {
            parts.Add(
                $"class={expectedClass} {(result.MatchesExpectedClass == true ? "match" : "MISMATCH")}");
        }

        if (result.ExpectedRgb is { } expectedRgb)
        {
            parts.Add(
                $"rgb={expectedRgb} {(result.MatchesExpectedRgb == true ? "match" : "MISMATCH")}" +
                (result.IsExactRgbMatch == true ? " (exact)" : ""));
        }

        Console.WriteLine($"  Expected:    {string.Join("; ", parts)}");
    }
}

internal sealed record PixelOutput(
    string Hwnd,
    uint Pid,
    string Process,
    string Title,
    int ClientWidth,
    int ClientHeight,
    int X,
    int Y,
    int R,
    int G,
    int B,
    string Hex,
    int ColorClass,
    string ColorClassName,
    string Method,
    int Tolerance,
    int? ExpectedClass,
    bool? MatchesExpectedClass,
    string? ExpectedRgb,
    bool? MatchesExpectedRgb,
    bool? ExactRgbMatch,
    DateTimeOffset Timestamp);
