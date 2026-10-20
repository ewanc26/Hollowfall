using Hollowfall.Core.Physics;

namespace Hollowfall.Tests;

public class StepSmoothingTests
{
    private const double Dt = 1.0 / 60;

    [Fact]
    public void ANoOffsetStaysZero()
    {
        Assert.Equal(0, StepSmoothing.Settle(0, Dt));
        Assert.Equal(0, StepSmoothing.Settle(0.3, Dt)); // a positive offset is meaningless: drop it
        Assert.Equal(0, StepSmoothing.Settle(double.NaN, Dt));
    }

    [Fact]
    public void TheEyeClimbsMonotonicallyAndNeverOvershoots()
    {
        double offset = -1.0, previous = offset;
        int ticks = 0;
        while (offset < 0 && ticks++ < 600)
        {
            offset = StepSmoothing.Settle(offset, Dt);
            Assert.True(offset >= previous, "the offset should only move toward zero");
            Assert.True(offset <= 0, "it must not overshoot");
            previous = offset;
        }
        Assert.Equal(0, offset);
        Assert.InRange(ticks, 6, 40); // a full block settles in a fraction of a second: smooth, not sluggish
    }

    [Fact]
    public void AFullBlockNeverJumpsMoreThanAFewHundredthsPerFrame()
    {
        double offset = -1.0;
        double biggest = 0;
        while (offset < 0)
        {
            double next = StepSmoothing.Settle(offset, Dt);
            biggest = Math.Max(biggest, next - offset);
            offset = next;
        }
        Assert.True(biggest < 0.2, $"the eye moved {biggest:F3} in one frame");
    }

    [Fact]
    public void ASmallStepSettlesQuicklyToo()
    {
        double offset = -0.1;
        int ticks = 0;
        while (offset < 0 && ticks++ < 100) offset = StepSmoothing.Settle(offset, Dt);
        Assert.InRange(ticks, 1, 15);
    }
}
