namespace GameProbe.Core.Diagnostics;

public sealed record ActionLogEntry(
    DateTimeOffset Timestamp,
    string Level,
    string Action,
    string Message,
    IReadOnlyDictionary<string, object?>? Data);
