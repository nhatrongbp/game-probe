using GameProbe.Core.Interop;

namespace GameProbe.Core.Windows;

public sealed class GameWindow
{
    private readonly WindowLocator _locator;
    private WindowInfo _info;

    public GameWindow(WindowLocator locator, WindowInfo info)
    {
        _locator = locator ?? throw new ArgumentNullException(nameof(locator));
        _info = info ?? throw new ArgumentNullException(nameof(info));
    }

    public WindowInfo Info => _info;

    public IntPtr Handle => _info.Handle;

    public bool IsAlive => _info.Handle != IntPtr.Zero && NativeMethods.IsWindow(_info.Handle);

    public bool Refresh()
    {
        var info = _locator.FindByHandle(_info.Handle);
        if (info is null)
        {
            return false;
        }

        _info = info;
        return true;
    }

    public static GameWindow? Attach(WindowLocator locator, WindowSearchCriteria criteria)
    {
        ArgumentNullException.ThrowIfNull(locator);
        ArgumentNullException.ThrowIfNull(criteria);

        var info = locator.Find(criteria);
        return info is null ? null : new GameWindow(locator, info);
    }

    public static GameWindow? AttachByHandle(WindowLocator locator, IntPtr handle)
    {
        ArgumentNullException.ThrowIfNull(locator);

        var info = locator.FindByHandle(handle);
        return info is null ? null : new GameWindow(locator, info);
    }
}
