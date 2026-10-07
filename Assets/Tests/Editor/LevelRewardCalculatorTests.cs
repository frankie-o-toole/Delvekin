using NUnit.Framework;

public sealed class LevelRewardCalculatorTests
{
    [Test]
    public void FirstCompletionEarnsEveryDeliveredResource()
    {
        LevelRewardResult result = LevelRewardCalculator.Calculate(
            "level-a",
            LevelOutcome.Success,
            deliveredResources: 50,
            totalOreCapacity: 100,
            hasPreviousCompletion: false,
            previousBestDelivered: 0);

        Assert.That(result.IsFirstCompletion, Is.True);
        Assert.That(result.PreviousBestDelivered, Is.Zero);
        Assert.That(result.BestDelivered, Is.EqualTo(50));
        Assert.That(result.NewlyEarnedResources, Is.EqualTo(50));
    }

    [Test]
    public void WorseReplayEarnsNothingAndKeepsBest()
    {
        LevelRewardResult result = LevelRewardCalculator.Calculate(
            "level-a",
            LevelOutcome.Success,
            deliveredResources: 40,
            totalOreCapacity: 100,
            hasPreviousCompletion: true,
            previousBestDelivered: 50);

        Assert.That(result.IsFirstCompletion, Is.False);
        Assert.That(result.PreviousBestDelivered, Is.EqualTo(50));
        Assert.That(result.BestDelivered, Is.EqualTo(50));
        Assert.That(result.NewlyEarnedResources, Is.Zero);
    }

    [Test]
    public void ImprovedReplayEarnsOnlyHighWaterDifference()
    {
        LevelRewardResult result = LevelRewardCalculator.Calculate(
            "level-a",
            LevelOutcome.Success,
            deliveredResources: 80,
            totalOreCapacity: 100,
            hasPreviousCompletion: true,
            previousBestDelivered: 50);

        Assert.That(result.PreviousBestDelivered, Is.EqualTo(50));
        Assert.That(result.BestDelivered, Is.EqualTo(80));
        Assert.That(result.NewlyEarnedResources, Is.EqualTo(30));
    }

    [Test]
    public void FailedAttemptNeverEarnsOrChangesBest()
    {
        LevelRewardResult result = LevelRewardCalculator.Calculate(
            "level-a",
            LevelOutcome.Failure,
            deliveredResources: 80,
            totalOreCapacity: 100,
            hasPreviousCompletion: true,
            previousBestDelivered: 50);

        Assert.That(result.AttemptSucceeded, Is.False);
        Assert.That(result.BestDelivered, Is.EqualTo(50));
        Assert.That(result.NewlyEarnedResources, Is.Zero);
    }

}
