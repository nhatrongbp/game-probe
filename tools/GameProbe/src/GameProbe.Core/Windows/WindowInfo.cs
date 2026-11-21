namespace GameProbe.Core.Windows;

public sealed class WindowInfo
{
    public IntPtr Handle { get; init; }

    public uint ProcessId { get; init; }

    public string ClassName { get; init; } = "";

    public string Title { get; init; } = "";

    public string ProcessName { get; init; } = "";

    public string ProcessPath { get; init; } = "";

    public bool IsVisible { get; init; }

    public bool IsMinimized { get; init; }

    public bool IsHung { get; init; }

    public int WindowLeft { get; init; }

    public int WindowTop { get; init; }

    public int WindowWidth { get; init; }

    public int WindowHeight { get; init; }

    public int ClientWidth { get; init; }

    public int ClientHeight { get; init; }

    public uint Dpi { get; init; } = 96;

    public string HandleHex => $"0x{Handle.ToInt64():X}";

    public string ClientSizeText => $"{ClientWidth}x{ClientHeight}";

    public string WindowRectText => $"({WindowLeft},{WindowTop}) {WindowWidth}x{WindowHeight}";

    public string DisplayName => string.IsNullOrWhiteSpace(Title)
        ? $"{ClassName} {HandleHex}"
        : Title;

    public double DpiScale => Dpi / 96.0;
}
