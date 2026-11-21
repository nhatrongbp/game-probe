namespace GameProbe.Core.Windows;

public sealed class WindowAttachmentChangedEventArgs : EventArgs
{
    public WindowAttachmentChangedEventArgs(WindowInfo? previous, WindowInfo? current)
    {
        Previous = previous;
        Current = current;
    }

    public WindowInfo? Previous { get; }

    public WindowInfo? Current { get; }

    public bool Attached => Current is not null;

    public bool Detached => Current is null;
}
