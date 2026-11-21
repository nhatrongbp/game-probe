using System.Text.Json;
using GameProbe.Core.Diagnostics;
using GameProbe.Core.Windows;

namespace GameProbe.Cli.Commands;

internal static class WindowListCommand
{
    public static int Execute(string[] args, IActionLogger logger)
    {
        var includeAll = false;
        var json = false;

        foreach (var arg in args)
        {
            switch (arg)
            {
                case "-h":
                case "--help":
                    HelpText.PrintWindowList();
                    return ExitCodes.Success;
                case "-a":
                case "--all":
                    includeAll = true;
                    break;
                case "--json":
                    json = true;
                    break;
                default:
                    Console.Error.WriteLine($"Option không hợp lệ: {arg}");
                    HelpText.PrintWindowList();
                    return ExitCodes.Usage;
            }
        }

        var locator = new WindowLocator(logger);
        var windows = locator.EnumerateTopLevelWindows()
            .Where(window => includeAll || IsMeaningful(window))
            .OrderBy(window => window.ProcessName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(window => window.ClassName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(window => window.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (json)
        {
            Console.WriteLine(JsonSerializer.Serialize(
                windows.Select(WindowJson.ToListItem),
                WindowJson.Options));
        }
        else
        {
            PrintTable(windows, includeAll);
        }

        logger.Info(
            "window.list",
            "Window list completed",
            new Dictionary<string, object?>
            {
                ["count"] = windows.Count,
                ["includeAll"] = includeAll
            });

        return ExitCodes.Success;
    }

    private static void PrintTable(IReadOnlyList<WindowInfo> windows, bool includeAll)
    {
        if (windows.Count == 0)
        {
            Console.WriteLine("Không tìm thấy cửa sổ nào.");
            return;
        }

        Console.WriteLine(
            $"{"HWND",-14} {"PID",-7} {"PROCESS",-20} {"CLASS",-28} {"CLIENT",-11} {"DPI",-4} {"FLAGS",-10} TITLE");

        foreach (var window in windows)
        {
            var flags = BuildFlags(window);
            Console.WriteLine(
                $"{window.HandleHex,-14} " +
                $"{window.ProcessId,-7} " +
                $"{Fit(window.ProcessName, 20),-20} " +
                $"{Fit(window.ClassName, 28),-28} " +
                $"{window.ClientSizeText,-11} " +
                $"{window.Dpi,-4} " +
                $"{flags,-10} " +
                $"{Fit(window.Title, 60)}");
        }

        Console.WriteLine();
        Console.WriteLine(
            includeAll
                ? $"Tổng: {windows.Count} cửa sổ (gồm cả hidden/kích thước nhỏ)."
                : $"Tổng: {windows.Count} cửa sổ visible có kích thước >= 16x16. Dùng --all để xem tất cả.");
    }

    private static bool IsMeaningful(WindowInfo window) =>
        window.IsVisible && window.ClientWidth >= 16 && window.ClientHeight >= 16;

    private static string BuildFlags(WindowInfo window)
    {
        var flags = new List<string>();
        if (!window.IsVisible)
        {
            flags.Add("hidden");
        }

        if (window.IsMinimized)
        {
            flags.Add("min");
        }

        if (window.IsHung)
        {
            flags.Add("hung");
        }

        return flags.Count == 0 ? "-" : string.Join(",", flags);
    }

    private static string Fit(string value, int width)
    {
        if (value.Length <= width)
        {
            return value;
        }

        return width <= 3 ? value[..width] : value[..(width - 3)] + "...";
    }
}
