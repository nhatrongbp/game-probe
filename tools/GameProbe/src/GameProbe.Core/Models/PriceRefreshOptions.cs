namespace GameProbe.Core.Models;

/// <summary>
/// Hardcoded-coordinate scenario that re-opens a buy dialog until the displayed
/// maximum price changes, then clicks the price and the buy button.
/// </summary>
public sealed class PriceRefreshOptions
{
    public string RelistPointId { get; set; } = "";

    /// <summary>Optional green-text pixel check that the relist button is present.</summary>
    public string? RelistGuardProbeId { get; set; }

    /// <summary>Pixel probe that matches the red buy button while the dialog is open.</summary>
    public string PopupProbeId { get; set; } = "";

    public string PriceRegionId { get; set; } = "";

    /// <summary>Click target on the displayed maximum-price value.</summary>
    public string PricePointId { get; set; } = "";

    public string CancelPointId { get; set; } = "";

    public string BuyPointId { get; set; } = "";

    /// <summary>Delay after launch so the user can focus the game window.</summary>
    public int StartDelayMs { get; set; } = 3000;

    /// <summary>Wait after the red button appears, before capturing a stable frame.</summary>
    public int SettleMs { get; set; } = 350;

    /// <summary>Gap between the two frames used to reject animation frames.</summary>
    public int StableFrameGapMs { get; set; } = 120;

    public int StableTimeoutMs { get; set; } = 1500;

    public int PollIntervalMs { get; set; } = 100;

    public int PopupTimeoutMs { get; set; } = 3000;

    public int CloseTimeoutMs { get; set; } = 3000;

    /// <summary>Delay between a cancelled dialog and the next relist click.</summary>
    public int AttemptDelayMs { get; set; } = 800;

    public int MaxAttempts { get; set; } = 40;

    /// <summary>Delay between clicking the changed price and clicking buy.</summary>
    public int PriceClickDelayMs { get; set; } = 200;

    /// <summary>Per-channel tolerance; 0 means exact RGB comparison.</summary>
    public int ChannelTolerance { get; set; }

    /// <summary>Number of changed pixels required to treat the price as changed.</summary>
    public int MinChangedPixels { get; set; } = 1;

    /// <summary>Expected client width; 0 disables the guard.</summary>
    public int ExpectedClientWidth { get; set; }

    /// <summary>Expected client height; 0 disables the guard.</summary>
    public int ExpectedClientHeight { get; set; }

    /// <summary>Expected DPI; 0 disables the guard.</summary>
    public uint ExpectedDpi { get; set; }
}
