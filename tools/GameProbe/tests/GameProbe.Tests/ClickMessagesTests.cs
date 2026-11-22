using GameProbe.Core.Input;
using GameProbe.Core.Models;
using Xunit;

namespace GameProbe.Tests;

public sealed class ClickMessagesTests
{
    [Theory]
    [InlineData(MouseButton.Left, 0x0201u, 0x0202u, 0x0001u)]
    [InlineData(MouseButton.Right, 0x0204u, 0x0205u, 0x0002u)]
    [InlineData(MouseButton.Middle, 0x0207u, 0x0208u, 0x0010u)]
    public void For_MapsButtonMessages(
        MouseButton button,
        uint expectedDown,
        uint expectedUp,
        uint expectedWParam)
    {
        var messages = ClickMessages.For(button);

        Assert.Equal(expectedDown, messages.Down);
        Assert.Equal(expectedUp, messages.Up);
        Assert.Equal(expectedWParam, messages.ButtonWParam);
    }

    [Fact]
    public void GetDownWParam_ButtonMode_ReturnsMarker()
    {
        Assert.Equal(ClickMessages.MkLeftButton,
            ClickMessages.GetDownWParam(MouseButton.Left, WParamMode.Button));
        Assert.Equal(ClickMessages.MkRightButton,
            ClickMessages.GetDownWParam(MouseButton.Right, WParamMode.Button));
        Assert.Equal(ClickMessages.MkMiddleButton,
            ClickMessages.GetDownWParam(MouseButton.Middle, WParamMode.Button));
    }

    [Fact]
    public void GetDownWParam_NoneMode_ReturnsZero()
    {
        Assert.Equal(0u, ClickMessages.GetDownWParam(MouseButton.Left, WParamMode.None));
    }

    [Fact]
    public void MakeLParam_PacksXAndY()
    {
        var lParam = ClickMessages.MakeLParam(568, 124);

        Assert.Equal(8_127_032, lParam.ToInt64());
    }

    [Fact]
    public void GetMessageName_ReturnsKnownNames()
    {
        Assert.Equal("WM_MOUSEMOVE", ClickMessages.GetMessageName(ClickMessages.WmMouseMove));
        Assert.Equal("WM_LBUTTONDOWN", ClickMessages.GetMessageName(ClickMessages.WmLeftButtonDown));
        Assert.Equal("0x1234", ClickMessages.GetMessageName(0x1234));
    }

    [Fact]
    public void InvalidButton_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ClickMessages.For((MouseButton)99));
    }
}
