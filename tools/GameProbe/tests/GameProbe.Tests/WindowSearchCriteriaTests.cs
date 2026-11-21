using GameProbe.Core.Models;
using GameProbe.Core.Windows;
using Xunit;

namespace GameProbe.Tests;

public sealed class WindowSearchCriteriaTests
{
    [Fact]
    public void Matches_ProcessNameWithoutExtension()
    {
        var window = CreateWindowInfo();
        var criteria = new WindowSearchCriteria { ProcessName = "fczf" };

        Assert.True(criteria.Matches(window));
    }

    [Fact]
    public void Matches_ProcessNameWithExtension_CaseInsensitive()
    {
        var window = CreateWindowInfo();
        var criteria = new WindowSearchCriteria { ProcessName = "FCZF.EXE" };

        Assert.True(criteria.Matches(window));
    }

    [Fact]
    public void Matches_TitleContains_CaseInsensitive()
    {
        var window = CreateWindowInfo();
        var criteria = new WindowSearchCriteria { TitleContains = "lobby" };

        Assert.True(criteria.Matches(window));
    }

    [Fact]
    public void Matches_RequiresVisibleByDefault()
    {
        var window = CreateWindowInfo(visible: false);
        var criteria = new WindowSearchCriteria { ClassName = "FIFAKC" };

        Assert.False(criteria.Matches(window));
    }

    [Fact]
    public void Matches_AllowsHiddenWhenRequested()
    {
        var window = CreateWindowInfo(visible: false);
        var criteria = new WindowSearchCriteria { ClassName = "FIFAKC", RequireVisible = false };

        Assert.True(criteria.Matches(window));
    }

    [Fact]
    public void DoesNotMatch_DifferentClass()
    {
        var window = CreateWindowInfo();
        var criteria = new WindowSearchCriteria { ClassName = "Notepad" };

        Assert.False(criteria.Matches(window));
    }

    [Fact]
    public void EmptyCriteria_IsDetected()
    {
        Assert.True(new WindowSearchCriteria().IsEmpty);
        Assert.False(new WindowSearchCriteria { ProcessId = 10 }.IsEmpty);
    }

    private static WindowInfo CreateWindowInfo(bool visible = true) => new()
    {
        Handle = new IntPtr(0x1234),
        ProcessId = 4242,
        ProcessName = "fczf.exe",
        ProcessPath = @"C:\Games\FC Online\fczf.exe",
        ClassName = "FIFAKC",
        Title = "FC Online - Lobby",
        IsVisible = visible,
        ClientWidth = 1280,
        ClientHeight = 720,
        Dpi = 96
    };
}
