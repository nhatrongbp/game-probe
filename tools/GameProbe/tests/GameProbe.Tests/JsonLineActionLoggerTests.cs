using System.Text.Json;
using GameProbe.Core.Diagnostics;
using Xunit;

namespace GameProbe.Tests;

public sealed class JsonLineActionLoggerTests
{
    [Fact]
    public void Log_ShouldWriteOneJsonLine()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "GameProbe.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var path = Path.Combine(tempDir, "test.jsonl");
            var logger = new JsonLineActionLogger(path);

            logger.Info(
                "test.action",
                "hello",
                new Dictionary<string, object?> { ["value"] = 42 });

            var lines = File.ReadAllLines(path);
            Assert.Single(lines);

            using var document = JsonDocument.Parse(lines[0]);
            var root = document.RootElement;

            Assert.Equal("info", root.GetProperty("level").GetString());
            Assert.Equal("test.action", root.GetProperty("action").GetString());
            Assert.Equal("hello", root.GetProperty("message").GetString());
            Assert.Equal(42, root.GetProperty("data").GetProperty("value").GetInt32());
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }
}
