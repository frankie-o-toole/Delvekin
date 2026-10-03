using NUnit.Framework;

public class LevelAttemptResultTests
{
    [Test]
    public void RescuePercentageUsesAuthoredTotal()
    {
        LevelAttemptResult result = new(
            LevelOutcome.Success,
            20,
            10,
            15,
            10,
            2,
            0,
            3,
            5,
            90d);

        Assert.That(result.RescuePercentage, Is.EqualTo(50d));
    }

    [Test]
    public void LeftBehindIncludesActiveAndUnspawnedDwarves()
    {
        LevelAttemptResult result = new(
            LevelOutcome.Success,
            20,
            10,
            15,
            10,
            2,
            0,
            3,
            5,
            90d);

        Assert.That(result.LeftBehind, Is.EqualTo(8));
    }

    [Test]
    public void NegativeRuntimeValuesAreClamped()
    {
        LevelAttemptResult result = new(
            LevelOutcome.Failure,
            -1,
            -1,
            -1,
            -1,
            -1,
            -1,
            -1,
            -1,
            -1d);

        Assert.That(result.TotalDwarves, Is.EqualTo(1));
        Assert.That(result.Rescued, Is.Zero);
        Assert.That(result.LeftBehind, Is.Zero);
        Assert.That(result.SimulationSeconds, Is.Zero);
    }
}
