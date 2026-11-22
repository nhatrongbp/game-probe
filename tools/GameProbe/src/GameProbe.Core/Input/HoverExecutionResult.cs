namespace GameProbe.Core.Input;

public sealed record HoverExecutionResult(
    int X,
    int Y,
    TimeSpan Duration);
