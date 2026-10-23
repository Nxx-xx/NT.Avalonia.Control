using NT.AvaloniaControl.VirtualKeyboard;

namespace VirtualKeyboard.Core.Tests;

public sealed class RepeatControllerTests
{
    [Fact]
    public void RepeatsRespectDelayAndIntervalWithoutReplayingMissedTicks()
    {
        var repeat = new RepeatController();
        repeat.Begin(1, TimeSpan.Zero, TimeSpan.FromMilliseconds(500), TimeSpan.FromMilliseconds(70));
        Assert.False(repeat.TryTick(1, TimeSpan.FromMilliseconds(499)));
        Assert.True(repeat.TryTick(1, TimeSpan.FromMilliseconds(500)));
        Assert.False(repeat.TryTick(1, TimeSpan.FromMilliseconds(569)));
        Assert.True(repeat.TryTick(1, TimeSpan.FromMilliseconds(570)));
        Assert.True(repeat.TryTick(1, TimeSpan.FromSeconds(2)));
        Assert.False(repeat.TryTick(1, TimeSpan.FromSeconds(2)));
    }

    [Fact]
    public void CancellationAndSessionIdentityPreventOldTimersFromActing()
    {
        var repeat = new RepeatController();
        repeat.Begin(1, TimeSpan.Zero, TimeSpan.FromMilliseconds(500), TimeSpan.FromMilliseconds(70));
        Assert.False(repeat.TryTick(2, TimeSpan.FromSeconds(1)));
        repeat.Cancel();
        Assert.False(repeat.IsActive);
        Assert.False(repeat.TryTick(1, TimeSpan.FromSeconds(1)));
        repeat.Begin(2, TimeSpan.FromSeconds(1), TimeSpan.FromMilliseconds(500), TimeSpan.FromMilliseconds(70));
        Assert.False(repeat.TryTick(1, TimeSpan.FromSeconds(2)));
        Assert.True(repeat.TryTick(2, TimeSpan.FromSeconds(2)));
    }

    [Fact]
    public void ZeroDelayDoesNotDuplicateTheInitialPressAction()
    {
        var repeat = new RepeatController();
        repeat.Begin(1, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.FromMilliseconds(70));
        Assert.False(repeat.TryTick(1, TimeSpan.Zero));
        Assert.False(repeat.TryTick(1, TimeSpan.FromMilliseconds(69)));
        Assert.True(repeat.TryTick(1, TimeSpan.FromMilliseconds(70)));
    }

    [Theory]
    [InlineData(500, 499.1)]
    [InlineData(500, 499.9)]
    [InlineData(10000, 9999.99)]
    public void AnEarlyFirstTickSchedulesOnlyTheRemainingDeadline(int delayMilliseconds, double earlyTickMilliseconds)
    {
        var repeat = new RepeatController();
        repeat.Begin(1, TimeSpan.Zero, TimeSpan.FromMilliseconds(delayMilliseconds), TimeSpan.FromMilliseconds(70));
        var earlyTick = TimeSpan.FromMilliseconds(earlyTickMilliseconds);
        Assert.False(repeat.TryTick(1, earlyTick));
        var nextDelay = Assert.IsType<TimeSpan>(repeat.GetNextTickDelay(1, earlyTick));
        Assert.Equal(TimeSpan.FromMilliseconds(1), nextDelay);
        // This models a dispatcher retry after its newly scheduled interval,
        // rather than incorrectly waiting another full initial RepeatDelay.
        Assert.True(repeat.TryTick(1, earlyTick + nextDelay));
        Assert.Equal(TimeSpan.FromMilliseconds(70), repeat.GetNextTickDelay(1, earlyTick + nextDelay));
        Assert.False(repeat.TryTick(1, earlyTick + nextDelay));
    }

    [Fact]
    public void TimerDelayRoundsUpToMillisecondsAndNeverRevivesACancelledSession()
    {
        var repeat = new RepeatController();
        repeat.Begin(4, TimeSpan.Zero, TimeSpan.FromMilliseconds(500), TimeSpan.FromMilliseconds(70));
        Assert.Equal(TimeSpan.FromMilliseconds(11), repeat.GetNextTickDelay(4, TimeSpan.FromMilliseconds(489.2)));
        Assert.Equal(TimeSpan.FromMilliseconds(1), repeat.GetNextTickDelay(4, TimeSpan.FromMilliseconds(500)));
        Assert.Null(repeat.GetNextTickDelay(3, TimeSpan.FromMilliseconds(499.9)));
        repeat.Cancel();
        Assert.Null(repeat.GetNextTickDelay(4, TimeSpan.FromMilliseconds(499.9)));
        repeat.Begin(5, TimeSpan.FromSeconds(1), TimeSpan.FromMilliseconds(500), TimeSpan.FromMilliseconds(70));
        Assert.Null(repeat.GetNextTickDelay(4, TimeSpan.FromSeconds(2)));
        Assert.Equal(TimeSpan.FromMilliseconds(500), repeat.GetNextTickDelay(5, TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public void AZeroInitialDelayAndLaterRepeatHaveTheSameEarlyTickRetryRule()
    {
        var repeat = new RepeatController();
        repeat.Begin(1, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.FromMilliseconds(70));
        var early = TimeSpan.FromMilliseconds(69.5);
        Assert.False(repeat.TryTick(1, early));
        Assert.Equal(TimeSpan.FromMilliseconds(1), repeat.GetNextTickDelay(1, early));
        Assert.True(repeat.TryTick(1, TimeSpan.FromMilliseconds(70.5)));
        Assert.False(repeat.TryTick(1, TimeSpan.FromMilliseconds(140)));
        Assert.Equal(TimeSpan.FromMilliseconds(1), repeat.GetNextTickDelay(1, TimeSpan.FromMilliseconds(140)));
        Assert.True(repeat.TryTick(1, TimeSpan.FromMilliseconds(141)));
    }

    [Theory]
    [InlineData(-1, 70)]
    [InlineData(10001, 70)]
    [InlineData(500, 0)]
    [InlineData(500, 9)]
    [InlineData(500, 10001)]
    public void InvalidSchedulingValuesAreRejected(int delay, int interval) => Assert.Throws<ArgumentOutOfRangeException>(() =>
        new RepeatController().Begin(1, TimeSpan.Zero, TimeSpan.FromMilliseconds(delay), TimeSpan.FromMilliseconds(interval)));
}
