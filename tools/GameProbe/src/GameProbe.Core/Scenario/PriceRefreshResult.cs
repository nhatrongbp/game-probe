using GameProbe.Core.Capture;

namespace GameProbe.Core.Scenario;

public sealed record PriceRefreshResult(
    PriceRefreshOutcome Outcome,
    int Attempts,
    RegionCaptureResult? Baseline,
    RegionCaptureResult? LastCurrent,
    RegionDiff? LastDiff,
    TimeSpan Duration,
    string Message);
