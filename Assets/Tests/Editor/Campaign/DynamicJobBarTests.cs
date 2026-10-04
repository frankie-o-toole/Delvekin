using NUnit.Framework;

public sealed class DynamicJobBarTests
{
    [Test]
    public void ButtonsShareConfiguredWidthBudget()
    {
        float width = DwarfJobBarUI.CalculateJobButtonWidth(
            jobCount: 5,
            widthBudget: 900f,
            minimumWidth: 120f,
            maximumWidth: 225f);

        Assert.That(width, Is.EqualTo(180f));
    }

    [Test]
    public void SingleButtonDoesNotGrowBeyondMaximum()
    {
        float width = DwarfJobBarUI.CalculateJobButtonWidth(
            jobCount: 1,
            widthBudget: 900f,
            minimumWidth: 120f,
            maximumWidth: 225f);

        Assert.That(width, Is.EqualTo(225f));
    }

    [Test]
    public void CrowdedBarDoesNotShrinkBelowMinimum()
    {
        float width = DwarfJobBarUI.CalculateJobButtonWidth(
            jobCount: 20,
            widthBudget: 900f,
            minimumWidth: 120f,
            maximumWidth: 225f);

        Assert.That(width, Is.EqualTo(120f));
    }
}
