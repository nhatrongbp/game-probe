using GameProbe.Core.Diagnostics;
using GameProbe.Core.Windows;
using Xunit;

namespace GameProbe.Tests;

public sealed class WindowLocatorTests
{
    [Fact]
    public void DescribeWindow_ReadsNativeWindowProperties()
    {
        using var hidden = new TestWindow("GameProbe Hidden Test", 321, 123);

        var locator = new WindowLocator(NullActionLogger.Instance);
        var info = locator.DescribeWindow(hidden.Handle);

        Assert.NotNull(info);
        Assert.Equal(hidden.Handle, info.Handle);
        Assert.Equal("GameProbe Hidden Test", info.Title);
        Assert.Equal(321, info.ClientWidth);
        Assert.Equal(123, info.ClientHeight);
        Assert.False(info.IsVisible);
        Assert.Equal((uint)Environment.ProcessId, info.ProcessId);
        Assert.False(string.IsNullOrWhiteSpace(info.ClassName));
        Assert.False(string.IsNullOrWhiteSpace(info.ProcessName));
    }

    [Fact]
    public void Find_ByTitle_FindsHiddenWindow()
    {
        var title = "GameProbe Hidden Search " + Guid.NewGuid().ToString("N");
        using var hidden = new TestWindow(title, 200, 100);

        var locator = new WindowLocator(NullActionLogger.Instance);
        var found = locator.Find(new WindowSearchCriteria
        {
            TitleContains = title,
            RequireVisible = false
        });

        Assert.NotNull(found);
        Assert.Equal(hidden.Handle, found.Handle);
    }

    [Fact]
    public void FindByHandle_InvalidHandle_ReturnsNull()
    {
        var locator = new WindowLocator(NullActionLogger.Instance);

        Assert.Null(locator.FindByHandle(IntPtr.Zero));
        Assert.Null(locator.FindByHandle(new IntPtr(0x12345678)));
    }

    [Fact]
    public void GameWindow_Refresh_ReadsLatestInfo()
    {
        using var hidden = new TestWindow("GameProbe Refresh Test", 400, 200);
        var locator = new WindowLocator(NullActionLogger.Instance);

        var gameWindow = GameWindow.AttachByHandle(locator, hidden.Handle);

        Assert.NotNull(gameWindow);
        Assert.True(gameWindow.IsAlive);
        Assert.True(gameWindow.Refresh());
        Assert.Equal(hidden.Handle, gameWindow.Handle);
    }

    [Fact]
    public void Monitor_AttachesAndDetaches()
    {
        var hidden = new TestWindow("GameProbe Monitor Test", 300, 150);
        var locator = new WindowLocator(NullActionLogger.Instance);
        using var monitor = new GameWindowMonitor(
            locator,
            new WindowSearchCriteria
            {
                Handle = hidden.Handle,
                RequireVisible = false
            },
            TimeSpan.FromMilliseconds(500),
            NullActionLogger.Instance);

        var events = new List<WindowAttachmentChangedEventArgs>();
        monitor.AttachmentChanged += (_, args) => events.Add(args);

        try
        {
            Assert.True(monitor.CheckNow());
            Assert.NotNull(monitor.Current);
            Assert.True(monitor.IsAttached);

            hidden.Dispose();

            Assert.False(monitor.CheckNow());
            Assert.Null(monitor.Current);
            Assert.False(monitor.IsAttached);
            Assert.Equal(2, events.Count);
            Assert.True(events[0].Attached);
            Assert.True(events[1].Detached);
        }
        finally
        {
            hidden.Dispose();
        }
    }
}
