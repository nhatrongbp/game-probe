using System.Text;
using System.Text.Json;
using GameProbe.Core.Diagnostics;
using GameProbe.Core.Profiles;

namespace GameProbe.Cli;

internal static class Program
{
    private static int Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;

        var logger = new CompositeActionLogger(
            new ConsoleActionLogger(Console.Error),
            new JsonLineActionLogger());

        try
        {
            return Run(args, logger);
        }
        catch (CliUsageException ex)
        {
            Console.Error.WriteLine(ex.Message);
            logger.Warn("cli.usage", ex.Message);
            return ExitCodes.Usage;
        }
        catch (ProfileValidationException ex)
        {
            logger.Error("cli.profile_invalid", "Profile validation failed");
            Console.Error.WriteLine("Profile không hợp lệ:");
            foreach (var error in ex.Errors)
            {
                Console.Error.WriteLine($"  - {error}");
            }

            return ExitCodes.Usage;
        }
        catch (FileNotFoundException ex)
        {
            logger.Error("cli.file_not_found", ex.Message);
            Console.Error.WriteLine($"Không tìm thấy file: {ex.FileName ?? ex.Message}");
            return ExitCodes.NotFound;
        }
        catch (UnauthorizedAccessException ex)
        {
            logger.Error("cli.access_denied", ex.Message);
            Console.Error.WriteLine($"Không có quyền truy cập: {ex.Message}");
            return ExitCodes.AccessDenied;
        }
        catch (JsonException ex)
        {
            logger.Error("cli.json_invalid", ex.Message);
            Console.Error.WriteLine($"JSON không hợp lệ: {ex.Message}");
            return ExitCodes.Usage;
        }
        catch (GameProbeException ex)
        {
            logger.Error(
                "cli.error",
                ex.Message,
                new Dictionary<string, object?>
                {
                    ["kind"] = ex.Kind.ToString(),
                    ["win32"] = ex.Win32Error
                });
            Console.Error.WriteLine($"Lỗi: {ex.Message}");
            return MapGameProbeError(ex.Kind);
        }
        catch (OperationCanceledException)
        {
            logger.Warn("cli.canceled", "Operation canceled");
            Console.Error.WriteLine("Đã huỷ.");
            return ExitCodes.Canceled;
        }
        catch (ArgumentException ex)
        {
            logger.Error("cli.argument", ex.Message);
            Console.Error.WriteLine($"Tham số không hợp lệ: {ex.Message}");
            return ExitCodes.Usage;
        }
        catch (Exception ex)
        {
            logger.Error("cli.unhandled", ex.Message);
            Console.Error.WriteLine($"Lỗi không mong muốn: {ex.Message}");
            return ExitCodes.Failure;
        }
    }

    private static int Run(string[] args, IActionLogger logger)
    {
        if (args.Length == 0)
        {
            HelpText.PrintGeneral();
            return ExitCodes.Success;
        }

        switch (args[0].ToLowerInvariant())
        {
            case "help":
            case "--help":
            case "-h":
                HelpText.PrintGeneral();
                return ExitCodes.Success;

            case "--version":
            case "-v":
                var version = typeof(Program).Assembly.GetName().Version?.ToString(3) ?? "0.1.0";
                Console.WriteLine($"GameProbe {version}");
                return ExitCodes.Success;

            case "profile":
                if (args.Length >= 2 && args[1].Equals("validate", StringComparison.OrdinalIgnoreCase))
                {
                    return Commands.ProfileValidateCommand.Execute(args.Skip(2).ToArray(), logger);
                }

                Console.Error.WriteLine("Thiếu lệnh con. Dùng: gameprobe profile validate --file <path>");
                HelpText.PrintProfileValidate();
                return ExitCodes.Usage;

            case "window":
                if (args.Length < 2)
                {
                    Console.Error.WriteLine("Thiếu lệnh con. Dùng: gameprobe window list|info");
                    HelpText.PrintWindowCommands();
                    return ExitCodes.Usage;
                }

                return args[1].ToLowerInvariant() switch
                {
                    "list" => Commands.WindowListCommand.Execute(args.Skip(2).ToArray(), logger),
                    "info" => Commands.WindowInfoCommand.Execute(args.Skip(2).ToArray(), logger),
                    _ => UnknownWindowCommand(args[1])
                };

            case "click":
                return Commands.ClickCommand.Execute(args.Skip(1).ToArray(), logger);

            case "capture":
                return Commands.CaptureCommand.Execute(args.Skip(1).ToArray(), logger);

            case "region":
                if (args.Length < 2
                    || args[1].Equals("--help", StringComparison.OrdinalIgnoreCase)
                    || args[1].Equals("-h", StringComparison.OrdinalIgnoreCase))
                {
                    HelpText.PrintRegionCommands();
                    return args.Length < 2 ? ExitCodes.Usage : ExitCodes.Success;
                }

                return args[1].ToLowerInvariant() switch
                {
                    "capture" => Commands.RegionCaptureCommand.Execute(
                        args.Skip(2).ToArray(),
                        logger),
                    "stability" => Commands.RegionStabilityCommand.Execute(
                        args.Skip(2).ToArray(),
                        logger),
                    _ => UnknownRegionCommand(args[1])
                };

            case "scenario":
                if (args.Length >= 2
                    && args[1].Equals("price-refresh", StringComparison.OrdinalIgnoreCase))
                {
                    return Commands.PriceRefreshCommand.Execute(
                        args.Skip(2).ToArray(),
                        logger);
                }

                Console.Error.WriteLine("Thiếu lệnh con. Dùng: gameprobe scenario price-refresh --profile <path>");
                HelpText.PrintPriceRefresh();
                return ExitCodes.Usage;

            case "pixel":
                if (args.Length >= 2 && args[1].Equals("watch", StringComparison.OrdinalIgnoreCase))
                {
                    return Commands.PixelWatchCommand.Execute(args.Skip(2).ToArray(), logger);
                }

                return Commands.PixelReadCommand.Execute(args.Skip(1).ToArray(), logger);

            default:
                Console.Error.WriteLine($"Lệnh không hợp lệ: {args[0]}");
                HelpText.PrintGeneral();
                return ExitCodes.Usage;
        }
    }

    private static int UnknownWindowCommand(string command)
    {
        Console.Error.WriteLine($"Lệnh window không hợp lệ: {command}");
        HelpText.PrintWindowCommands();
        return ExitCodes.Usage;
    }

    private static int UnknownRegionCommand(string command)
    {
        Console.Error.WriteLine($"Lệnh region không hợp lệ: {command}");
        HelpText.PrintRegionCommands();
        return ExitCodes.Usage;
    }

    private static int MapGameProbeError(GameProbeErrorKind kind)
    {
        return kind switch
        {
            GameProbeErrorKind.InvalidConfiguration => ExitCodes.Usage,
            GameProbeErrorKind.Unsupported => ExitCodes.Usage,
            GameProbeErrorKind.WindowNotFound => ExitCodes.NotFound,
            GameProbeErrorKind.WindowUnavailable => ExitCodes.NotFound,
            GameProbeErrorKind.AccessDenied => ExitCodes.AccessDenied,
            GameProbeErrorKind.OutOfBounds => ExitCodes.OutOfBounds,
            GameProbeErrorKind.SendFailed => ExitCodes.SendFailed,
            GameProbeErrorKind.Timeout => ExitCodes.Timeout,
            GameProbeErrorKind.Canceled => ExitCodes.Canceled,
            _ => ExitCodes.Failure
        };
    }
}
