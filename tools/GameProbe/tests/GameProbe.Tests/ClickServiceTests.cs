using GameProbe.Core.Diagnostics;
using GameProbe.Core.Input;
using GameProbe.Core.Models;
using GameProbe.Core.Windows;
using Xunit;

namespace GameProbe.Tests;

public sealed class ClickServiceTests
{
    [Fact]
    public void Click_PostMode_SendsHoverDownAndUp()
    {
        using var hidden = new TestWindow("GameProbe Click Post", 200, 100);
        var window = Attach(hidden);
        var transport = new FakeTransport();
        var service = CreateService(transport);
        var point = CreatePoint();

        var result = service.Click(window, point);

        Assert.Equal(1, result.SentClicks);
        Assert.Equal(10, result.X);
        Assert.Equal(20, result.Y);
        Assert.Collection(
            transport.Messages,
            message =>
            {
                Assert.Equal(ClickMessages.WmMouseMove, message.Message);
                Assert.Equal(IntPtr.Zero, message.WParam);
                Assert.False(message.Synchronous);
            },
            message =>
            {
                Assert.Equal(ClickMessages.WmLeftButtonDown, message.Message);
                Assert.Equal(new IntPtr(ClickMessages.MkLeftButton), message.WParam);
            },
            message =>
            {
                Assert.Equal(ClickMessages.WmLeftButtonUp, message.Message);
                Assert.Equal(IntPtr.Zero, message.WParam);
            });

        Assert.All(transport.Messages, message =>
            Assert.Equal(ClickMessages.MakeLParam(10, 20), message.LParam));
    }

    [Fact]
    public void Click_CountTwoWithoutHover_SendsTwoDownUpPairs()
    {
        using var hidden = new TestWindow("GameProbe Click Repeat", 200, 100);
        var window = Attach(hidden);
        var transport = new FakeTransport();
        var service = CreateService(transport);
        var point = CreatePoint();
        point.Hover = false;
        point.Count = 2;

        var result = service.Click(window, point);

        Assert.Equal(2, result.SentClicks);
        Assert.Equal(4, transport.Messages.Count);
        Assert.Equal(
            new[]
            {
                ClickMessages.WmLeftButtonDown,
                ClickMessages.WmLeftButtonUp,
                ClickMessages.WmLeftButtonDown,
                ClickMessages.WmLeftButtonUp
            },
            transport.Messages.Select(message => message.Message));
    }

    [Fact]
    public void Click_SendMode_UsesSynchronousTransport()
    {
        using var hidden = new TestWindow("GameProbe Click Send", 200, 100);
        var window = Attach(hidden);
        var transport = new FakeTransport();
        var service = CreateService(transport);
        var point = CreatePoint();
        point.SyncMode = ClickSendMode.Send;

        service.Click(window, point);

        Assert.NotEmpty(transport.Messages);
        Assert.All(transport.Messages, message => Assert.True(message.Synchronous));
    }

    [Fact]
    public void Click_WParamNone_SendsZeroDownWParam()
    {
        using var hidden = new TestWindow("GameProbe Click WParam", 200, 100);
        var window = Attach(hidden);
        var transport = new FakeTransport();
        var service = CreateService(transport);
        var point = CreatePoint();
        point.Hover = false;
        point.WParamMode = WParamMode.None;

        service.Click(window, point);

        Assert.Equal(2, transport.Messages.Count);
        Assert.Equal(IntPtr.Zero, transport.Messages[0].WParam);
    }

    [Fact]
    public void Click_OutOfBounds_ThrowsAndSendsNothing()
    {
        using var hidden = new TestWindow("GameProbe Click Bounds", 200, 100);
        var window = Attach(hidden);
        var transport = new FakeTransport();
        var service = CreateService(transport);
        var point = CreatePoint();
        point.X = CoordinateSpec.Client(500);

        var exception = Assert.Throws<GameProbeException>(() => service.Click(window, point));

        Assert.Equal(GameProbeErrorKind.OutOfBounds, exception.Kind);
        Assert.Empty(transport.Messages);
    }

    [Fact]
    public void Click_TransportFailure_ThrowsSendFailed()
    {
        using var hidden = new TestWindow("GameProbe Click Failure", 200, 100);
        var window = Attach(hidden);
        var transport = new FakeTransport
        {
            FailMessage = ClickMessages.WmLeftButtonDown,
            FailureError = 87
        };
        var service = CreateService(transport);
        var point = CreatePoint();

        var exception = Assert.Throws<GameProbeException>(() => service.Click(window, point));

        Assert.Equal(GameProbeErrorKind.SendFailed, exception.Kind);
        Assert.Equal(87, exception.Win32Error);
    }

    [Fact]
    public void Click_AlreadyCanceled_ThrowsAndSendsNothing()
    {
        using var hidden = new TestWindow("GameProbe Click Cancel", 200, 100);
        var window = Attach(hidden);
        var transport = new FakeTransport();
        var service = CreateService(transport);
        var point = CreatePoint();

        Assert.Throws<OperationCanceledException>(() =>
            service.Click(window, point, new CancellationToken(canceled: true)));
        Assert.Empty(transport.Messages);
    }

    [Fact]
    public void Click_InvalidCount_ThrowsArgumentOutOfRange()
    {
        using var hidden = new TestWindow("GameProbe Click Invalid", 200, 100);
        var window = Attach(hidden);
        var service = CreateService(new FakeTransport());
        var point = CreatePoint();
        point.Count = 0;

        Assert.Throws<ArgumentOutOfRangeException>(() => service.Click(window, point));
    }

    private static ClickService CreateService(FakeTransport transport) =>
        new(
            NullActionLogger.Instance,
            transport,
            new ClickServiceOptions
            {
                MinimumClickSpacingMs = 0,
                SendTimeoutMs = 10
            });

    private static ClickPoint CreatePoint() => new()
    {
        Id = "test",
        Name = "Test point",
        X = CoordinateSpec.Client(10),
        Y = CoordinateSpec.Client(20),
        Button = MouseButton.Left,
        Count = 1,
        HoldMs = 0,
        IntervalMs = 20,
        SyncMode = ClickSendMode.Post,
        Hover = true,
        WParamMode = WParamMode.Button
    };

    private static GameWindow Attach(TestWindow hidden)
    {
        var locator = new WindowLocator(NullActionLogger.Instance);
        return GameWindow.AttachByHandle(locator, hidden.Handle)
            ?? throw new InvalidOperationException("Could not attach to test window.");
    }

    private sealed class FakeTransport : IWindowMessageTransport
    {
        public List<SentMessage> Messages { get; } = new();

        public uint? FailMessage { get; init; }

        public int FailureError { get; init; } = 87;

        public WindowMessageSendResult PostMessage(
            IntPtr hWnd,
            uint message,
            IntPtr wParam,
            IntPtr lParam)
        {
            return Record(message, wParam, lParam, synchronous: false);
        }

        public WindowMessageSendResult SendMessage(
            IntPtr hWnd,
            uint message,
            IntPtr wParam,
            IntPtr lParam,
            TimeSpan timeout)
        {
            return Record(message, wParam, lParam, synchronous: true);
        }

        private WindowMessageSendResult Record(
            uint message,
            IntPtr wParam,
            IntPtr lParam,
            bool synchronous)
        {
            if (FailMessage == message)
            {
                return WindowMessageSendResult.Fail(FailureError);
            }

            Messages.Add(new SentMessage(message, wParam, lParam, synchronous));
            return WindowMessageSendResult.Ok;
        }
    }

    private sealed record SentMessage(
        uint Message,
        IntPtr WParam,
        IntPtr LParam,
        bool Synchronous);
}
