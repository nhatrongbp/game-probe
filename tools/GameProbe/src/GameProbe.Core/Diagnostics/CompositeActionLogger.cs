namespace GameProbe.Core.Diagnostics;

public sealed class CompositeActionLogger : IActionLogger
{
    private readonly IActionLogger[] _loggers;

    public CompositeActionLogger(params IActionLogger[] loggers)
    {
        _loggers = loggers ?? throw new ArgumentNullException(nameof(loggers));
    }

    public void Log(
        GameProbeLogLevel level,
        string action,
        string message,
        IReadOnlyDictionary<string, object?>? data = null)
    {
        foreach (var logger in _loggers)
        {
            logger.Log(level, action, message, data);
        }
    }
}
