using NUnit.Framework;
using UnityEngine;

public sealed class CampaignProgressServiceTests
{
    private GameObject serviceObject;
    private CampaignProgressService service;

    [SetUp]
    public void SetUp()
    {
        serviceObject = new GameObject("Campaign Service Test");
        service = serviceObject.AddComponent<CampaignProgressService>();
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(serviceObject);
    }

    [Test]
    public void LoadedDataIsCopiedBeforeUse()
    {
        CampaignSaveData source = CampaignSaveData.CreateNew(
            new[] { DwarfJobType.DirectionAlter });

        service.LoadFromData(source);
        source.unlockedJobIds.Clear();

        Assert.That(
            service.IsJobUnlocked(DwarfJobType.DirectionAlter),
            Is.True);
    }

    [Test]
    public void UnlockJobIsIdempotentAndRejectsNone()
    {
        service.LoadFromData(CampaignSaveData.CreateNew());

        Assert.That(
            service.UnlockJob(DwarfJobType.Tunneller),
            Is.True);
        Assert.That(
            service.UnlockJob(DwarfJobType.Tunneller),
            Is.False);
        Assert.That(
            service.UnlockJob(DwarfJobType.None),
            Is.False);
        Assert.That(
            service.IsJobUnlocked(DwarfJobType.Tunneller),
            Is.True);
    }

    [Test]
    public void SnapshotCannotMutateServiceState()
    {
        service.LoadFromData(CampaignSaveData.CreateNew(
            new[] { DwarfJobType.Digger }));

        CampaignSaveData snapshot = service.CreateSnapshot();
        snapshot.unlockedJobIds.Clear();

        Assert.That(
            service.IsJobUnlocked(DwarfJobType.Digger),
            Is.True);
    }

    [Test]
    public void NewerSchemaIsRejected()
    {
        CampaignSaveData future = CampaignSaveData.CreateNew();
        future.schemaVersion = CampaignSaveData.CurrentSchemaVersion + 1;

        Assert.Throws<System.InvalidOperationException>(
            () => service.LoadFromData(future));
    }

    [Test]
    public void FirstSuccessfulAttemptCreatesPermanentProgress()
    {
        service.LoadFromData(CampaignSaveData.CreateNew());

        bool changed = service.RecordLevelAttempt(
            "level-a",
            CreateAttempt(
                LevelOutcome.Success,
                delivered: 40,
                capacity: 60,
                simulationSeconds: 90d));

        CampaignLevelProgressData progress =
            service.GetLevelProgress("level-a");

        Assert.That(changed, Is.True);
        Assert.That(progress.completed, Is.True);
        Assert.That(progress.bestDeliveredResources, Is.EqualTo(40));
        Assert.That(progress.perfectResourceRun, Is.False);
        Assert.That(progress.bestSimulationSeconds, Is.EqualTo(90d));
    }

    [Test]
    public void WorseReplayCannotLowerStoredHighWaterMarks()
    {
        service.LoadFromData(CampaignSaveData.CreateNew());
        service.RecordLevelAttempt(
            "level-a",
            CreateAttempt(LevelOutcome.Success, 50, 60, 80d));

        bool changed = service.RecordLevelAttempt(
            "level-a",
            CreateAttempt(LevelOutcome.Success, 30, 60, 120d));

        CampaignLevelProgressData progress =
            service.GetLevelProgress("level-a");

        Assert.That(changed, Is.False);
        Assert.That(progress.bestDeliveredResources, Is.EqualTo(50));
        Assert.That(progress.bestSimulationSeconds, Is.EqualTo(80d));
    }

    [Test]
    public void ReplayCanImproveResourcesPerfectRunAndTimeIndependently()
    {
        service.LoadFromData(CampaignSaveData.CreateNew());
        service.RecordLevelAttempt(
            "level-a",
            CreateAttempt(LevelOutcome.Success, 40, 60, 100d));
        service.RecordLevelAttempt(
            "level-a",
            CreateAttempt(LevelOutcome.Success, 60, 60, 130d));
        service.RecordLevelAttempt(
            "level-a",
            CreateAttempt(LevelOutcome.Success, 50, 60, 70d));

        CampaignLevelProgressData progress =
            service.GetLevelProgress("level-a");

        Assert.That(progress.bestDeliveredResources, Is.EqualTo(60));
        Assert.That(progress.perfectResourceRun, Is.True);
        Assert.That(progress.bestSimulationSeconds, Is.EqualTo(70d));
    }

    [Test]
    public void FailedAttemptDoesNotCreateOrChangeProgress()
    {
        service.LoadFromData(CampaignSaveData.CreateNew());

        bool changed = service.RecordLevelAttempt(
            "level-a",
            CreateAttempt(LevelOutcome.Failure, 40, 60, 90d));

        Assert.That(changed, Is.False);
        Assert.That(service.GetLevelProgress("level-a"), Is.Null);
    }

    [Test]
    public void ProgressIsIsolatedByStableLevelIdAndSnapshots()
    {
        service.LoadFromData(CampaignSaveData.CreateNew());
        service.RecordLevelAttempt(
            "level-a",
            CreateAttempt(LevelOutcome.Success, 40, 60, 90d));
        service.RecordLevelAttempt(
            "level-b",
            CreateAttempt(LevelOutcome.Success, 20, 30, 50d));

        CampaignLevelProgressData levelA =
            service.GetLevelProgress("level-a");
        CampaignLevelProgressData levelB =
            service.GetLevelProgress("level-b");
        levelA.bestDeliveredResources = 999;

        Assert.That(levelB.bestDeliveredResources, Is.EqualTo(20));
        Assert.That(
            service.GetLevelProgress("level-a").bestDeliveredResources,
            Is.EqualTo(40));
    }

    [Test]
    public void ProgressSurvivesCampaignSnapshotReload()
    {
        service.LoadFromData(CampaignSaveData.CreateNew());
        service.RecordLevelAttempt(
            "level-a",
            CreateAttempt(LevelOutcome.Success, 60, 60, 75d));

        CampaignSaveData saved = service.CreateSnapshot();
        service.InitializeNewCampaign();
        service.LoadFromData(saved);

        CampaignLevelProgressData progress =
            service.GetLevelProgress("level-a");

        Assert.That(progress.completed, Is.True);
        Assert.That(progress.bestDeliveredResources, Is.EqualTo(60));
        Assert.That(progress.perfectResourceRun, Is.True);
        Assert.That(progress.bestSimulationSeconds, Is.EqualTo(75d));
    }

    [Test]
    public void PersistentRewardPaysOnlyImprovementOverStoredBest()
    {
        service.LoadFromData(CampaignSaveData.CreateNew());

        LevelRewardResult first = service.ResolveLevelReward(
            "level-a",
            LevelOutcome.Success,
            deliveredResources: 50,
            totalOreCapacity: 100);
        service.RecordLevelAttempt(
            "level-a",
            CreateAttempt(LevelOutcome.Success, 50, 100, 90d));

        LevelRewardResult improved = service.ResolveLevelReward(
            "level-a",
            LevelOutcome.Success,
            deliveredResources: 80,
            totalOreCapacity: 100);
        service.RecordLevelAttempt(
            "level-a",
            CreateAttempt(LevelOutcome.Success, 80, 100, 85d));

        LevelRewardResult worse = service.ResolveLevelReward(
            "level-a",
            LevelOutcome.Success,
            deliveredResources: 60,
            totalOreCapacity: 100);

        Assert.That(first.IsFirstCompletion, Is.True);
        Assert.That(first.NewlyEarnedResources, Is.EqualTo(50));
        Assert.That(improved.PreviousBestDelivered, Is.EqualTo(50));
        Assert.That(improved.NewlyEarnedResources, Is.EqualTo(30));
        Assert.That(worse.PreviousBestDelivered, Is.EqualTo(80));
        Assert.That(worse.NewlyEarnedResources, Is.Zero);
    }

    [Test]
    public void RewardHighWaterSurvivesSnapshotReload()
    {
        service.LoadFromData(CampaignSaveData.CreateNew());
        service.RecordLevelAttempt(
            "level-a",
            CreateAttempt(LevelOutcome.Success, 70, 100, 90d));

        CampaignSaveData saved = service.CreateSnapshot();
        service.InitializeNewCampaign();
        service.LoadFromData(saved);

        LevelRewardResult replay = service.ResolveLevelReward(
            "level-a",
            LevelOutcome.Success,
            deliveredResources: 90,
            totalOreCapacity: 100);

        Assert.That(replay.IsFirstCompletion, Is.False);
        Assert.That(replay.PreviousBestDelivered, Is.EqualTo(70));
        Assert.That(replay.NewlyEarnedResources, Is.EqualTo(20));
    }

    [Test]
    public void FailureDoesNotConsumePersistentReward()
    {
        service.LoadFromData(CampaignSaveData.CreateNew());

        LevelRewardResult failed = service.ResolveLevelReward(
            "level-a",
            LevelOutcome.Failure,
            deliveredResources: 40,
            totalOreCapacity: 100);
        service.RecordLevelAttempt(
            "level-a",
            CreateAttempt(LevelOutcome.Failure, 40, 100, 60d));

        LevelRewardResult success = service.ResolveLevelReward(
            "level-a",
            LevelOutcome.Success,
            deliveredResources: 50,
            totalOreCapacity: 100);

        Assert.That(failed.NewlyEarnedResources, Is.Zero);
        Assert.That(success.IsFirstCompletion, Is.True);
        Assert.That(success.NewlyEarnedResources, Is.EqualTo(50));
    }

    [Test]
    public void BlankLevelIdIsRejected()
    {
        service.LoadFromData(CampaignSaveData.CreateNew());

        Assert.Throws<System.ArgumentException>(() =>
            service.RecordLevelAttempt(
                " ",
                CreateAttempt(LevelOutcome.Success, 10, 10, 20d)));
    }

    private static LevelAttemptResult CreateAttempt(
        LevelOutcome outcome,
        int delivered,
        int capacity,
        double simulationSeconds)
    {
        return new LevelAttemptResult(
            outcome,
            totalDwarves: 10,
            totalOreCapacity: capacity,
            requiredMinedResources: 1,
            spawned: 10,
            deliveredResources: delivered,
            died: 0,
            recalled: 0,
            activeAtEnd: 0,
            unspawnedAtEnd: 0,
            simulationSeconds: simulationSeconds);
    }
}
