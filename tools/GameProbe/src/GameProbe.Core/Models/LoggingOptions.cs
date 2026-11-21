namespace GameProbe.Core.Models;

public sealed class LoggingOptions
{
    public string Level { get; set; } = "info";

    public bool ScreenshotOnError { get; set; }
}
