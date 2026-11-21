namespace GameProbe.Core.Windows;

public sealed class WindowSearchCriteria
{
    public IntPtr? Handle { get; init; }

    public uint? ProcessId { get; init; }

    public string? ClassName { get; init; }

    public string? ProcessName { get; init; }

    public string? TitleContains { get; init; }

    public bool RequireVisible { get; init; } = true;

    public bool IsEmpty =>
        Handle is null
        && ProcessId is null
        && string.IsNullOrWhiteSpace(ClassName)
        && string.IsNullOrWhiteSpace(ProcessName)
        && string.IsNullOrWhiteSpace(TitleContains);

    public bool Matches(WindowInfo window)
    {
        ArgumentNullException.ThrowIfNull(window);

        if (Handle is { } requiredHandle && window.Handle != requiredHandle)
        {
            return false;
        }

        if (ProcessId is { } requiredPid && window.ProcessId != requiredPid)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(ClassName)
            && !window.ClassName.Equals(ClassName.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(ProcessName) && !MatchesProcessName(window, ProcessName.Trim()))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(TitleContains)
            && !window.Title.Contains(TitleContains.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return !RequireVisible || window.IsVisible;
    }

    private static bool MatchesProcessName(WindowInfo window, string expected)
    {
        if (string.IsNullOrEmpty(window.ProcessName))
        {
            return false;
        }

        if (window.ProcessName.Equals(expected, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var withoutExtension = Path.GetFileNameWithoutExtension(window.ProcessName);
        return withoutExtension.Equals(expected, StringComparison.OrdinalIgnoreCase);
    }
}
