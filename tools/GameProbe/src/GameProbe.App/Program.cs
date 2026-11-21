using GameProbe.Core.Diagnostics;

namespace GameProbe.App;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();

        GameProbePaths.EnsureBaseDirectories();
        var logger = new JsonLineActionLogger();

        logger.Info(
            "app.start",
            "GameProbe starting",
            new Dictionary<string, object?>
            {
                ["version"] = typeof(Program).Assembly.GetName().Version?.ToString(3),
                ["os"] = Environment.OSVersion.VersionString
            });

        Application.Run(new MainForm(logger));

        logger.Info("app.stop", "GameProbe stopped");
    }
}
