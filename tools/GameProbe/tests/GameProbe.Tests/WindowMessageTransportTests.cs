using GameProbe.Core.Input;
using Xunit;

namespace GameProbe.Tests;

public sealed class WindowMessageTransportTests
{
    private const uint WmNull = 0x0000;

    [Fact]
    public void PostMessage_ToLiveWindow_Succeeds()
    {
        using var hidden = new TestWindow("GameProbe Transport Post", 100, 100);
        var transport = new NativeWindowMessageTransport();

        var result = transport.PostMessage(
            hidden.Handle,
            WmNull,
            IntPtr.Zero,
            IntPtr.Zero);

        Assert.True(result.Success, $"PostMessage failed (Win32 {result.ErrorCode}).");
    }

    [Fact]
    public void SendMessage_ToLiveWindow_Succeeds()
    {
        using var hidden = new TestWindow("GameProbe Transport Send", 100, 100);
        var transport = new NativeWindowMessageTransport();

        var result = transport.SendMessage(
            hidden.Handle,
            WmNull,
            IntPtr.Zero,
            IntPtr.Zero,
            TimeSpan.FromMilliseconds(100));

        Assert.True(result.Success, $"SendMessage failed (Win32 {result.ErrorCode}).");
    }
}
