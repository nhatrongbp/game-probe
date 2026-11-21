using System.Text;
using GameProbe.Core.Diagnostics;

namespace GameProbe.Cli;

internal static class Program
{
    private static int Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;

        var logger = new ConsoleActionLogger(Console.Error);

        try
        {
            return Run(args, logger);
        }
        catch (Exception ex)
        {
            logger.Error("cli.unhandled", ex.Message);
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
}
