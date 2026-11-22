using System.Globalization;
using System.Text.Json;
using GameProbe.Core.Diagnostics;
using GameProbe.Core.Input;
using GameProbe.Core.Windows;

namespace GameProbe.Cli.Commands;

internal static class CaptureCommand
{
    private const int DefaultTimeoutSeconds = 30;

    public static int Execute(string[] args, IActionLogger logger)
    {
        try
        {
            return ExecuteCore(args, logger);
        }
        catch (CliUsageException ex)
        {
            Console.Error.WriteLine(ex.Message);
            HelpText.PrintCapture();
            return ExitCodes.Usage;
        }
    }

    private static int ExecuteCore(string[] args, IActionLogger logger)
    {
        var target = new TargetSelection();
        var timeoutSeconds = DefaultTimeoutSeconds;
        var json = false;

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
                    HelpText.PrintCapture();
                    return ExitCodes.Success;

                case "--timeout":
                    var timeoutText = RequireValue(args, ref i, "--timeout");
                    if (!int.TryParse(
                            timeoutText,
                            NumberStyles.Integer,
                            CultureInfo.InvariantCulture,
                            out timeoutSeconds)
                        || timeoutSeconds is < 1 or > 600)
                    {
                        throw new CliUsageException("--timeout phải nằm trong khoảng 1..600 giây.");
                    }

                    break;

                case "--json":
                    json = true;
                    break;

                default:
                    throw new CliUsageException($"Option không hợp lệ: {args[i]}");
            }
        }

        var profile = target.LoadProfileIfSpecified();
        var locator = new WindowLocator(logger);
        var window = target.ResolveGameWindow(locator, profile);
        var service = new CursorCaptureService(logger);

        Console.Error.WriteLine(
            $"Target: {window.Info.HandleHex} pid {window.Info.ProcessId} {window.Info.ProcessName} " +
            $"({window.Info.ClientSizeText}).");
        Console.Error.WriteLine(
            $"Đưa con trỏ tới điểm cần capture trên cửa sổ game rồi nhấn F8 " +
            $"(timeout {timeoutSeconds} giây, Ctrl+C để huỷ).");

        using var cancellation = new ConsoleCancellation();
        CursorCaptureResult capture;

        try
        {
            capture = service.WaitForHotkey(
                window,
                TimeSpan.FromSeconds(timeoutSeconds),
                cancellation.Token);
        }
        catch (OperationCanceledException)
        {
            logger.Warn("capture.canceled", "Cursor capture canceled by user");
            Console.Error.WriteLine("Đã huỷ capture.");
            return ExitCodes.Canceled;
        }

        WriteOutput(capture, json);

        if (!capture.Point.IsInsideClientArea)
        {
            Console.Error.WriteLine(
                $"Con trỏ nằm ngoài client area: client ({capture.Point.RawX:0.##},{capture.Point.RawY:0.##}), " +
                $"client size {capture.Window.ClientSizeText}.");
            return ExitCodes.OutOfBounds;
        }

        return ExitCodes.Success;
    }

    private static void WriteOutput(CursorCaptureResult capture, bool json)
    {
        var point = capture.Point;

        if (json)
        {
            var output = new CaptureOutput(
                capture.Window.HandleHex,
                capture.Window.ProcessId,
                capture.Window.ProcessName,
                capture.Window.Title,
                capture.Window.ClientWidth,
                capture.Window.ClientHeight,
                capture.ScreenX,
                capture.ScreenY,
                (int)point.RawX,
                (int)point.RawY,
                point.IsInsideClientArea,
                new CoordinateOutput("client", (int)point.RawX),
                new CoordinateOutput("client", (int)point.RawY));

            Console.WriteLine(JsonSerializer.Serialize(output, CommandJson.Pretty));
            return;
        }

        Console.WriteLine(
            $"Captured: client ({point.RawX:0.##}, {point.RawY:0.##}), " +
            $"screen ({capture.ScreenX}, {capture.ScreenY}), inside={YesNo(point.IsInsideClientArea)}");
        Console.WriteLine($"Window:   {capture.Window.HandleHex} pid {capture.Window.ProcessId} " +
                          $"{capture.Window.ProcessName} ({capture.Window.ClientSizeText})");

        if (point.IsInsideClientArea)
        {
            Console.WriteLine();
            Console.WriteLine("Dán vào profile:");
            Console.WriteLine($"  \"x\": {{ \"mode\": \"client\", \"value\": {(int)point.RawX} }},");
            Console.WriteLine($"  \"y\": {{ \"mode\": \"client\", \"value\": {(int)point.RawY} }}");
        }
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

internal sealed record CoordinateOutput(string Mode, int Value);

internal sealed record CaptureOutput(
    string Hwnd,
    uint Pid,
    string Process,
    string Title,
    int ClientWidth,
    int ClientHeight,
    int ScreenX,
    int ScreenY,
    int ClientX,
    int ClientY,
    bool Inside,
    CoordinateOutput X,
    CoordinateOutput Y);
