using NUnit.Framework;

public class ResourceGoalProgressTests
{
    [Test]
    public void GoalRemainsPossibleWhenMaximumMatchesRequirement()
    {
        ResourceGoalProgress progress = new(2, 3, 1, 6);

        Assert.That(
            progress.MaximumPossibleMinedResources,
            Is.EqualTo(6));
        Assert.That(progress.IsImpossible, Is.False);
    }

    [Test]
    public void GoalBecomesImpossibleBelowRequirement()
    {
        ResourceGoalProgress progress = new(2, 2, 1, 6);

        Assert.That(
            progress.MaximumPossibleMinedResources,
            Is.EqualTo(5));
        Assert.That(progress.IsImpossible, Is.True);
    }

    [Test]
    public void ReachedGoalNeverBecomesImpossible()
    {
        ResourceGoalProgress progress = new(6, 0, 0, 6);

        Assert.That(progress.TargetReached, Is.True);
        Assert.That(progress.IsImpossible, Is.False);
    }

    [Test]
    public void TargetIsNotReachedBelowRequirement()
    {
        ResourceGoalProgress progress = new(5, 3, 2, 6);

        Assert.That(progress.TargetReached, Is.False);
    }

    [Test]
    public void InvalidCountsAreClampedToZero()
    {
        ResourceGoalProgress progress = new(-1, -2, -3, 1);

        Assert.That(
            progress.MaximumPossibleMinedResources,
            Is.Zero);
        Assert.That(progress.IsImpossible, Is.True);
    }
}
