using System.Text.Json;
using GameProbe.Core.Diagnostics;
using GameProbe.Core.Input;
using GameProbe.Core.Models;
using GameProbe.Core.Profiles;
using GameProbe.Core.Windows;

namespace GameProbe.Cli.Commands;

internal static class ClickCommand
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
            HelpText.PrintClick();
            return ExitCodes.Usage;
        }
    }

    private static int ExecuteCore(string[] args, IActionLogger logger)
    {
        var target = new TargetSelection();
        string? pointId = null;
        var json = false;
        var dryRun = false;
        var noHover = false;
        var hoverOnly = false;

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
                    HelpText.PrintClick();
                    return ExitCodes.Success;

                case "--point":
                case "-p":
                    pointId = RequireValue(args, ref i, args[i]);
                    break;

                case "--json":
                    json = true;
                    break;

                case "--dry-run":
                    dryRun = true;
                    break;

                case "--no-hover":
                    noHover = true;
                    break;

                case "--hover-only":
                    hoverOnly = true;
                    break;

                default:
                    throw new CliUsageException($"Option không hợp lệ: {args[i]}");
            }
        }

        if (target.ProfilePath is null)
        {
            throw new CliUsageException("Thiếu --profile <path>.");
        }

        if (string.IsNullOrWhiteSpace(pointId))
        {
            throw new CliUsageException("Thiếu --point <id>.");
        }

        if (hoverOnly && dryRun)
        {
            throw new CliUsageException("Không dùng --hover-only cùng lúc với --dry-run.");
        }

        if (hoverOnly && noHover)
        {
            throw new CliUsageException("Không dùng --hover-only cùng lúc với --no-hover.");
        }

        var profile = new ProfileStore().Load(target.ProfilePath);
        var point = profile.ClickPoints.FirstOrDefault(
            item => item.Id.Equals(pointId, StringComparison.OrdinalIgnoreCase));

        if (point is null)
        {
            throw new CliUsageException(
                $"Profile '{profile.Name}' không có click point '{pointId}'.");
        }

        if (noHover)
        {
            point.Hover = false;
        }

        var locator = new WindowLocator(logger);
        var window = target.ResolveGameWindow(locator, profile);
        var resolved = CoordinateResolver.Resolve(point, window.Info);

        if (resolved.IsOutsideClientArea)
        {
            throw new GameProbeException(
                GameProbeErrorKind.OutOfBounds,
                $"Point '{point.Id}' nằm ngoài client area: raw " +
                $"({resolved.RawX:0.##},{resolved.RawY:0.##}), client {window.Info.ClientSizeText}.");
        }

        if (dryRun)
        {
            WriteOutput(
                window.Info,
                point,
                resolved.X,
                resolved.Y,
                dryRun: true,
                sentClicks: 0,
                duration: TimeSpan.Zero,
                json);

            logger.Info(
                "click.dryrun",
                "Click dry run completed",
                new Dictionary<string, object?>
                {
                    ["hwnd"] = window.Info.HandleHex,
                    ["point"] = point.Id,
                    ["x"] = resolved.X,
                    ["y"] = resolved.Y
                });
            return ExitCodes.Success;
        }

        using var cancellation = new ConsoleCancellation();

        if (hoverOnly)
        {
            var hoverService = new ClickService(logger);
            var hoverResult = hoverService.Hover(window, point, cancellation.Token);
            WriteHoverOutput(window.Info, point, hoverResult, json);
            return ExitCodes.Success;
        }

        var service = new ClickService(logger);
        var result = service.Click(window, point, cancellation.Token);

        WriteOutput(
            window.Info,
            point,
            result.X,
            result.Y,
            dryRun: false,
            result.SentClicks,
            result.Duration,
            json);
        return ExitCodes.Success;
    }

    private static void WriteOutput(
        WindowInfo info,
        ClickPoint point,
        int x,
        int y,
        bool dryRun,
        int sentClicks,
        TimeSpan duration,
        bool json)
    {
        if (json)
        {
            var output = new ClickOutput(
                info.HandleHex,
                info.ProcessId,
                info.ProcessName,
                info.Title,
                info.ClientWidth,
                info.ClientHeight,
                new ClickPointOutput(
                    point.Id,
                    point.Name,
                    FormatHelpers.DescribeCoordinate(point.X),
                    FormatHelpers.DescribeCoordinate(point.Y),
                    point.Button.ToString().ToLowerInvariant(),
                    point.Count,
                    point.SyncMode.ToString().ToLowerInvariant(),
                    point.Hover,
                    point.WParamMode.ToString().ToLowerInvariant()),
                x,
                y,
                dryRun,
                sentClicks,
                Math.Round(duration.TotalMilliseconds, 1));

            Console.WriteLine(JsonSerializer.Serialize(output, CommandJson.Pretty));
            return;
        }

        Console.WriteLine(
            dryRun
                ? $"Dry run — point '{point.Id}' ({point.Name})"
                : $"Click OK — point '{point.Id}' ({point.Name})");
        Console.WriteLine($"  Window:      {info.HandleHex} pid {info.ProcessId} {info.ProcessName}");
        Console.WriteLine($"  Client:      {info.ClientSizeText}");
        Console.WriteLine(
            $"  Configured:  x={FormatHelpers.DescribeCoordinate(point.X)}, " +
            $"y={FormatHelpers.DescribeCoordinate(point.Y)}");
        Console.WriteLine($"  Resolved:    ({x}, {y})");
        Console.WriteLine(
            $"  Button:      {point.Button.ToString().ToLowerInvariant()}, " +
            $"count={point.Count}, mode={point.SyncMode.ToString().ToLowerInvariant()}, " +
            $"hover={YesNo(point.Hover)}, wParam={point.WParamMode.ToString().ToLowerInvariant()}");

        if (dryRun)
        {
            Console.WriteLine("  Sent:        0 (dry run, không gửi message)");
        }
        else
        {
            Console.WriteLine(
                $"  Sent:        {sentClicks} click(s) in {duration.TotalMilliseconds:0.0} ms");
        }
    }

    private static void WriteHoverOutput(
        WindowInfo info,
        ClickPoint point,
        HoverExecutionResult result,
        bool json)
    {
        if (json)
        {
            var output = new HoverOutput(
                info.HandleHex,
                info.ProcessId,
                info.ProcessName,
                info.Title,
                info.ClientWidth,
                info.ClientHeight,
                new ClickPointOutput(
                    point.Id,
                    point.Name,
                    FormatHelpers.DescribeCoordinate(point.X),
                    FormatHelpers.DescribeCoordinate(point.Y),
                    point.Button.ToString().ToLowerInvariant(),
                    point.Count,
                    point.SyncMode.ToString().ToLowerInvariant(),
                    point.Hover,
                    point.WParamMode.ToString().ToLowerInvariant()),
                result.X,
                result.Y,
                point.HoverSettleMs,
                Math.Round(result.Duration.TotalMilliseconds, 1));

            Console.WriteLine(JsonSerializer.Serialize(output, CommandJson.Pretty));
            return;
        }

        Console.WriteLine($"Hover OK — point '{point.Id}' ({point.Name})");
        Console.WriteLine($"  Window:      {info.HandleHex} pid {info.ProcessId} {info.ProcessName}");
        Console.WriteLine($"  Client:      {info.ClientSizeText}");
        Console.WriteLine(
            $"  Configured:  x={FormatHelpers.DescribeCoordinate(point.X)}, " +
            $"y={FormatHelpers.DescribeCoordinate(point.Y)}");
        Console.WriteLine($"  Resolved:    ({result.X}, {result.Y})");
        Console.WriteLine(
            $"  Sent:        WM_MOUSEMOVE, mode={point.SyncMode.ToString().ToLowerInvariant()}");
        Console.WriteLine($"  Settle:      {point.HoverSettleMs} ms");
        Console.WriteLine($"  Duration:    {result.Duration.TotalMilliseconds:0.0} ms");
    }

    private static string YesNo(bool value) => value ? "yes" : "no";

    private static string RequireValue(string[] args, ref int index, string option)
    {
        if (index + 1 >= args.Length)
        {
            throw new CliUsageException($"Thiếu giá trị cho {option}.");
        }

        return args[++index];
    }
}

internal sealed record ClickPointOutput(
    string Id,
    string Name,
    string ConfiguredX,
    string ConfiguredY,
    string Button,
    int Count,
    string SyncMode,
    bool Hover,
    string WParamMode);

internal sealed record ClickOutput(
    string Hwnd,
    uint Pid,
    string Process,
    string Title,
    int ClientWidth,
    int ClientHeight,
    ClickPointOutput Point,
    int X,
    int Y,
    bool DryRun,
    int SentClicks,
    double DurationMs);

internal sealed record HoverOutput(
    string Hwnd,
    uint Pid,
    string Process,
    string Title,
    int ClientWidth,
    int ClientHeight,
    ClickPointOutput Point,
    int X,
    int Y,
    int SettleMs,
    double DurationMs);
