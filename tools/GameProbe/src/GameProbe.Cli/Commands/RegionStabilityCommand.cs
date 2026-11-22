using System.Text.Json;
using GameProbe.Core.Capture;
using GameProbe.Core.Diagnostics;
using GameProbe.Core.Input;
using GameProbe.Core.Models;
using GameProbe.Core.Windows;

namespace GameProbe.Cli.Commands;

internal static class RegionStabilityCommand
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
            HelpText.PrintRegionStability();
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
            HelpText.PrintRegionStability();
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

        var source = new GdiRegionSource();
        var samples = new List<RegionStabilitySample>(options.Count);
        RegionCaptureResult? reference = null;
        using var cancellation = new ConsoleCancellation();

        for (var i = 0; i < options.Count; i++)
        {
            cancellation.Token.ThrowIfCancellationRequested();

            var capture = source.Capture(
                window.Handle,
                resolved.X,
                resolved.Y,
                resolved.Width,
                resolved.Height);

            reference ??= capture;

            var diff = RegionComparer.Compare(reference, capture);
            samples.Add(new RegionStabilitySample(
                i + 1,
                capture.HashHex,
                capture.Method.ToString(),
                diff.ChangedPixels,
                diff.MaxChannelDelta,
                diff.IsDifferent,
                diff.BoundsText));

            if (i + 1 < options.Count
                && cancellation.Token.WaitHandle.WaitOne(options.IntervalMs))
            {
                cancellation.Token.ThrowIfCancellationRequested();
            }
        }

        var first = reference
            ?? throw new InvalidOperationException("Không có frame nào được chụp.");

        var changedSamples = samples.Count(sample => sample.IsDifferent);
        var maxChangedPixels = samples.Max(sample => sample.ChangedPixels);

        string? outputPath = null;
        if (!string.IsNullOrWhiteSpace(options.OutPath))
        {
            outputPath = Path.GetFullPath(options.OutPath);
            BmpWriter.Write32Bgra(outputPath, first);
        }

        logger.Info(
            "region.stability",
            "Region stability check completed",
            new Dictionary<string, object?>
            {
                ["hwnd"] = window.Info.HandleHex,
                ["pid"] = window.Info.ProcessId,
                ["region"] = region.Id,
                ["samples"] = samples.Count,
                ["changedSamples"] = changedSamples,
                ["maxChangedPixels"] = maxChangedPixels,
                ["firstHash"] = first.HashHex,
                ["out"] = outputPath
            });

        WriteOutput(
            window.Info,
            region,
            resolved,
            first,
            samples,
            changedSamples,
            maxChangedPixels,
            outputPath,
            options.Json);

        return ExitCodes.Success;
    }

    private static void WriteOutput(
        WindowInfo info,
        RegionSpec region,
        ResolvedRegion resolved,
        RegionCaptureResult reference,
        IReadOnlyList<RegionStabilitySample> samples,
        int changedSamples,
        int maxChangedPixels,
        string? outputPath,
        bool json)
    {
        if (json)
        {
            var output = new RegionStabilityOutput(
                info.HandleHex,
                info.ProcessId,
                info.ProcessName,
                info.Title,
                info.ClientWidth,
                info.ClientHeight,
                region.Id,
                region.Name,
                resolved.X,
                resolved.Y,
                resolved.Width,
                resolved.Height,
                reference.Method.ToString(),
                reference.HashHex,
                samples,
                samples.Count,
                changedSamples,
                maxChangedPixels,
                outputPath);
            Console.WriteLine(JsonSerializer.Serialize(output, CommandJson.Pretty));
            return;
        }

        Console.WriteLine($"Region '{region.Id}' ({region.Name}) — {resolved.Width}x{resolved.Height}");
        Console.WriteLine($"  Window:      {info.HandleHex} pid {info.ProcessId} {info.ProcessName}");
        Console.WriteLine($"  Resolved:    ({resolved.X},{resolved.Y})");
        Console.WriteLine($"  Method:      {reference.Method}, first hash {reference.HashHex}");
        Console.WriteLine();

        foreach (var sample in samples)
        {
            var status = sample.IsDifferent
                ? $"DIFF changed={sample.ChangedPixels} maxDelta={sample.MaxChannelDelta} bounds={sample.Bounds}"
                : "same";
            Console.WriteLine($"  #{sample.Index,-3} {sample.Hash} {status}");
        }

        Console.WriteLine();
        Console.WriteLine(
            $"Summary: {samples.Count} samples, {changedSamples} different, " +
            $"max changed pixels = {maxChangedPixels}.");
        if (changedSamples > 0)
        {
            Console.WriteLine(
                "Cảnh báo: exact/pixel diff đang dao động; tăng channelTolerance " +
                "hoặc minChangedPixels, hoặc thu hẹp vùng crop.");
        }

        if (outputPath is not null)
        {
            Console.WriteLine($"Saved first frame: {outputPath}");
        }
    }
}

internal sealed record RegionStabilitySample(
    int Index,
    string Hash,
    string Method,
    int ChangedPixels,
    int MaxChannelDelta,
    bool IsDifferent,
    string Bounds);

internal sealed record RegionStabilityOutput(
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
    string FirstHash,
    IReadOnlyList<RegionStabilitySample> Samples,
    int SampleCount,
    int ChangedSamples,
    int MaxChangedPixels,
    string? Path);
