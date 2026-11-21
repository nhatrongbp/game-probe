namespace GameProbe.Core.Diagnostics;

public interface IActionLogger
{
    void Log(
        GameProbeLogLevel level,
        string action,
        string message,
        IReadOnlyDictionary<string, object?>? data = null);
}
