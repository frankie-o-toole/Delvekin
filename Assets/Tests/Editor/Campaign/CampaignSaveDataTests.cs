using System.Collections.Generic;
using NUnit.Framework;

public sealed class CampaignSaveDataTests
{
    [Test]
    public void NewCampaignHasStableIdentityAndUniqueJobIds()
    {
        CampaignSaveData data = CampaignSaveData.CreateNew(
            new[]
            {
                DwarfJobType.DirectionAlter,
                DwarfJobType.Tunneller,
                DwarfJobType.Tunneller,
                DwarfJobType.None
            },
            startingOre: 25);

        Assert.That(data.schemaVersion,
            Is.EqualTo(CampaignSaveData.CurrentSchemaVersion));
        Assert.That(data.campaignId, Has.Length.EqualTo(32));
        Assert.That(data.AvailableOre, Is.EqualTo(25));
        Assert.That(data.unlockedJobIds, Is.EqualTo(new[]
        {
            DwarfJobIdUtility.DirectionAlter,
            DwarfJobIdUtility.Tunneller
        }));
    }

    [Test]
    public void NormalizeRepairsCountsAndDuplicateIds()
    {
        CampaignSaveData data = new()
        {
            earnedOre = 10,
            spentOre = 40,
            unlockedJobIds = new List<string>
            {
                " TUNNELLER ",
                "tunneller",
                "future_job",
                string.Empty
            }
        };

        data.Normalize();

        Assert.That(data.spentOre, Is.EqualTo(10));
        Assert.That(data.AvailableOre, Is.Zero);
        Assert.That(data.unlockedJobIds, Is.EqualTo(new[]
        {
            DwarfJobIdUtility.Tunneller,
            "future_job"
        }));
    }

    [Test]
    public void CloneDoesNotShareMutableLists()
    {
        CampaignSaveData source = CampaignSaveData.CreateNew(
            new[] { DwarfJobType.DirectionAlter });
        source.levelProgress.Add(new CampaignLevelProgressData
        {
            levelId = "level-a",
            completed = true,
            bestDeliveredResources = 30
        });

        CampaignSaveData clone = source.Clone();
        clone.unlockedJobIds.Add(DwarfJobIdUtility.Digger);
        clone.levelProgress[0].bestDeliveredResources = 50;

        Assert.That(source.unlockedJobIds,
            Does.Not.Contain(DwarfJobIdUtility.Digger));
        Assert.That(
            source.levelProgress[0].bestDeliveredResources,
            Is.EqualTo(30));
    }

    [TestCase(DwarfJobType.DirectionAlter, DwarfJobIdUtility.DirectionAlter)]
    [TestCase(DwarfJobType.Tunneller, DwarfJobIdUtility.Tunneller)]
    [TestCase(DwarfJobType.Digger, DwarfJobIdUtility.Digger)]
    [TestCase(DwarfJobType.StairBuilder, DwarfJobIdUtility.StairBuilder)]
    [TestCase(DwarfJobType.LadderBuilder, DwarfJobIdUtility.LadderBuilder)]
    public void StableJobIdsRoundTrip(
        DwarfJobType expectedType,
        string expectedId)
    {
        Assert.That(
            DwarfJobIdUtility.TryGetId(
                expectedType,
                out string actualId),
            Is.True);
        Assert.That(actualId, Is.EqualTo(expectedId));
        Assert.That(
            DwarfJobIdUtility.TryGetJobType(
                actualId,
                out DwarfJobType actualType),
            Is.True);
        Assert.That(actualType, Is.EqualTo(expectedType));
    }
}
