using NUnit.Framework;

public sealed class LevelRewardCalculatorTests
{
    [SetUp]
    public void SetUp()
    {
        SessionLevelRewardHistory.Clear();
    }

    [TearDown]
    public void TearDown()
    {
        SessionLevelRewardHistory.Clear();
    }

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

    [Test]
    public void FailedFirstAttemptDoesNotConsumeFirstCompletion()
    {
        LevelRewardResult failed =
            SessionLevelRewardHistory.ResolveAttempt(
                "level-a",
                LevelOutcome.Failure,
                deliveredResources: 30,
                totalOreCapacity: 100);

        LevelRewardResult succeeded =
            SessionLevelRewardHistory.ResolveAttempt(
                "level-a",
                LevelOutcome.Success,
                deliveredResources: 50,
                totalOreCapacity: 100);

        Assert.That(failed.IsFirstCompletion, Is.False);
        Assert.That(failed.NewlyEarnedResources, Is.Zero);
        Assert.That(succeeded.IsFirstCompletion, Is.True);
        Assert.That(succeeded.NewlyEarnedResources, Is.EqualTo(50));
    }

    [Test]
    public void SessionHistoryTracksEachLevelIndependently()
    {
        LevelRewardResult first =
            SessionLevelRewardHistory.ResolveAttempt(
                "level-a",
                LevelOutcome.Success,
                deliveredResources: 50,
                totalOreCapacity: 100);

        LevelRewardResult improved =
            SessionLevelRewardHistory.ResolveAttempt(
                "level-a",
                LevelOutcome.Success,
                deliveredResources: 80,
                totalOreCapacity: 100);

        LevelRewardResult otherLevel =
            SessionLevelRewardHistory.ResolveAttempt(
                "level-b",
                LevelOutcome.Success,
                deliveredResources: 20,
                totalOreCapacity: 60);

        Assert.That(first.NewlyEarnedResources, Is.EqualTo(50));
        Assert.That(improved.PreviousBestDelivered, Is.EqualTo(50));
        Assert.That(improved.NewlyEarnedResources, Is.EqualTo(30));
        Assert.That(otherLevel.IsFirstCompletion, Is.True);
        Assert.That(otherLevel.NewlyEarnedResources, Is.EqualTo(20));
    }
}
