using GameProbe.Core.Capture;
using Xunit;

namespace GameProbe.Tests;

public sealed class ColorClassifierTests
{
    [Theory]
    [InlineData(255, 255, 255, 0)]
    [InlineData(128, 128, 128, 0)]
    [InlineData(1, 1, 1, 0)]
    [InlineData(0, 0, 0, 3)]
    [InlineData(0, 0, 255, 1)]
    [InlineData(0, 160, 255, 1)]
    [InlineData(255, 255, 0, 2)]
    [InlineData(255, 0, 0, 3)]
    [InlineData(0, 255, 0, 3)]
    [InlineData(0, 255, 255, 3)]
    public void Classify_MatchesFfzikRules(int r, int g, int b, int expected)
    {
        Assert.Equal(expected, ColorClassifier.Classify(r, g, b));
    }

    [Fact]
    public void Classify_BlueBoundary_UsesTolerance()
    {
        Assert.Equal(PixelColorClass.Blue, ColorClassifier.Classify(0, 0, 20, tolerance: 8));
        Assert.Equal(PixelColorClass.Other, ColorClassifier.Classify(0, 0, 20, tolerance: 30));
    }

    [Fact]
    public void Classify_WhiteBoundary_RequiresPositiveRed()
    {
        Assert.Equal(PixelColorClass.WhiteOrGray, ColorClassifier.Classify(10, 10, 10, tolerance: 8));
        Assert.Equal(PixelColorClass.Other, ColorClassifier.Classify(0, 0, 0, tolerance: 8));
    }

    [Fact]
    public void IsClose_ComparesAllChannelsWithinTolerance()
    {
        var actual = new PixelColor(0, 160, 255);
        var expected = new PixelColor(0, 165, 250);

        Assert.True(ColorClassifier.IsClose(actual, expected, tolerance: 5));
        Assert.False(ColorClassifier.IsClose(actual, expected, tolerance: 4));
    }

    [Fact]
    public void FromHex_ParsesAndFormats()
    {
        var color = PixelColor.FromHex("#00a0FF");

        Assert.Equal(new PixelColor(0x00, 0xA0, 0xFF), color);
        Assert.Equal("#00A0FF", color.Hex);
        Assert.Equal("(0,160,255)", color.RgbText);
    }

    [Theory]
    [InlineData("00A0FF")]
    [InlineData("#00A0FF")]
    public void FromHex_AcceptsOptionalHash(string value)
    {
        Assert.Equal(new PixelColor(0, 160, 255), PixelColor.FromHex(value));
    }

    [Theory]
    [InlineData("")]
    [InlineData("#12345")]
    [InlineData("#GGGGGG")]
    [InlineData("red")]
    public void FromHex_InvalidValue_Throws(string value)
    {
        Assert.Throws<FormatException>(() => PixelColor.FromHex(value));
    }

    [Fact]
    public void ColorClassName_ReturnsKnownLabels()
    {
        Assert.Equal("trắng/xám", PixelColorClass.Describe(PixelColorClass.WhiteOrGray));
        Assert.Equal("xanh dương", PixelColorClass.Describe(PixelColorClass.Blue));
        Assert.Equal("vàng", PixelColorClass.Describe(PixelColorClass.Yellow));
        Assert.Equal("khác/đen", PixelColorClass.Describe(PixelColorClass.Other));
    }
}
