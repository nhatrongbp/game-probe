namespace GameProbe.Cli.Commands;

internal sealed class ConsoleCancellation : IDisposable
{
    private readonly CancellationTokenSource _source = new();
    private readonly ConsoleCancelEventHandler _handler;
    private bool _disposed;

    public ConsoleCancellation()
    {
        _handler = (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            _source.Cancel();
        };

        Console.CancelKeyPress += _handler;
    }

    public CancellationToken Token => _source.Token;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Console.CancelKeyPress -= _handler;
        _source.Dispose();
    }
}
