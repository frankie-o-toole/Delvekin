using NUnit.Framework;

public class RescueGoalProgressTests
{
    [Test]
    public void GoalRemainsPossibleWhenMaximumMatchesRequirement()
    {
        RescueGoalProgress progress = new(2, 3, 1, 6);

        Assert.That(progress.MaximumPossibleRescues, Is.EqualTo(6));
        Assert.That(progress.IsImpossible, Is.False);
    }

    [Test]
    public void GoalBecomesImpossibleBelowRequirement()
    {
        RescueGoalProgress progress = new(2, 2, 1, 6);

        Assert.That(progress.MaximumPossibleRescues, Is.EqualTo(5));
        Assert.That(progress.IsImpossible, Is.True);
    }

    [Test]
    public void ReachedGoalNeverBecomesImpossible()
    {
        RescueGoalProgress progress = new(6, 0, 0, 6);

        Assert.That(progress.TargetReached, Is.True);
        Assert.That(progress.IsImpossible, Is.False);
    }

    [Test]
    public void InvalidCountsAreClampedToZero()
    {
        RescueGoalProgress progress = new(-1, -2, -3, 1);

        Assert.That(progress.MaximumPossibleRescues, Is.Zero);
        Assert.That(progress.IsImpossible, Is.True);
    }
}
