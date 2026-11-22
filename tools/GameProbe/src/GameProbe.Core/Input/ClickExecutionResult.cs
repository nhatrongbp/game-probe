namespace GameProbe.Core.Input;

public sealed record ClickExecutionResult(
    int X,
    int Y,
    int SentClicks,
    TimeSpan Duration);
