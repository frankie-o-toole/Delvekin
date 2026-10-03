using NUnit.Framework;

public class LevelSimulationSpeedTests
{
    [TestCase(LevelSimulationSpeed.Normal, 1f)]
    [TestCase(LevelSimulationSpeed.Fast, 2f)]
    [TestCase(LevelSimulationSpeed.VeryFast, 4f)]
    public void SupportedSpeedMapsToExpectedTimeScale(
        LevelSimulationSpeed speed,
        float expected)
    {
        Assert.That(
            LevelSimulationSpeedUtility.ToTimeScale(speed),
            Is.EqualTo(expected));
    }

    [Test]
    public void UnsupportedSpeedFallsBackToNormal()
    {
        LevelSimulationSpeed unsupported =
            (LevelSimulationSpeed)3;

        Assert.That(
            LevelSimulationSpeedUtility.IsSupported(unsupported),
            Is.False);

        Assert.That(
            LevelSimulationSpeedUtility.ToTimeScale(unsupported),
            Is.EqualTo(1f));
    }
}
