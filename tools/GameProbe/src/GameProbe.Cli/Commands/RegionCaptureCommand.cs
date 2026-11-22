using System.Text.Json;
using GameProbe.Core.Capture;
using GameProbe.Core.Diagnostics;
using GameProbe.Core.Input;
using GameProbe.Core.Models;
using GameProbe.Core.Profiles;
using GameProbe.Core.Windows;

namespace GameProbe.Cli.Commands;

internal static class RegionCaptureCommand
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
            HelpText.PrintRegionCapture();
            return ExitCodes.Usage;
        }
    }

    private static int ExecuteCore(string[] args, IActionLogger logger)
    {
        var target = new TargetSelection();
        var options = new RegionOptions();

        for (var i = 0; i < args.Length; i++)
        {
            if (target.TryConsume(args, ref i) || options.TryConsume(args, ref i))
            {
                continue;
            }

            throw new CliUsageException($"Option không hợp lệ: {args[i]}");
        }

        if (options.HelpRequested)
        {
            HelpText.PrintRegionCapture();
            return ExitCodes.Success;
        }

        options.Validate();

        var profile = target.LoadProfileIfSpecified();
        var region = options.BuildRegion(profile);

        var locator = new WindowLocator(logger);
        var window = target.ResolveGameWindow(locator, profile);
        var resolved = RegionResolver.Resolve(region, window.Info);

        if (resolved.IsOutsideClientArea)
        {
            throw new GameProbeException(
                GameProbeErrorKind.OutOfBounds,
                $"Region '{region.Id}' nằm ngoài client area: {resolved}, " +
                $"client {window.Info.ClientSizeText}.");
        }

        var capture = new GdiRegionSource().Capture(
            window.Handle,
            resolved.X,
            resolved.Y,
            resolved.Width,
            resolved.Height);

        var outputPath = options.OutPath;
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            outputPath = Path.Combine(
                GameProbePaths.CapturesDirectory,
                $"region-{DateTime.Now:yyyyMMdd-HHmmss-fff}.bmp");
        }

        BmpWriter.Write32Bgra(outputPath, capture);
        var fullOutputPath = Path.GetFullPath(outputPath);

        logger.Info(
            "region.capture",
            "Region captured",
            new Dictionary<string, object?>
            {
                ["hwnd"] = window.Info.HandleHex,
                ["pid"] = window.Info.ProcessId,
                ["region"] = region.Id,
                ["x"] = capture.X,
                ["y"] = capture.Y,
                ["width"] = capture.Width,
                ["height"] = capture.Height,
                ["method"] = capture.Method.ToString(),
                ["hash"] = capture.HashHex,
                ["out"] = fullOutputPath
            });

        WriteOutput(window.Info, region, capture, fullOutputPath, options.Json);
        return ExitCodes.Success;
    }

    private static void WriteOutput(
        WindowInfo info,
        RegionSpec region,
        RegionCaptureResult capture,
        string outputPath,
        bool json)
    {
        if (json)
        {
            var output = new RegionCaptureOutput(
                info.HandleHex,
                info.ProcessId,
                info.ProcessName,
                info.Title,
                info.ClientWidth,
                info.ClientHeight,
                region.Id,
                region.Name,
                capture.X,
                capture.Y,
                capture.Width,
                capture.Height,
                capture.Method.ToString(),
                capture.HashHex,
                outputPath,
                capture.CapturedAt);

            Console.WriteLine(JsonSerializer.Serialize(output, CommandJson.Pretty));
            return;
        }

        Console.WriteLine($"Region '{region.Id}' ({region.Name})");
        Console.WriteLine($"  Window:      {info.HandleHex} pid {info.ProcessId} {info.ProcessName}");
        Console.WriteLine($"  Client:      {info.ClientSizeText}");
        Console.WriteLine($"  Resolved:    ({capture.X},{capture.Y}) {capture.Width}x{capture.Height}");
        Console.WriteLine($"  Capture:     {capture.Method}, hash {capture.HashHex}");
        Console.WriteLine($"  Saved:       {outputPath}");
    }
}

internal sealed record RegionCaptureOutput(
    string Hwnd,
    uint Pid,
    string Process,
    string Title,
    int ClientWidth,
    int ClientHeight,
    string Region,
    string Name,
    int X,
    int Y,
    int Width,
    int Height,
    string Method,
    string Hash,
    string Path,
    DateTimeOffset Timestamp);
