namespace GameProbe.Core.Models;

public sealed class ClickPoint
{
    public string Id { get; set; } = "";

    public string Name { get; set; } = "";

    public CoordinateSpec X { get; set; } = new();

    public CoordinateSpec Y { get; set; } = new();

    public MouseButton Button { get; set; } = MouseButton.Left;

    public int Count { get; set; } = 1;

    public int HoldMs { get; set; } = 50;

    public int IntervalMs { get; set; } = 200;

    public ClickSendMode SyncMode { get; set; } = ClickSendMode.Post;

    public bool Hover { get; set; } = true;

    /// <summary>Delay after WM_MOUSEMOVE before the button-down message.</summary>
    public int HoverSettleMs { get; set; }

    public WParamMode WParamMode { get; set; } = WParamMode.Button;
}
