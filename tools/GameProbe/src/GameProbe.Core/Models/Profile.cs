namespace GameProbe.Core.Models;

public sealed class Profile
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    public string Name { get; set; } = "";

    public TargetSpec Target { get; set; } = new();

    public List<ClickPoint> ClickPoints { get; set; } = new();

    public List<PixelProbeSpec> PixelProbes { get; set; } = new();

    public MemoryOptions Memory { get; set; } = new();

    public HotkeyOptions Hotkeys { get; set; } = new();

    public LoggingOptions Logging { get; set; } = new();
}
