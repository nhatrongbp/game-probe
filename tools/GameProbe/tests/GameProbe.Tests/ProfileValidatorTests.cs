using GameProbe.Core.Models;
using GameProbe.Core.Profiles;
using Xunit;

namespace GameProbe.Tests;

public sealed class ProfileValidatorTests
{
    private static string SampleProfilePath =>
        Path.Combine(AppContext.BaseDirectory, "profiles", "fc-online.json");

    [Fact]
    public void DuplicateId_ShouldFail()
    {
        var profile = LoadSample();
        profile.PixelProbes.Add(new PixelProbeSpec
        {
            Id = profile.ClickPoints[0].Id,
            Name = "Duplicate",
            X = CoordinateSpec.Client(1),
            Y = CoordinateSpec.Client(1)
        });

        var result = ProfileValidator.Validate(profile);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("unique", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void PercentOutOfRange_ShouldFail()
    {
        var profile = LoadSample();
        profile.ClickPoints[0].X = CoordinateSpec.Percent(1.5);

        var result = ProfileValidator.Validate(profile);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("percent", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void WrongAnchorAxis_ShouldFail()
    {
        var profile = LoadSample();
        profile.ClickPoints[0].Y = CoordinateSpec.Anchored(Anchor.Left);

        var result = ProfileValidator.Validate(profile);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("anchor", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void InvalidRgb_ShouldFail()
    {
        var profile = LoadSample();
        profile.PixelProbes[0].ExpectedRgb = "blue";

        var result = ProfileValidator.Validate(profile);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("RRGGBB", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void NegativeHoverSettleMs_ShouldFail()
    {
        var profile = LoadSample();
        profile.ClickPoints[0].HoverSettleMs = -1;

        var result = ProfileValidator.Validate(profile);

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            error => error.Contains("hoverSettleMs", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void UnknownEncoding_ShouldFail()
    {
        var profile = LoadSample();
        profile.Memory.Encodings.Add("utf32");

        var result = ProfileValidator.Validate(profile);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("utf32", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void DuplicateHotkey_ShouldFail()
    {
        var profile = LoadSample();
        profile.Hotkeys.ReadPixel = profile.Hotkeys.Panic;

        var result = ProfileValidator.Validate(profile);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("duplicates", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void EmptyTarget_ShouldFail()
    {
        var profile = LoadSample();
        profile.Target.ClassName = "";
        profile.Target.ProcessName = "";
        profile.Target.TitleContains = "";

        var result = ProfileValidator.Validate(profile);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("target", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void NullStrings_ShouldFailGracefully()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "GameProbe.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var path = Path.Combine(tempDir, "nulls.json");
            File.WriteAllText(
                path,
                """
                {
                  "schemaVersion": 1,
                  "name": "Nulls",
                  "target": { "className": null, "processName": null, "titleContains": null },
                  "memory": { "filter": null },
                  "clickPoints": [],
                  "pixelProbes": []
                }
                """);

            var exception = Assert.Throws<ProfileValidationException>(() => new ProfileStore().Load(path));
            Assert.NotEmpty(exception.Errors);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void SendInput_ShouldOnlyWarn()
    {
        var profile = LoadSample();
        profile.ClickPoints[0].SyncMode = ClickSendMode.SendInput;

        var result = ProfileValidator.Validate(profile);

        Assert.True(result.IsValid, string.Join("; ", result.Errors));
        Assert.Contains(result.Warnings, warning => warning.Contains("sendInput", StringComparison.OrdinalIgnoreCase));
    }

    private static Profile LoadSample()
    {
        return new ProfileStore().Load(SampleProfilePath);
    }
}
