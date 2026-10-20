using Hollowfall.Core.Input;
using Hollowfall.Core.Ui;

namespace Hollowfall.Tests;

public class UiScalingTests
{
    [Fact]
    public void TheBaseWindowIsUnscaled() => Assert.Equal(1.0, UiScaling.Factor(1280, 720, 1.0));

    [Theory]
    [InlineData(2560, 1440, 2.0)]
    [InlineData(1920, 1080, 1.5)]
    [InlineData(3840, 2160, 3.0)]
    [InlineData(1024, 576, 0.8)]
    public void TheUiGrowsAndShrinksWithTheWindow(double w, double h, double expected) => Assert.Equal(expected, UiScaling.Factor(w, h, 1.0));

    [Fact]
    public void ANarrowOrShortWindowScalesByItsTighterSide()
    {
        // Wide but short: the height decides, so nothing is cut off at the bottom.
        Assert.Equal(1.0, UiScaling.Factor(3000, 720, 1.0));
        // Tall but narrow: the width decides.
        Assert.Equal(1.0, UiScaling.Factor(1280, 3000, 1.0));
    }

    [Fact]
    public void TinyAndHugeWindowsStayInsideTheAutomaticLimits()
    {
        Assert.Equal(0.75, UiScaling.Factor(200, 100, 1.0));
        Assert.Equal(3.0, UiScaling.Factor(20000, 10000, 1.0));
    }

    [Fact]
    public void ThePlayersMultiplierIsAppliedOnTopAndClamped()
    {
        Assert.Equal(1.5, UiScaling.Factor(1280, 720, 1.5));
        Assert.Equal(2.4, UiScaling.Factor(1920, 1080, 2.0), 5); // the multiplier is clamped to 1.6 first: 1.5 x 1.6
    }

    [Fact]
    public void AutomaticSizingCanBeSwitchedOff()
    {
        Assert.Equal(1.0, UiScaling.Factor(3840, 2160, 1.0, automatic: false));
        Assert.Equal(1.2, UiScaling.Factor(3840, 2160, 1.2, automatic: false));
    }

    [Fact]
    public void ScalesChangeInWholeStepsSoDraggingDoesNotShimmer()
    {
        double previous = UiScaling.Factor(1280, 720, 1.0);
        int changes = 0;
        for (int w = 1280; w <= 2560; w++)
        {
            double f = UiScaling.Factor(w, w * 9.0 / 16.0, 1.0);
            Assert.Equal(0, Math.Round(f / UiScaling.Step) - f / UiScaling.Step, 6);
            if (f != previous) changes++;
            previous = f;
        }
        Assert.InRange(changes, 15, 25); // 1.0 to 2.0 in steps of 0.05
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(-5, 100)]
    [InlineData(double.NaN, 720)]
    [InlineData(double.PositiveInfinity, double.PositiveInfinity)]
    public void BadWindowSizesGiveANeutralScaleNotAnException(double w, double h) => Assert.Equal(1.0, UiScaling.Factor(w, h, 1.0));

    [Fact]
    public void ABadUserScaleFallsBackToTheDefault()
    {
        Assert.Equal(1.0, UiScaling.Factor(1280, 720, double.NaN));
        Assert.Equal(1.0, UiScaling.Factor(1280, 720, double.PositiveInfinity));
    }

    [Fact]
    public void TheSettingIsClampedSavedAndLoaded()
    {
        var s = new GameSettings { UiScale = 9, AutoUiScale = false };
        Assert.Equal(UiScaling.UserRange.Max, s.UiScale);
        var back = GameSettings.FromJson(s.ToJson());
        Assert.Equal(UiScaling.UserRange.Max, back.UiScale);
        Assert.False(back.AutoUiScale);
        var fresh = GameSettings.FromJson("{}");
        Assert.Equal(UiScaling.UserRange.Default, fresh.UiScale);
        Assert.True(fresh.AutoUiScale);
        Assert.Equal(UiScaling.UserRange.Min, new GameSettings { UiScale = -1 }.UiScale);
    }
}
