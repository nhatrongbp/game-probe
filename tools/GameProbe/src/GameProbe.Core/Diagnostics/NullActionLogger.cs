namespace GameProbe.Core.Diagnostics;

public sealed class NullActionLogger : IActionLogger
{
    public static NullActionLogger Instance { get; } = new();

    private NullActionLogger()
    {
    }

    public void Log(
        GameProbeLogLevel level,
        string action,
        string message,
        IReadOnlyDictionary<string, object?>? data = null)
    {
    }
}
