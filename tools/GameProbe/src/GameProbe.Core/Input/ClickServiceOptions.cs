namespace GameProbe.Core.Input;

public sealed class ClickServiceOptions
{
    /// <summary>Minimum delay between two click sequences (the plan requires at least 20 ms).</summary>
    public int MinimumClickSpacingMs { get; init; } = 20;

    /// <summary>Timeout used by <c>SendMessageTimeoutW</c> for the <c>send</c> mode.</summary>
    public int SendTimeoutMs { get; init; } = 1000;
}
