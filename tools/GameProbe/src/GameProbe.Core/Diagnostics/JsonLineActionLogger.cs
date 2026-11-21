using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GameProbe.Core.Diagnostics;

public sealed class JsonLineActionLogger : IActionLogger
{
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly object _gate = new();
    private readonly string _filePath;

    public JsonLineActionLogger(string? filePath = null)
    {
        _filePath = Path.GetFullPath(filePath ?? GameProbePaths.GetDefaultLogFilePath());
    }

    public string FilePath => _filePath;

    public void Log(
        GameProbeLogLevel level,
        string action,
        string message,
        IReadOnlyDictionary<string, object?>? data = null)
    {
        var entry = new ActionLogEntry(
            DateTimeOffset.Now,
            level.ToString().ToLowerInvariant(),
            action,
            message,
            data);

        try
        {
            var line = JsonSerializer.Serialize(entry, JsonOptions);

            lock (_gate)
            {
                var directory = Path.GetDirectoryName(_filePath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.AppendAllText(_filePath, line + Environment.NewLine, Utf8NoBom);
            }
        }
        catch (Exception ex)
        {
            // Logging must never take the application down.
            Debug.WriteLine($"GameProbe file logging failed: {ex}");
        }
    }
}
