using NUnit.Framework;

public class ResourceGoalProgressTests
{
    [Test]
    public void GoalRemainsPossibleWhenMaximumMatchesRequirement()
    {
        ResourceGoalProgress progress = new(
            3,
            1,
            6,
            new OreRuntimeProgress(10, 2, 1, 7));

        Assert.That(
            progress.MaximumPossibleMinedResources,
            Is.EqualTo(6));
        Assert.That(progress.IsImpossible, Is.False);
    }

    [Test]
    public void GoalBecomesImpossibleBelowRequirement()
    {
        ResourceGoalProgress progress = new(
            2,
            1,
            6,
            new OreRuntimeProgress(10, 2, 0, 8));

        Assert.That(
            progress.MaximumPossibleMinedResources,
            Is.EqualTo(5));
        Assert.That(progress.IsImpossible, Is.True);
    }

    [Test]
    public void GoalBecomesImpossibleWhenOreIsExhausted()
    {
        ResourceGoalProgress progress = new(
            8,
            4,
            6,
            new OreRuntimeProgress(5, 5, 0, 0));

        Assert.That(
            progress.MaximumPossibleMinedResources,
            Is.EqualTo(5));
        Assert.That(progress.IsImpossible, Is.True);
    }

    [Test]
    public void ReachedGoalNeverBecomesImpossible()
    {
        ResourceGoalProgress progress = new(
            0,
            0,
            6,
            new OreRuntimeProgress(6, 6, 0, 0));

        Assert.That(progress.TargetReached, Is.True);
        Assert.That(progress.IsImpossible, Is.False);
    }

    [Test]
    public void TargetIsNotReachedBelowRequirement()
    {
        ResourceGoalProgress progress = new(
            3,
            2,
            6,
            new OreRuntimeProgress(10, 5, 0, 5));

        Assert.That(progress.TargetReached, Is.False);
    }

    [Test]
    public void InvalidCountsAreClampedToZero()
    {
        ResourceGoalProgress progress = new(
            -2,
            -3,
            1,
            new OreRuntimeProgress(-1, -1, -1, -1));

        Assert.That(
            progress.MaximumPossibleMinedResources,
            Is.Zero);
        Assert.That(progress.IsImpossible, Is.True);
    }
}
