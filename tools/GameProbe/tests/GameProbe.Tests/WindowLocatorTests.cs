using System.Runtime.InteropServices;
using GameProbe.Core.Diagnostics;
using GameProbe.Core.Windows;
using Xunit;

namespace GameProbe.Tests;

public sealed class WindowLocatorTests
{
    private const uint WsPopup = 0x80000000;

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateWindowExW(
        uint dwExStyle,
        string lpClassName,
        string lpWindowName,
        uint dwStyle,
        int x,
        int y,
        int nWidth,
        int nHeight,
        IntPtr hWndParent,
        IntPtr hMenu,
        IntPtr hInstance,
        IntPtr lpParam);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyWindow(IntPtr hWnd);

    [Fact]
    public void DescribeWindow_ReadsNativeWindowProperties()
    {
        using var hidden = new HiddenWindow("GameProbe Hidden Test", 321, 123);

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
        using var hidden = new HiddenWindow(title, 200, 100);

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
        using var hidden = new HiddenWindow("GameProbe Refresh Test", 400, 200);
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
        var hidden = new HiddenWindow("GameProbe Monitor Test", 300, 150);
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

    private sealed class HiddenWindow : IDisposable
    {
        private IntPtr _handle;

        public HiddenWindow(string title, int width, int height)
        {
            _handle = CreateWindowExW(
                0,
                "STATIC",
                title,
                WsPopup,
                0,
                0,
                width,
                height,
                IntPtr.Zero,
                IntPtr.Zero,
                IntPtr.Zero,
                IntPtr.Zero);

            if (_handle == IntPtr.Zero)
            {
                throw new InvalidOperationException(
                    $"CreateWindowExW failed (Win32 {Marshal.GetLastWin32Error()}).");
            }
        }

        public IntPtr Handle => _handle;

        public void Dispose()
        {
            if (_handle != IntPtr.Zero)
            {
                DestroyWindow(_handle);
                _handle = IntPtr.Zero;
            }
        }
    }
}
