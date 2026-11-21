namespace GameProbe.Core.Models;

public enum CoordinateMode
{
    Client,
    Percent,
    Anchor
}

public enum Anchor
{
    Left,
    Top,
    Center,
    Right,
    Bottom
}

public enum MouseButton
{
    Left,
    Right,
    Middle
}

public enum ClickSendMode
{
    Post,
    Send,
    SendInput
}

public enum WParamMode
{
    Button,
    None
}
