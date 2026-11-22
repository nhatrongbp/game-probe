using System.Globalization;
using GameProbe.Core.Diagnostics;
using GameProbe.Core.Models;
using GameProbe.Core.Profiles;
using GameProbe.Core.Windows;

namespace GameProbe.Cli.Commands;

/// <summary>Consumes the target-selection options shared by click/pixel/capture commands.</summary>
internal sealed class TargetSelection
{
    public string? ProfilePath { get; private set; }

    public IntPtr? Hwnd { get; private set; }

    public uint? Pid { get; private set; }

    public string? ClassName { get; private set; }

    public string? ProcessName { get; private set; }

    public string? TitleContains { get; private set; }

    public bool IncludeHidden { get; private set; }

    public bool HasExplicitCriteria =>
        Pid is not null
        || !string.IsNullOrWhiteSpace(ClassName)
        || !string.IsNullOrWhiteSpace(ProcessName)
        || !string.IsNullOrWhiteSpace(TitleContains);

    public bool TryConsume(string[] args, ref int index)
    {
        switch (args[index])
        {
            case "--profile":
                ProfilePath = RequireValue(args, ref index, "--profile");
                return true;

            case "--hwnd":
                var hwndText = RequireValue(args, ref index, "--hwnd");
                if (!HwndParser.TryParse(hwndText, out var handle))
                {
                    throw new CliUsageException($"HWND không hợp lệ: {hwndText}");
                }

                Hwnd = handle;
                return true;

            case "--pid":
                var pidText = RequireValue(args, ref index, "--pid");
                if (!uint.TryParse(pidText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var pid)
                    || pid == 0)
                {
                    throw new CliUsageException($"PID không hợp lệ: {pidText}");
                }

                Pid = pid;
                return true;

            case "--class":
                ClassName = RequireValue(args, ref index, "--class");
                return true;

            case "--process":
                ProcessName = RequireValue(args, ref index, "--process");
                return true;

            case "--title":
                TitleContains = RequireValue(args, ref index, "--title");
                return true;

            case "--include-hidden":
                IncludeHidden = true;
                return true;

            default:
                return false;
        }
    }

    public void Validate()
    {
        if (Hwnd is not null && HasExplicitCriteria)
        {
            throw new CliUsageException(
                "Không dùng --hwnd cùng lúc với --pid/--class/--process/--title.");
        }
    }

    public Profile? LoadProfileIfSpecified()
    {
        return ProfilePath is null ? null : new ProfileStore().Load(ProfilePath);
    }

    public WindowInfo Resolve(WindowLocator locator, Profile? profile)
    {
        ArgumentNullException.ThrowIfNull(locator);
        Validate();

        if (Hwnd is { } explicitHandle)
        {
            return locator.FindByHandle(explicitHandle)
                ?? throw new GameProbeException(
                    GameProbeErrorKind.WindowNotFound,
                    $"Không tìm thấy cửa sổ HWND 0x{explicitHandle.ToInt64():X}.");
        }

        var className = ClassName;
        var processName = ProcessName;
        var titleContains = TitleContains;

        if (!HasExplicitCriteria && profile is not null)
        {
            className = profile.Target.ClassName;
            processName = profile.Target.ProcessName;
            titleContains = profile.Target.TitleContains;
        }

        if (string.IsNullOrWhiteSpace(className)
            && string.IsNullOrWhiteSpace(processName)
            && string.IsNullOrWhiteSpace(titleContains)
            && Pid is null)
        {
            throw new CliUsageException(
                "Cần --profile hoặc ít nhất một tiêu chí --hwnd/--pid/--class/--process/--title.");
        }

        var criteria = new WindowSearchCriteria
        {
            ProcessId = Pid,
            ClassName = className,
            ProcessName = processName,
            TitleContains = titleContains,
            RequireVisible = !IncludeHidden
        };

        return locator.Find(criteria)
            ?? throw new GameProbeException(
                GameProbeErrorKind.WindowNotFound,
                "Không tìm thấy cửa sổ phù hợp với tiêu chí target.");
    }

    public GameWindow ResolveGameWindow(WindowLocator locator, Profile? profile)
    {
        var info = Resolve(locator, profile);
        return new GameWindow(locator, info);
    }

    private static string RequireValue(string[] args, ref int index, string option)
    {
        if (index + 1 >= args.Length)
        {
            throw new CliUsageException($"Thiếu giá trị cho {option}.");
        }

        var value = args[++index];
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new CliUsageException($"Giá trị cho {option} không được để trống.");
        }

        return value;
    }
}
