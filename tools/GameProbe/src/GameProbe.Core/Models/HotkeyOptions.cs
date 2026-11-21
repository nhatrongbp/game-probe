namespace GameProbe.Core.Models;

public sealed class HotkeyOptions
{
    public string CaptureCursor { get; set; } = "F8";

    public string TestClick { get; set; } = "F9";

    public string ReadPixel { get; set; } = "F10";

    public string MemorySnapshot { get; set; } = "F11";

    public string Panic { get; set; } = "F12";
}
