namespace GameProbe.Core.Diagnostics;

public sealed class ConsoleActionLogger : IActionLogger
{
    private readonly TextWriter _writer;

    public ConsoleActionLogger(TextWriter? writer = null)
    {
        _writer = writer ?? Console.Out;
    }

    public void Log(
        GameProbeLogLevel level,
        string action,
        string message,
        IReadOnlyDictionary<string, object?>? data = null)
    {
        var timestamp = DateTimeOffset.Now.ToString("HH:mm:ss");
        _writer.WriteLine($"[{timestamp}] {level.ToString().ToUpperInvariant(),-5} {action}: {message}");

        if (data is not null && data.Count > 0)
        {
            foreach (var (key, value) in data)
            {
                _writer.WriteLine($"         {key} = {value}");
            }
        }
    }
}
