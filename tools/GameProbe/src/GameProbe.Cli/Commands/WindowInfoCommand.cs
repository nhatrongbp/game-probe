using System.Globalization;
using System.Text.Json;
using GameProbe.Core.Diagnostics;
using GameProbe.Core.Windows;

namespace GameProbe.Cli.Commands;

internal static class WindowInfoCommand
{
    public static int Execute(string[] args, IActionLogger logger)
    {
        IntPtr? handle = null;
        uint? processId = null;
        string? className = null;
        string? processName = null;
        string? titleContains = null;
        var includeHidden = false;
        var json = false;
        var hasCriteria = false;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "-h":
                case "--help":
                    HelpText.PrintWindowInfo();
                    return ExitCodes.Success;

                case "--hwnd":
                    if (i + 1 >= args.Length)
                    {
                        return MissingValue("--hwnd");
                    }

                    if (!HwndParser.TryParse(args[++i], out var parsedHandle))
                    {
                        Console.Error.WriteLine($"HWND không hợp lệ: {args[i]}");
                        return ExitCodes.Usage;
                    }

                    handle = parsedHandle;
                    break;

                case "--pid":
                    if (i + 1 >= args.Length)
                    {
                        return MissingValue("--pid");
                    }

                    if (!uint.TryParse(args[++i], NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedPid)
                        || parsedPid == 0)
                    {
                        Console.Error.WriteLine($"PID không hợp lệ: {args[i]}");
                        return ExitCodes.Usage;
                    }

                    processId = parsedPid;
                    hasCriteria = true;
                    break;

                case "--class":
                    if (i + 1 >= args.Length)
                    {
                        return MissingValue("--class");
                    }

                    className = args[++i];
                    hasCriteria = true;
                    break;

                case "--process":
                    if (i + 1 >= args.Length)
                    {
                        return MissingValue("--process");
                    }

                    processName = args[++i];
                    hasCriteria = true;
                    break;

                case "--title":
                    if (i + 1 >= args.Length)
                    {
                        return MissingValue("--title");
                    }

                    titleContains = args[++i];
                    hasCriteria = true;
                    break;

                case "--include-hidden":
                    includeHidden = true;
                    break;

                case "--json":
                    json = true;
                    break;

                default:
                    Console.Error.WriteLine($"Option không hợp lệ: {args[i]}");
                    HelpText.PrintWindowInfo();
                    return ExitCodes.Usage;
            }
        }

        if (handle is not null && hasCriteria)
        {
            Console.Error.WriteLine("Không dùng --hwnd cùng lúc với --pid/--class/--process/--title.");
            return ExitCodes.Usage;
        }

        if (handle is null && !hasCriteria)
        {
            Console.Error.WriteLine("Cần --hwnd hoặc ít nhất một tiêu chí tìm kiếm.");
            HelpText.PrintWindowInfo();
            return ExitCodes.Usage;
        }

        var locator = new WindowLocator(logger);
        WindowInfo? info;
        var matchCount = 1;

        if (handle is { } explicitHandle)
        {
            info = locator.FindByHandle(explicitHandle);
        }
        else
        {
            var criteria = new WindowSearchCriteria
            {
                ProcessId = processId,
                ClassName = className,
                ProcessName = processName,
                TitleContains = titleContains,
                RequireVisible = !includeHidden
            };

            var matches = locator.FindAll(criteria);
            info = matches.FirstOrDefault();
            matchCount = matches.Count;
        }

        if (info is null)
        {
            logger.Warn("window.info", "No matching window found");
            Console.Error.WriteLine("Không tìm thấy cửa sổ phù hợp.");
            return ExitCodes.NotFound;
        }

        if (json)
        {
            Console.WriteLine(JsonSerializer.Serialize(WindowJson.ToDetail(info), WindowJson.Options));
        }
        else
        {
            PrintDetail(info, matchCount);
        }

        logger.Info(
            "window.info",
            "Window info completed",
            new Dictionary<string, object?>
            {
                ["hwnd"] = info.HandleHex,
                ["pid"] = info.ProcessId,
                ["title"] = info.Title,
                ["matches"] = matchCount
            });

        return ExitCodes.Success;
    }

    private static void PrintDetail(WindowInfo info, int matchCount)
    {
        Console.WriteLine($"HWND:          {info.HandleHex}");
        Console.WriteLine($"PID:           {info.ProcessId}");
        Console.WriteLine($"Process:       {ValueOr(info.ProcessName, "(unavailable)")}");
        Console.WriteLine($"Process path:  {ValueOr(info.ProcessPath, "(unavailable)")}");
        Console.WriteLine($"Window class:  {ValueOr(info.ClassName, "(empty)")}");
        Console.WriteLine($"Title:         {ValueOr(info.Title, "(empty)")}");
        Console.WriteLine($"Visible:       {YesNo(info.IsVisible)}");
        Console.WriteLine($"Minimized:     {YesNo(info.IsMinimized)}");
        Console.WriteLine($"Hung:          {YesNo(info.IsHung)}");
        Console.WriteLine($"Window rect:   {info.WindowRectText}");
        Console.WriteLine($"Client size:   {info.ClientSizeText}");
        Console.WriteLine($"DPI:           {info.Dpi} ({Math.Round(info.DpiScale * 100)}%)");

        if (matchCount > 1)
        {
            Console.WriteLine();
            Console.WriteLine(
                $"Có {matchCount} cửa sổ khớp; đang hiển thị cửa sổ ưu tiên nhất (visible/lớn nhất).");
        }
    }

    private static string YesNo(bool value) => value ? "yes" : "no";

    private static string ValueOr(string value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value;

    private static int MissingValue(string option)
    {
        Console.Error.WriteLine($"Thiếu giá trị cho {option}.");
        return ExitCodes.Usage;
    }
}

internal static class HwndParser
{
    public static bool TryParse(string? text, out IntPtr handle)
    {
        handle = IntPtr.Zero;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var value = text.Trim();
        long parsed;

        if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            if (!long.TryParse(
                    value[2..],
                    NumberStyles.HexNumber,
                    CultureInfo.InvariantCulture,
                    out parsed))
            {
                return false;
            }
        }
        else if (!long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed))
        {
            return false;
        }

        if (parsed <= 0)
        {
            return false;
        }

        handle = new IntPtr(parsed);
        return true;
    }
}
