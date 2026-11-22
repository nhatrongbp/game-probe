namespace GameProbe.Core.Diagnostics;

public static class GameProbePaths
{
    public static string ApplicationDataRoot { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "GameProbe");

    public static string LocalDataRoot { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "GameProbe");

    public static string DefaultProfilesDirectory => Path.Combine(ApplicationDataRoot, "profiles");

    public static string LogsDirectory => Path.Combine(LocalDataRoot, "logs");

    public static string SnapshotsDirectory => Path.Combine(LocalDataRoot, "snapshots");

    public static string CapturesDirectory => Path.Combine(LocalDataRoot, "captures");

    public static string GetDefaultLogFilePath(DateTimeOffset? timestamp = null)
    {
        var local = (timestamp ?? DateTimeOffset.Now).ToLocalTime();
        return Path.Combine(LogsDirectory, $"gameprobe-{local:yyyyMMdd}.jsonl");
    }

    public static void EnsureBaseDirectories()
    {
        Directory.CreateDirectory(ApplicationDataRoot);
        Directory.CreateDirectory(DefaultProfilesDirectory);
        Directory.CreateDirectory(LocalDataRoot);
        Directory.CreateDirectory(LogsDirectory);
        Directory.CreateDirectory(SnapshotsDirectory);
        Directory.CreateDirectory(CapturesDirectory);
    }
}
