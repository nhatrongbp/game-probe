namespace GameProbe.Core.Diagnostics;

public static class LoggerExtensions
{
    public static void Trace(
        this IActionLogger logger,
        string action,
        string message,
        IReadOnlyDictionary<string, object?>? data = null)
        => logger.Log(GameProbeLogLevel.Trace, action, message, data);

    public static void Debug(
        this IActionLogger logger,
        string action,
        string message,
        IReadOnlyDictionary<string, object?>? data = null)
        => logger.Log(GameProbeLogLevel.Debug, action, message, data);

    public static void Info(
        this IActionLogger logger,
        string action,
        string message,
        IReadOnlyDictionary<string, object?>? data = null)
        => logger.Log(GameProbeLogLevel.Info, action, message, data);

    public static void Warn(
        this IActionLogger logger,
        string action,
        string message,
        IReadOnlyDictionary<string, object?>? data = null)
        => logger.Log(GameProbeLogLevel.Warn, action, message, data);

    public static void Error(
        this IActionLogger logger,
        string action,
        string message,
        IReadOnlyDictionary<string, object?>? data = null)
        => logger.Log(GameProbeLogLevel.Error, action, message, data);
}
