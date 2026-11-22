using GameProbe.Core.Models;

namespace GameProbe.Core.Input;

public readonly record struct ClickMessageSet(uint Down, uint Up, uint ButtonWParam);

public static class ClickMessages
{
    public const uint WmMouseMove = 0x0200;
    public const uint WmLeftButtonDown = 0x0201;
    public const uint WmLeftButtonUp = 0x0202;
    public const uint WmRightButtonDown = 0x0204;
    public const uint WmRightButtonUp = 0x0205;
    public const uint WmMiddleButtonDown = 0x0207;
    public const uint WmMiddleButtonUp = 0x0208;

    public const uint MkLeftButton = 0x0001;
    public const uint MkRightButton = 0x0002;
    public const uint MkMiddleButton = 0x0010;

    public static ClickMessageSet For(MouseButton button)
    {
        return button switch
        {
            MouseButton.Left => new ClickMessageSet(WmLeftButtonDown, WmLeftButtonUp, MkLeftButton),
            MouseButton.Right => new ClickMessageSet(WmRightButtonDown, WmRightButtonUp, MkRightButton),
            MouseButton.Middle => new ClickMessageSet(WmMiddleButtonDown, WmMiddleButtonUp, MkMiddleButton),
            _ => throw new ArgumentOutOfRangeException(nameof(button), button, "Mouse button không hợp lệ.")
        };
    }

    public static uint GetDownWParam(MouseButton button, WParamMode wParamMode)
    {
        if (wParamMode == WParamMode.None)
        {
            return 0;
        }

        if (wParamMode != WParamMode.Button)
        {
            throw new ArgumentOutOfRangeException(nameof(wParamMode), wParamMode, "wParamMode không hợp lệ.");
        }

        return For(button).ButtonWParam;
    }

    public static IntPtr MakeLParam(int x, int y)
    {
        var packed = ((y & 0xFFFF) << 16) | (x & 0xFFFF);
        return new IntPtr(packed);
    }

    public static string GetMessageName(uint message)
    {
        return message switch
        {
            WmMouseMove => "WM_MOUSEMOVE",
            WmLeftButtonDown => "WM_LBUTTONDOWN",
            WmLeftButtonUp => "WM_LBUTTONUP",
            WmRightButtonDown => "WM_RBUTTONDOWN",
            WmRightButtonUp => "WM_RBUTTONUP",
            WmMiddleButtonDown => "WM_MBUTTONDOWN",
            WmMiddleButtonUp => "WM_MBUTTONUP",
            _ => $"0x{message:X4}"
        };
    }
}
