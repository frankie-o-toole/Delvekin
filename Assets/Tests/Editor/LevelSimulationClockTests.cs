using NUnit.Framework;

public class LevelSimulationClockTests
{
    [Test]
    public void RunningSimulationAccumulatesScaledTime()
    {
        LevelSimulationClock clock = new();

        clock.Advance(0.5f, LevelSimulationState.Running);
        clock.Advance(2f, LevelSimulationState.Running);

        Assert.That(clock.ElapsedSeconds, Is.EqualTo(2.5d));
    }

    [TestCase(LevelSimulationState.Preparation)]
    [TestCase(LevelSimulationState.Paused)]
    [TestCase(LevelSimulationState.Completed)]
    public void NonRunningStatesDoNotAccumulateTime(
        LevelSimulationState state)
    {
        LevelSimulationClock clock = new();

        clock.Advance(3f, state);

        Assert.That(clock.ElapsedSeconds, Is.Zero);
    }

    [Test]
    public void ScaledDeltaTimePreservesFutureFastForwardCost()
    {
        LevelSimulationClock clock = new();

        clock.Advance(4f, LevelSimulationState.Running);

        Assert.That(clock.ElapsedSeconds, Is.EqualTo(4d));
    }

    [Test]
    public void ResetClearsElapsedTime()
    {
        LevelSimulationClock clock = new();
        clock.Advance(10f, LevelSimulationState.Running);

        clock.Reset();

        Assert.That(clock.ElapsedSeconds, Is.Zero);
    }

    [TestCase(float.NaN)]
    [TestCase(float.PositiveInfinity)]
    [TestCase(-1f)]
    [TestCase(0f)]
    public void InvalidDeltaTimeIsIgnored(float deltaTime)
    {
        LevelSimulationClock clock = new();

        clock.Advance(deltaTime, LevelSimulationState.Running);

        Assert.That(clock.ElapsedSeconds, Is.Zero);
    }
}
