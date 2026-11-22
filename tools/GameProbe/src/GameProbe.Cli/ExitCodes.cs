namespace GameProbe.Cli;

internal static class ExitCodes
{
    public const int Success = 0;
    public const int Usage = 1;
    public const int NotFound = 2;
    public const int AccessDenied = 3;
    public const int OutOfBounds = 4;
    public const int SendFailed = 5;
    public const int Timeout = 5;
    public const int Canceled = 6;
    public const int Failure = 7;
}
