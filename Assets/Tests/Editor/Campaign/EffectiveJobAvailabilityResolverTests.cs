using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public sealed class EffectiveJobAvailabilityResolverTests
{
    [Test]
    public void ResolveIntersectsLevelRulesAndUnlocksInFixedOrder()
    {
        List<LevelJobRule> levelRules = new()
        {
            Rule(DwarfJobType.LadderBuilder, 4),
            Rule(DwarfJobType.Tunneller, 2),
            Rule(DwarfJobType.DirectionAlter, 8),
            Rule(DwarfJobType.Digger, 3)
        };

        IReadOnlyList<EffectiveJobAvailability> result =
            EffectiveJobAvailabilityResolver.Resolve(
                levelRules,
                new[]
                {
                    DwarfJobType.Digger,
                    DwarfJobType.DirectionAlter,
                    DwarfJobType.LadderBuilder
                });

        Assert.That(result.Count, Is.EqualTo(3));
        Assert.That(result[0].JobType,
            Is.EqualTo(DwarfJobType.DirectionAlter));
        Assert.That(result[1].JobType,
            Is.EqualTo(DwarfJobType.Digger));
        Assert.That(result[2].JobType,
            Is.EqualTo(DwarfJobType.LadderBuilder));
        Assert.That(result[0].StartingCount, Is.EqualTo(8));
    }

    [Test]
    public void ResolveExcludesUnlockedJobAbsentFromLevel()
    {
        IReadOnlyList<EffectiveJobAvailability> result =
            EffectiveJobAvailabilityResolver.Resolve(
                new[] { Rule(DwarfJobType.Tunneller, 2) },
                new[]
                {
                    DwarfJobType.Tunneller,
                    DwarfJobType.StairBuilder
                });

        Assert.That(result.Count, Is.EqualTo(1));
        Assert.That(result[0].JobType,
            Is.EqualTo(DwarfJobType.Tunneller));
    }

    [Test]
    public void ResolveClampsInvalidCounts()
    {
        LevelJobRule rule = new()
        {
            jobType = DwarfJobType.Digger,
            defaultCount = -4,
            maximumCount = -2
        };

        IReadOnlyList<EffectiveJobAvailability> result =
            EffectiveJobAvailabilityResolver.Resolve(
                new[] { rule },
                new[] { DwarfJobType.Digger });

        Assert.That(result[0].StartingCount, Is.Zero);
        Assert.That(result[0].MaximumCount, Is.Zero);
    }

    [Test]
    public void InventoryUsesOnlyResolvedJobsAndRestoresThemOnReset()
    {
        GameObject inventoryObject = new("Job Inventory Test");

        try
        {
            DwarfJobInventory inventory =
                inventoryObject.AddComponent<DwarfJobInventory>();

            IReadOnlyList<EffectiveJobAvailability> jobs =
                EffectiveJobAvailabilityResolver.Resolve(
                    new[]
                    {
                        Rule(DwarfJobType.DirectionAlter, 5),
                        Rule(DwarfJobType.Tunneller, 7)
                    },
                    new[] { DwarfJobType.DirectionAlter });

            inventory.ConfigureForLevel(jobs);

            Assert.That(inventory.GetCount(
                DwarfJobType.DirectionAlter), Is.EqualTo(5));
            Assert.That(inventory.GetCount(
                DwarfJobType.Tunneller), Is.Zero);
            Assert.That(inventory.TryConsume(
                DwarfJobType.DirectionAlter), Is.True);

            inventory.ResetToStartingStock();

            Assert.That(inventory.GetCount(
                DwarfJobType.DirectionAlter), Is.EqualTo(5));
        }
        finally
        {
            Object.DestroyImmediate(inventoryObject);
        }
    }

    private static LevelJobRule Rule(
        DwarfJobType jobType,
        int count)
    {
        return new LevelJobRule
        {
            jobType = jobType,
            defaultCount = count,
            maximumCount = count
        };
    }
}
