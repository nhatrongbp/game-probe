namespace GameProbe.Core.Scenario;

public interface ISleeper
{
    void Sleep(int milliseconds, CancellationToken cancellationToken);
}

public sealed class ThreadSleeper : ISleeper
{
    public static ThreadSleeper Instance { get; } = new();

    private ThreadSleeper()
    {
    }

    public void Sleep(int milliseconds, CancellationToken cancellationToken)
    {
        if (milliseconds <= 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return;
        }

        if (cancellationToken.WaitHandle.WaitOne(milliseconds))
        {
            cancellationToken.ThrowIfCancellationRequested();
        }
    }
}
