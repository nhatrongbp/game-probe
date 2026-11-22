using System.Text.Json;
using GameProbe.Core.Models;
using GameProbe.Core.Profiles;
using Xunit;

namespace GameProbe.Tests;

public sealed class ProfileStoreTests
{
    private static string SampleProfilePath =>
        Path.Combine(AppContext.BaseDirectory, "profiles", "fc-online.json");

    private static string TestTargetProfilePath =>
        Path.Combine(AppContext.BaseDirectory, "profiles", "test-target.json");

    [Fact]
    public void SampleProfile_ShouldBeValid()
    {
        var store = new ProfileStore();

        var profile = store.Load(SampleProfilePath);
        var validation = ProfileValidator.Validate(profile);

        Assert.True(validation.IsValid, string.Join("; ", validation.Errors));
        Assert.Equal("FC Online", profile.Name);
        Assert.Equal(7, profile.ClickPoints.Count);
        Assert.Equal(4, profile.PixelProbes.Count);
        Assert.Single(profile.Regions);
        Assert.NotNull(profile.PriceRefresh);
        Assert.Equal(250, profile.ClickPoints.First(point => point.Id == "relist").HoverSettleMs);
        Assert.Equal(1000, profile.PriceRefresh!.RelistGuardTimeoutMs);
        Assert.Equal(Anchor.Center, profile.ClickPoints[1].X.Anchor);
        Assert.Equal(30, profile.ClickPoints[1].X.Offset);
        Assert.Equal(Anchor.Bottom, profile.ClickPoints[1].Y.Anchor);
        Assert.Equal(-67, profile.ClickPoints[1].Y.Offset);
    }

    [Fact]
    public void TestTargetProfile_ShouldBeValid()
    {
        var store = new ProfileStore();

        var profile = store.Load(TestTargetProfilePath);
        var validation = ProfileValidator.Validate(profile);

        Assert.True(validation.IsValid, string.Join("; ", validation.Errors));
        Assert.Equal("TestTarget fixture", profile.Name);
        Assert.Equal(6, profile.ClickPoints.Count);
        Assert.Equal(6, profile.PixelProbes.Count);
        Assert.Equal("GameProbe.TestTarget", profile.Target.ProcessName);
    }

    [Fact]
    public void SaveAndLoad_ShouldRoundTrip()
    {
        var tempDir = CreateTempDirectory();
        try
        {
            var store = new ProfileStore();
            var profile = store.Load(SampleProfilePath);
            var target = Path.Combine(tempDir, "profile.json");

            store.Save(profile, target);
            var loaded = store.Load(target);

            Assert.Equal(profile.Name, loaded.Name);
            Assert.Equal(profile.SchemaVersion, loaded.SchemaVersion);
            Assert.Equal(profile.ClickPoints.Count, loaded.ClickPoints.Count);
            Assert.Equal(profile.PixelProbes.Count, loaded.PixelProbes.Count);
            Assert.Equal(profile.Regions.Count, loaded.Regions.Count);
            Assert.NotNull(loaded.PriceRefresh);
            Assert.Equal(
                profile.PriceRefresh!.PopupProbeId,
                loaded.PriceRefresh!.PopupProbeId);
            Assert.Equal(
                profile.PriceRefresh.RelistGuardTimeoutMs,
                loaded.PriceRefresh.RelistGuardTimeoutMs);
            Assert.Equal(
                profile.ClickPoints.First(point => point.Id == "relist").HoverSettleMs,
                loaded.ClickPoints.First(point => point.Id == "relist").HoverSettleMs);
            Assert.Equal(profile.ClickPoints[1].X.Offset, loaded.ClickPoints[1].X.Offset);
            Assert.Equal(profile.Hotkeys.Panic, loaded.Hotkeys.Panic);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Save_InvalidProfile_ShouldThrow()
    {
        var tempDir = CreateTempDirectory();
        try
        {
            var store = new ProfileStore();
            var profile = store.Load(SampleProfilePath);
            profile.Name = "";

            Assert.Throws<ProfileValidationException>(() =>
                store.Save(profile, Path.Combine(tempDir, "invalid.json")));
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Load_MissingFile_ShouldThrow()
    {
        var store = new ProfileStore();
        var missing = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.missing.json");

        Assert.Throws<FileNotFoundException>(() => store.Load(missing));
    }

    [Fact]
    public void Load_MalformedJson_ShouldThrow()
    {
        var tempDir = CreateTempDirectory();
        try
        {
            var path = Path.Combine(tempDir, "broken.json");
            File.WriteAllText(path, "{ \"schemaVersion\": 1, ");

            var store = new ProfileStore();
            Assert.Throws<JsonException>(() => store.Load(path));
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "GameProbe.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
