using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Text;
using GameProbe.Core.Diagnostics;
using GameProbe.Core.Interop;

namespace GameProbe.Core.Windows;

public sealed class WindowLocator
{
    private readonly IActionLogger _logger;
    private readonly ConcurrentDictionary<uint, ProcessIdentity> _processCache = new();

    public WindowLocator(IActionLogger? logger = null)
    {
        _logger = logger ?? NullActionLogger.Instance;
    }

    public IReadOnlyList<WindowInfo> EnumerateTopLevelWindows()
    {
        var windows = new List<WindowInfo>();

        NativeMethods.EnumWindowsProc callback = (hwnd, _) =>
        {
            try
            {
                var info = DescribeWindow(hwnd);
                if (info is not null)
                {
                    windows.Add(info);
                }
            }
            catch (Exception ex)
            {
                _logger.Debug(
                    "window.enumerate",
                    $"Skipping HWND 0x{hwnd.ToInt64():X}: {ex.Message}");
            }

            return true;
        };

        if (!NativeMethods.EnumWindows(callback, IntPtr.Zero))
        {
            var error = Marshal.GetLastWin32Error();
            _logger.Warn("window.enumerate", $"EnumWindows failed (Win32 error {error}).");
        }

        return windows;
    }

    public WindowInfo? FindByHandle(IntPtr handle)
    {
        return DescribeWindow(handle);
    }

    public IReadOnlyList<WindowInfo> FindAll(WindowSearchCriteria criteria)
    {
        ArgumentNullException.ThrowIfNull(criteria);

        if (criteria.IsEmpty)
        {
            throw new ArgumentException("At least one search criterion is required.", nameof(criteria));
        }

        return EnumerateTopLevelWindows()
            .Where(criteria.Matches)
            .OrderByDescending(window => window.IsVisible)
            .ThenByDescending(window => (long)window.ClientWidth * window.ClientHeight)
            .ThenBy(window => window.ProcessName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(window => window.ClassName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(window => window.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public WindowInfo? Find(WindowSearchCriteria criteria)
    {
        return FindAll(criteria).FirstOrDefault();
    }

    public WindowInfo? DescribeWindow(IntPtr handle)
    {
        if (handle == IntPtr.Zero || !NativeMethods.IsWindow(handle))
        {
            return null;
        }

        try
        {
            var className = ReadClassName(handle);
            var title = ReadWindowTitle(handle);
            NativeMethods.GetWindowThreadProcessId(handle, out var processId);
            var process = GetProcessIdentity(processId);

            var hasClientRect = NativeMethods.GetClientRect(handle, out var clientRect);
            var hasWindowRect = NativeMethods.GetWindowRect(handle, out var windowRect);
            var dpi = NativeMethods.GetDpiForWindow(handle);
            if (dpi == 0)
            {
                dpi = 96;
            }

            return new WindowInfo
            {
                Handle = handle,
                ProcessId = processId,
                ClassName = className,
                Title = title,
                ProcessName = process.Name,
                ProcessPath = process.Path,
                IsVisible = NativeMethods.IsWindowVisible(handle),
                IsMinimized = NativeMethods.IsIconic(handle),
                IsHung = NativeMethods.IsHungAppWindow(handle),
                WindowLeft = hasWindowRect ? windowRect.Left : 0,
                WindowTop = hasWindowRect ? windowRect.Top : 0,
                WindowWidth = hasWindowRect ? windowRect.Width : 0,
                WindowHeight = hasWindowRect ? windowRect.Height : 0,
                ClientWidth = hasClientRect ? clientRect.Width : 0,
                ClientHeight = hasClientRect ? clientRect.Height : 0,
                Dpi = dpi
            };
        }
        catch (Exception ex)
        {
            _logger.Warn(
                "window.describe",
                $"Could not describe HWND 0x{handle.ToInt64():X}: {ex.Message}");
            return null;
        }
    }

    private static string ReadClassName(IntPtr handle)
    {
        var buffer = new StringBuilder(256);
        var length = NativeMethods.GetClassNameW(handle, buffer, buffer.Capacity);
        return length > 0 ? buffer.ToString(0, length) : "";
    }

    private static string ReadWindowTitle(IntPtr handle)
    {
        var length = NativeMethods.GetWindowTextLengthW(handle);
        if (length <= 0)
        {
            return "";
        }

        var capacity = Math.Min(length + 1, 4096);
        var buffer = new StringBuilder(capacity);
        var copied = NativeMethods.GetWindowTextW(handle, buffer, buffer.Capacity);
        return copied > 0 ? buffer.ToString(0, copied) : "";
    }

    private ProcessIdentity GetProcessIdentity(uint processId)
    {
        if (processId == 0)
        {
            return ProcessIdentity.Empty;
        }

        if (_processCache.TryGetValue(processId, out var cached))
        {
            return cached;
        }

        var identity = ResolveProcessIdentity(processId);
        if (!string.IsNullOrEmpty(identity.Path))
        {
            _processCache.TryAdd(processId, identity);
        }

        return identity;
    }

    private ProcessIdentity ResolveProcessIdentity(uint processId)
    {
        var handle = NativeMethods.OpenProcess(
            NativeMethods.ProcessQueryLimitedInformation,
            bInheritHandle: false,
            processId);

        if (handle == IntPtr.Zero)
        {
            return ProcessIdentity.Empty;
        }

        try
        {
            var buffer = new StringBuilder(32768);
            var size = buffer.Capacity;

            if (!NativeMethods.QueryFullProcessImageNameW(handle, 0, buffer, ref size))
            {
                return ProcessIdentity.Empty;
            }

            var path = buffer.ToString(0, size);
            return new ProcessIdentity(Path.GetFileName(path), path);
        }
        finally
        {
            NativeMethods.CloseHandle(handle);
        }
    }

    private readonly record struct ProcessIdentity(string Name, string Path)
    {
        public static ProcessIdentity Empty { get; } = new("", "");
    }
}
