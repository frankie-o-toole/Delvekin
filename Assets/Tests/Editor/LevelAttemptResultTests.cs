using NUnit.Framework;

public class LevelAttemptResultTests
{
    [Test]
    public void DeliveredPercentageUsesAuthoredOreCapacity()
    {
        LevelAttemptResult result = new(
            LevelOutcome.Success,
            20,
            20,
            10,
            15,
            10,
            2,
            0,
            3,
            5,
            90d);

        Assert.That(result.DeliveredResourcePercentage, Is.EqualTo(50d));
        Assert.That(result.RemainingResources, Is.EqualTo(10));
        Assert.That(result.IsPerfectResourceRun, Is.False);
    }

    [Test]
    public void DeliveringAllOreIsPerfect()
    {
        LevelAttemptResult result = new(
            LevelOutcome.Success,
            20,
            20,
            10,
            20,
            20,
            0,
            0,
            0,
            0,
            45d);

        Assert.That(result.DeliveredResourcePercentage, Is.EqualTo(100d));
        Assert.That(result.RemainingResources, Is.Zero);
        Assert.That(result.IsPerfectResourceRun, Is.True);
    }

    [Test]
    public void LeftBehindIncludesActiveAndUnspawnedDwarves()
    {
        LevelAttemptResult result = new(
            LevelOutcome.Success,
            20,
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
            -1,
            -1d);

        Assert.That(result.TotalDwarves, Is.EqualTo(1));
        Assert.That(result.TotalOreCapacity, Is.Zero);
        Assert.That(result.DeliveredResources, Is.Zero);
        Assert.That(result.RemainingResources, Is.Zero);
        Assert.That(result.IsPerfectResourceRun, Is.False);
        Assert.That(result.LeftBehind, Is.Zero);
        Assert.That(result.SimulationSeconds, Is.Zero);
    }
}
