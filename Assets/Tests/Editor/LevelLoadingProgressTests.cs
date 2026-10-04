using NUnit.Framework;

public sealed class LevelLoadingProgressTests
{
    [Test]
    public void LoadingStateReportsWorkAndClampsProgress()
    {
        LevelLoadingProgress progress = new(
            LevelLoadingState.BuildingChunks,
            "Building chunks",
            1.5f,
            completedWork: 8,
            totalWork: 20);

        Assert.That(progress.IsLoading, Is.True);
        Assert.That(progress.IsReady, Is.False);
        Assert.That(progress.HasDeterminateProgress, Is.True);
        Assert.That(progress.Progress, Is.EqualTo(1f));
        Assert.That(progress.CompletedWork, Is.EqualTo(8));
        Assert.That(progress.TotalWork, Is.EqualTo(20));
    }

    [Test]
    public void ReadyStateIsNotLoading()
    {
        LevelLoadingProgress progress = new(
            LevelLoadingState.Ready,
            "Ready",
            1f,
            completedWork: 1,
            totalWork: 1);

        Assert.That(progress.IsReady, Is.True);
        Assert.That(progress.IsLoading, Is.False);
    }

    [Test]
    public void UnknownWorkAmountIsIndeterminate()
    {
        LevelLoadingProgress progress = new(
            LevelLoadingState.Preparing,
            "Preparing level",
            0f);

        Assert.That(progress.HasDeterminateProgress, Is.False);
    }
}
