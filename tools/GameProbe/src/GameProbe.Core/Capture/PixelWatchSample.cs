namespace GameProbe.Core.Capture;

public sealed record PixelWatchSample(
    DateTimeOffset Timestamp,
    PixelReadResult Result,
    PixelReadResult? Previous,
    bool Changed);

public sealed record PixelWatchSummary(
    int SampleCount,
    int ChangeCount,
    TimeSpan Duration,
    bool Canceled);
