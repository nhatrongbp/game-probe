using GameProbe.Core.Models;
using GameProbe.Core.Profiles;
using Xunit;

namespace GameProbe.Tests;

public sealed class PriceRefreshProfileTests
{
    private static string SampleProfilePath =>
        Path.Combine(AppContext.BaseDirectory, "profiles", "fc-online.json");

    [Fact]
    public void SampleProfile_WithPriceRefresh_IsValid()
    {
        var profile = new ProfileStore().Load(SampleProfilePath);

        Assert.NotNull(profile.PriceRefresh);
        Assert.Contains(profile.Regions, region => region.Id == "buy-dialog-max-price");
        Assert.Contains(profile.ClickPoints, point => point.Id == "relist");
        Assert.Contains(profile.PixelProbes, probe => probe.Id == "buy-dialog-red");
    }

    [Fact]
    public void MissingBuyPointReference_ShouldFail()
    {
        var profile = new ProfileStore().Load(SampleProfilePath);
        profile.PriceRefresh!.BuyPointId = "missing-buy";

        var result = ProfileValidator.Validate(profile);

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            error => error.Contains("missing-buy", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void MissingPopupExpectedRgb_ShouldFail()
    {
        var profile = new ProfileStore().Load(SampleProfilePath);
        var probe = profile.PixelProbes.First(
            item => item.Id == profile.PriceRefresh!.PopupProbeId);
        probe.ExpectedRgb = null;

        var result = ProfileValidator.Validate(profile);

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            error => error.Contains("expectedRgb", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ZeroGuardTimeout_ShouldFail()
    {
        var profile = new ProfileStore().Load(SampleProfilePath);
        profile.PriceRefresh!.RelistGuardTimeoutMs = 0;

        var result = ProfileValidator.Validate(profile);

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            error => error.Contains("relistGuardTimeoutMs", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void DuplicateRegionId_ShouldFail()
    {
        var profile = new ProfileStore().Load(SampleProfilePath);
        profile.Regions.Add(new RegionSpec
        {
            Id = profile.ClickPoints[0].Id,
            Name = "duplicate",
            X = CoordinateSpec.Client(0),
            Y = CoordinateSpec.Client(0),
            Width = 2,
            Height = 2
        });

        var result = ProfileValidator.Validate(profile);

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            error => error.Contains("unique", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ZeroWidthRegion_ShouldFail()
    {
        var profile = new ProfileStore().Load(SampleProfilePath);
        profile.Regions[0].Width = 0;

        var result = ProfileValidator.Validate(profile);

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            error => error.Contains("width", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void MinChangedPixelsAboveRegionArea_ShouldFail()
    {
        var profile = new ProfileStore().Load(SampleProfilePath);
        profile.PriceRefresh!.MinChangedPixels = 1000000;

        var result = ProfileValidator.Validate(profile);

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            error => error.Contains("minChangedPixels", StringComparison.OrdinalIgnoreCase));
    }
}
