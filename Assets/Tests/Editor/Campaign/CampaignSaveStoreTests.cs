using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

public sealed class CampaignSaveStoreTests
{
    private string testDirectory;
    private string savePath;

    [SetUp]
    public void SetUp()
    {
        testDirectory = Path.Combine(
            Path.GetTempPath(),
            "DelvekinCampaignTests",
            Guid.NewGuid().ToString("N"));
        savePath = Path.Combine(testDirectory, "campaign.json");
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(testDirectory))
        {
            Directory.Delete(testDirectory, true);
        }
    }

    [Test]
    public void MissingSaveCreatesAndPersistsNewCampaign()
    {
        CampaignSaveLoadResult result =
            CampaignSaveStore.LoadOrCreate(
                savePath,
                () => CampaignSaveData.CreateNew(
                    startingOre: 12));

        Assert.That(result.Source,
            Is.EqualTo(CampaignLoadSource.CreatedNew));
        Assert.That(result.Data.AvailableOre, Is.EqualTo(12));
        Assert.That(File.Exists(savePath), Is.True);

        CampaignSaveLoadResult reloaded =
            CampaignSaveStore.LoadOrCreate(
                savePath,
                () => CampaignSaveData.CreateNew());

        Assert.That(reloaded.Source,
            Is.EqualTo(CampaignLoadSource.Primary));
        Assert.That(reloaded.Data.AvailableOre, Is.EqualTo(12));
        Assert.That(reloaded.Data.campaignId,
            Is.EqualTo(result.Data.campaignId));
    }

    [Test]
    public void SaveRoundTripPreservesWalletJobsAndLevelProgress()
    {
        CampaignSaveData source = CampaignSaveData.CreateNew(
            new[] { DwarfJobType.DirectionAlter },
            startingOre: 80);
        source.spentOre = 20;
        source.levelProgress.Add(new CampaignLevelProgressData
        {
            levelId = "level-a",
            completed = true,
            bestDeliveredResources = 60,
            perfectResourceRun = true,
            bestSimulationSeconds = 42.5d
        });

        CampaignSaveStore.Save(savePath, source);
        CampaignSaveLoadResult result =
            CampaignSaveStore.LoadOrCreate(
                savePath,
                () => CampaignSaveData.CreateNew());

        Assert.That(result.Data.earnedOre, Is.EqualTo(80));
        Assert.That(result.Data.spentOre, Is.EqualTo(20));
        Assert.That(result.Data.AvailableOre, Is.EqualTo(60));
        Assert.That(result.Data.unlockedJobIds,
            Does.Contain(DwarfJobIdUtility.DirectionAlter));
        Assert.That(result.Data.levelProgress, Has.Count.EqualTo(1));
        Assert.That(result.Data.levelProgress[0].perfectResourceRun,
            Is.True);
        Assert.That(result.Data.levelProgress[0].bestSimulationSeconds,
            Is.EqualTo(42.5d));
    }

    [Test]
    public void CorruptPrimaryIsArchivedAndRecoveredFromBackup()
    {
        CampaignSaveData first = CampaignSaveData.CreateNew(
            startingOre: 25);
        CampaignSaveStore.Save(savePath, first);

        CampaignSaveData second = first.Clone();
        second.earnedOre = 40;
        CampaignSaveStore.Save(savePath, second);
        File.WriteAllText(savePath, "not valid json");

        CampaignSaveLoadResult result =
            CampaignSaveStore.LoadOrCreate(
                savePath,
                () => CampaignSaveData.CreateNew());

        Assert.That(result.Source,
            Is.EqualTo(CampaignLoadSource.Backup));
        Assert.That(result.Data.earnedOre, Is.EqualTo(25));
        Assert.That(result.ArchivedCorruptPath, Is.Not.Null);
        Assert.That(File.Exists(result.ArchivedCorruptPath), Is.True);
        Assert.That(File.Exists(savePath), Is.True);
    }

    [Test]
    public void CorruptSaveWithoutBackupCreatesFreshCampaign()
    {
        Directory.CreateDirectory(testDirectory);
        File.WriteAllText(savePath, string.Empty);

        CampaignSaveLoadResult result =
            CampaignSaveStore.LoadOrCreate(
                savePath,
                () => CampaignSaveData.CreateNew(
                    startingOre: 7));

        Assert.That(result.Source,
            Is.EqualTo(CampaignLoadSource.CreatedNew));
        Assert.That(result.Data.AvailableOre, Is.EqualTo(7));
        Assert.That(result.ArchivedCorruptPath, Is.Not.Null);
        Assert.That(File.Exists(result.ArchivedCorruptPath), Is.True);
    }

    [Test]
    public void SchemaZeroMigratesAndIsRewritten()
    {
        Directory.CreateDirectory(testDirectory);
        CampaignSaveData legacy = CampaignSaveData.CreateNew(
            startingOre: 15);
        legacy.schemaVersion = 0;
        File.WriteAllText(
            savePath,
            JsonUtility.ToJson(legacy, true));

        CampaignSaveLoadResult result =
            CampaignSaveStore.LoadOrCreate(
                savePath,
                () => CampaignSaveData.CreateNew());

        Assert.That(result.WasMigrated, Is.True);
        Assert.That(result.Data.schemaVersion,
            Is.EqualTo(CampaignSaveData.CurrentSchemaVersion));
        Assert.That(result.Data.AvailableOre, Is.EqualTo(15));

        CampaignSaveData rewritten = JsonUtility.FromJson<CampaignSaveData>(
            File.ReadAllText(savePath));
        Assert.That(rewritten.schemaVersion,
            Is.EqualTo(CampaignSaveData.CurrentSchemaVersion));
    }

    [Test]
    public void FutureSchemaIsRejectedWithoutChangingFile()
    {
        Directory.CreateDirectory(testDirectory);
        string futureJson =
            "{\"schemaVersion\":" +
            (CampaignSaveData.CurrentSchemaVersion + 1) +
            ",\"campaignId\":\"future\"}";
        File.WriteAllText(savePath, futureJson);

        Assert.Throws<CampaignSaveUnsupportedVersionException>(() =>
            CampaignSaveStore.LoadOrCreate(
                savePath,
                () => CampaignSaveData.CreateNew()));

        Assert.That(File.ReadAllText(savePath), Is.EqualTo(futureJson));
        Assert.That(
            Directory.GetFiles(testDirectory, "*.corrupt-*"),
            Is.Empty);
    }

    [Test]
    public void DevelopmentDeleteRemovesActiveBackupAndTemporaryFiles()
    {
        Directory.CreateDirectory(testDirectory);
        File.WriteAllText(savePath, "active");
        File.WriteAllText(
            CampaignSaveStore.GetBackupPath(savePath),
            "backup");
        File.WriteAllText(
            CampaignSaveStore.GetTemporaryPath(savePath),
            "temporary");

        CampaignSaveStore.DeleteDevelopmentSave(savePath);

        Assert.That(File.Exists(savePath), Is.False);
        Assert.That(
            File.Exists(CampaignSaveStore.GetBackupPath(savePath)),
            Is.False);
        Assert.That(
            File.Exists(CampaignSaveStore.GetTemporaryPath(savePath)),
            Is.False);
    }
}
