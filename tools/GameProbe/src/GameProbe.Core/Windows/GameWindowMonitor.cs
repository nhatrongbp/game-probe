using GameProbe.Core.Diagnostics;

namespace GameProbe.Core.Windows;

public sealed class GameWindowMonitor : IDisposable
{
    private readonly WindowLocator _locator;
    private readonly WindowSearchCriteria _criteria;
    private readonly TimeSpan _interval;
    private readonly IActionLogger _logger;
    private readonly object _gate = new();

    private System.Threading.Timer? _timer;
    private WindowInfo? _current;
    private bool _disposed;

    public GameWindowMonitor(
        WindowLocator locator,
        WindowSearchCriteria criteria,
        TimeSpan? interval = null,
        IActionLogger? logger = null)
    {
        _locator = locator ?? throw new ArgumentNullException(nameof(locator));
        _criteria = criteria ?? throw new ArgumentNullException(nameof(criteria));

        if (criteria.IsEmpty)
        {
            throw new ArgumentException("At least one search criterion is required.", nameof(criteria));
        }

        _interval = interval ?? TimeSpan.FromSeconds(1);
        if (_interval < TimeSpan.FromMilliseconds(200) || _interval > TimeSpan.FromMinutes(1))
        {
            throw new ArgumentOutOfRangeException(
                nameof(interval),
                "Interval must be between 200 ms and 1 minute.");
        }

        _logger = logger ?? NullActionLogger.Instance;
    }

    public event EventHandler<WindowAttachmentChangedEventArgs>? AttachmentChanged;

    public WindowInfo? Current
    {
        get
        {
            lock (_gate)
            {
                return _current;
            }
        }
    }

    public bool IsAttached => Current is not null;

    public void Start()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(GameWindowMonitor));
            }

            _timer ??= new System.Threading.Timer(
                static state => ((GameWindowMonitor)state!).CheckSafely(),
                this,
                TimeSpan.Zero,
                _interval);
        }
    }

    public void Stop()
    {
        lock (_gate)
        {
            _timer?.Dispose();
            _timer = null;
        }
    }

    public bool CheckNow()
    {
        WindowInfo? previous;
        lock (_gate)
        {
            if (_disposed)
            {
                return false;
            }

            previous = _current;
        }

        WindowInfo? next = null;

        if (previous is not null)
        {
            var refreshed = _locator.FindByHandle(previous.Handle);
            if (refreshed is not null && _criteria.Matches(refreshed))
            {
                next = refreshed;
            }
        }

        next ??= _locator.Find(_criteria);

        bool changed;
        lock (_gate)
        {
            if (_disposed)
            {
                return false;
            }

            changed = previous?.Handle != next?.Handle;
            _current = next;
        }

        if (changed)
        {
            if (next is null)
            {
                _logger.Warn("window.monitor", "Target window was lost.");
            }
            else
            {
                _logger.Info(
                    "window.monitor",
                    "Target window attached.",
                    new Dictionary<string, object?>
                    {
                        ["hwnd"] = next.HandleHex,
                        ["pid"] = next.ProcessId,
                        ["title"] = next.Title
                    });
            }

            AttachmentChanged?.Invoke(this, new WindowAttachmentChangedEventArgs(previous, next));
        }

        return next is not null;
    }

    private void CheckSafely()
    {
        try
        {
            CheckNow();
        }
        catch (Exception ex)
        {
            _logger.Error("window.monitor", $"Window monitor check failed: {ex.Message}");
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _timer?.Dispose();
            _timer = null;
        }
    }
}
