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
}
