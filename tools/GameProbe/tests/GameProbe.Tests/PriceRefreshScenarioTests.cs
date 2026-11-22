using GameProbe.Core.Capture;
using GameProbe.Core.Diagnostics;
using GameProbe.Core.Input;
using GameProbe.Core.Models;
using GameProbe.Core.Scenario;
using GameProbe.Core.Windows;
using Xunit;

namespace GameProbe.Tests;

public sealed class PriceRefreshScenarioTests
{
    [Fact]
    public void DryRun_Changed_StopsBeforeBuy()
    {
        var state = new FakeState { ChangeOnOpen = 2 };
        var result = Run(state, confirmBuy: false, maxAttempts: 5);

        Assert.Equal(PriceRefreshOutcome.DryRunChanged, result.Outcome);
        Assert.Equal(2, result.Attempts);
        Assert.Equal(new[] { "relist", "cancel", "relist" }, state.ClickedPoints);
        Assert.False(state.Bought);
        Assert.NotNull(result.Baseline);
        Assert.NotNull(result.LastCurrent);
        Assert.True(result.LastDiff?.IsDifferent);
    }

    [Fact]
    public void ConfirmBuy_ClicksPriceAndBuy()
    {
        var state = new FakeState { ChangeOnOpen = 2 };
        var result = Run(state, confirmBuy: true, maxAttempts: 5);

        Assert.Equal(PriceRefreshOutcome.Bought, result.Outcome);
        Assert.Equal(2, result.Attempts);
        Assert.Equal(
            new[] { "relist", "cancel", "relist", "price", "buy" },
            state.ClickedPoints);
        Assert.True(state.Bought);
    }

    [Fact]
    public void SamePrice_CancelsAndReachesMaxAttempts()
    {
        var state = new FakeState { ChangeOnOpen = int.MaxValue };
        var result = Run(state, confirmBuy: true, maxAttempts: 3);

        Assert.Equal(PriceRefreshOutcome.MaxAttemptsReached, result.Outcome);
        Assert.Equal(3, result.Attempts);
        Assert.Equal(
            new[] { "relist", "cancel", "relist", "cancel", "relist", "cancel" },
            state.ClickedPoints);
        Assert.False(state.Bought);
    }

    [Fact]
    public void PopupNeverOpens_TimesOut()
    {
        var state = new FakeState { OpenOnRelist = false };

        var exception = Assert.Throws<GameProbeException>(
            () => Run(state, confirmBuy: false, maxAttempts: 3));

        Assert.Equal(GameProbeErrorKind.Timeout, exception.Kind);
        Assert.Equal(new[] { "relist" }, state.ClickedPoints);
    }

    [Fact]
    public void GuardMismatch_AbortsBeforeRelist()
    {
        var state = new FakeState { GuardMatches = false };

        var exception = Assert.Throws<GameProbeException>(
            () => Run(state, confirmBuy: false, maxAttempts: 3));

        Assert.Equal(GameProbeErrorKind.InvalidConfiguration, exception.Kind);
        Assert.Empty(state.ClickedPoints);
    }

    [Fact]
    public void PopupAlreadyOpen_CancelsBeforeBaseline()
    {
        var state = new FakeState { PopupOpen = true, ChangeOnOpen = 2 };
        var result = Run(state, confirmBuy: false, maxAttempts: 5);

        Assert.Equal(PriceRefreshOutcome.DryRunChanged, result.Outcome);
        Assert.Equal(
            new[] { "cancel", "relist", "cancel", "relist" },
            state.ClickedPoints);
    }

    [Fact]
    public void BlankPriceRegion_TimesOut()
    {
        var state = new FakeState { RegionBlank = true, ChangeOnOpen = 2 };

        var exception = Assert.Throws<GameProbeException>(
            () => Run(state, confirmBuy: false, maxAttempts: 3));

        Assert.Equal(GameProbeErrorKind.Timeout, exception.Kind);
    }

    private static PriceRefreshResult Run(
        FakeState state,
        bool confirmBuy,
        int maxAttempts)
    {
        using var testWindow = new TestWindow("price-refresh-test", 320, 200);
        var locator = new WindowLocator();
        var info = locator.FindByHandle(testWindow.Handle);
        Assert.NotNull(info);

        var window = new GameWindow(locator, info);
        var profile = CreateProfile(maxAttempts);

        var scenario = new PriceRefreshScenario(
            new FakeClickExecutor(state),
            new FakePixelReader(state),
            new FakeRegionSource(state),
            new NoopSleeper());

        return scenario.Run(
            window,
            profile,
            confirmBuy,
            CancellationToken.None);
    }

    private static Profile CreateProfile(int maxAttempts)
    {
        return new Profile
        {
            Name = "price-test",
            ClickPoints =
            {
                Click("relist"),
                Click("price"),
                Click("cancel"),
                Click("buy")
            },
            PixelProbes =
            {
                new PixelProbeSpec
                {
                    Id = "popup",
                    X = CoordinateSpec.Client(0),
                    Y = CoordinateSpec.Client(0),
                    ExpectedRgb = "#ED4428",
                    Tolerance = 16
                },
                new PixelProbeSpec
                {
                    Id = "guard",
                    X = CoordinateSpec.Client(0),
                    Y = CoordinateSpec.Client(0),
                    ExpectedRgb = "#08F368",
                    Tolerance = 24
                }
            },
            Regions =
            {
                new RegionSpec
                {
                    Id = "price-region",
                    X = CoordinateSpec.Client(0),
                    Y = CoordinateSpec.Client(0),
                    Width = 16,
                    Height = 1
                }
            },
            PriceRefresh = new PriceRefreshOptions
            {
                RelistPointId = "relist",
                RelistGuardProbeId = "guard",
                PopupProbeId = "popup",
                PriceRegionId = "price-region",
                PricePointId = "price",
                CancelPointId = "cancel",
                BuyPointId = "buy",
                StartDelayMs = 0,
                SettleMs = 0,
                StableFrameGapMs = 50,
                StableTimeoutMs = 500,
                PollIntervalMs = 20,
                PopupTimeoutMs = 100,
                CloseTimeoutMs = 200,
                AttemptDelayMs = 0,
                MaxAttempts = maxAttempts,
                PriceClickDelayMs = 0,
                ChannelTolerance = 0,
                MinChangedPixels = 1
            }
        };
    }

    private static ClickPoint Click(string id)
    {
        return new ClickPoint
        {
            Id = id,
            Name = id,
            X = CoordinateSpec.Client(0),
            Y = CoordinateSpec.Client(0)
        };
    }

    private sealed class FakeState
    {
        public bool PopupOpen;
        public bool OpenOnRelist = true;
        public bool GuardMatches = true;
        public bool Bought;
        public bool RegionBlank;
        public int OpenCount;
        public int ChangeOnOpen = int.MaxValue;
        public List<string> ClickedPoints { get; } = new();
    }

    private sealed class FakeClickExecutor : IClickExecutor
    {
        private readonly FakeState _state;

        public FakeClickExecutor(FakeState state)
        {
            _state = state;
        }

        public ClickExecutionResult Click(
            GameWindow window,
            ClickPoint point,
            CancellationToken cancellationToken = default)
        {
            _state.ClickedPoints.Add(point.Id);

            switch (point.Id)
            {
                case "relist":
                    if (_state.OpenOnRelist)
                    {
                        _state.OpenCount++;
                        _state.PopupOpen = true;
                    }

                    break;

                case "cancel":
                    _state.PopupOpen = false;
                    break;

                case "buy":
                    _state.PopupOpen = false;
                    _state.Bought = true;
                    break;
            }

            return new ClickExecutionResult(0, 0, 1, TimeSpan.Zero);
        }
    }

    private sealed class FakePixelReader : IPixelReader
    {
        private static readonly PixelColor Red = new(0xED, 0x44, 0x28);
        private static readonly PixelColor Dark = new(0x2C, 0x39, 0x2B);
        private static readonly PixelColor Green = new(0x08, 0xF3, 0x68);
        private static readonly PixelColor Gray = new(0x4C, 0x50, 0x50);

        private readonly FakeState _state;

        public FakePixelReader(FakeState state)
        {
            _state = state;
        }

        public PixelReadResult Read(GameWindow window, PixelProbeSpec probe)
        {
            if (probe.Id == "guard")
            {
                return CreateResult(probe, _state.GuardMatches ? Green : Gray, _state.GuardMatches);
            }

            return CreateResult(probe, _state.PopupOpen ? Red : Dark, _state.PopupOpen);
        }

        private static PixelReadResult CreateResult(
            PixelProbeSpec probe,
            PixelColor color,
            bool matches)
        {
            return new PixelReadResult(
                0,
                0,
                color,
                color.Classify(),
                PixelCaptureMethod.BitBlt,
                probe.Tolerance,
                probe.ExpectedClass,
                null,
                probe.ExpectedRgb,
                matches,
                matches,
                DateTimeOffset.Now);
        }
    }

    private sealed class FakeRegionSource : IRegionSource
    {
        private readonly FakeState _state;

        public FakeRegionSource(FakeState state)
        {
            _state = state;
        }

        public RegionCaptureResult Capture(
            IntPtr windowHandle,
            int x,
            int y,
            int width,
            int height)
        {
            var changed = _state.OpenCount >= _state.ChangeOnOpen;
            var pixels = new byte[width * height * 4];
            for (var i = 0; i < pixels.Length; i += 4)
            {
                pixels[i] = 0xF7;
                pixels[i + 1] = 0xFC;
                pixels[i + 2] = 0xFC;
                pixels[i + 3] = 0xFF;
            }

            // Draw a small dark "text" block so the scenario's content guard passes.
            if (!_state.RegionBlank)
            {
                for (var i = 0; i < Math.Min(10, width * height); i++)
                {
                    var offset = i * 4;
                    pixels[offset] = 0x40;
                    pixels[offset + 1] = 0x40;
                    pixels[offset + 2] = 0x40;
                }

                if (changed)
                {
                    pixels[0] = 0x30;
                }
            }

            return new RegionCaptureResult(
                x,
                y,
                width,
                height,
                pixels,
                RegionCaptureMethod.BitBlt,
                changed ? 2UL : 1UL,
                DateTimeOffset.Now);
        }
    }

    private sealed class NoopSleeper : ISleeper
    {
        public void Sleep(int milliseconds, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
        }
    }
}
