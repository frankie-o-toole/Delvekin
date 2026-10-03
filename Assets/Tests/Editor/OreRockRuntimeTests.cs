using NUnit.Framework;
using UnityEngine;

public sealed class OreRockRuntimeTests
{
    private GameObject oreRockObject;
    private GameObject firstDwarfObject;
    private GameObject secondDwarfObject;

    [TearDown]
    public void TearDown()
    {
        Destroy(firstDwarfObject);
        Destroy(secondDwarfObject);
        Destroy(oreRockObject);
    }

    [Test]
    public void ReservationIsAtomicAndExtractionConsumesOneUnit()
    {
        OreRockAuthoring oreRock = CreateOreRock(capacity: 1);
        DwarfAgent first = CreateDwarf(
            ref firstDwarfObject,
            Vector3Int.zero);
        DwarfAgent second = CreateDwarf(
            ref secondDwarfObject,
            Vector3Int.right * 8);

        Assert.That(oreRock.TryReserve(first), Is.True);
        Assert.That(oreRock.Remaining, Is.Zero);
        Assert.That(oreRock.Reserved, Is.EqualTo(1));
        Assert.That(oreRock.TryReserve(second), Is.False);

        OreRuntimeProgress reserved =
            OreRockAuthoring.GetRuntimeProgress();

        Assert.That(reserved.TotalCapacity, Is.EqualTo(1));
        Assert.That(reserved.Extracted, Is.Zero);
        Assert.That(reserved.Reserved, Is.EqualTo(1));
        Assert.That(reserved.Remaining, Is.Zero);
        Assert.That(reserved.IsConsistent, Is.True);

        Assert.That(oreRock.CompleteExtraction(first), Is.True);
        Assert.That(oreRock.Reserved, Is.Zero);
        Assert.That(oreRock.Extracted, Is.EqualTo(1));
        Assert.That(oreRock.Remaining, Is.Zero);
        Assert.That(oreRock.CompleteExtraction(first), Is.False);

        OreRuntimeProgress extracted =
            OreRockAuthoring.GetRuntimeProgress();

        Assert.That(extracted.Extracted, Is.EqualTo(1));
        Assert.That(extracted.Reserved, Is.Zero);
        Assert.That(extracted.Remaining, Is.Zero);
        Assert.That(extracted.IsConsistent, Is.True);
    }

    [Test]
    public void LeadingFaceFindsRuntimeOreRockWithoutColliderPhysics()
    {
        OreRockAuthoring expected = CreateOreRock(capacity: 60);
        DwarfAgent dwarf = CreateDwarf(
            ref firstDwarfObject,
            Vector3Int.zero);

        bool found = OreRockAuthoring.TryFindAtLeadingFace(
            dwarf,
            out OreRockAuthoring actual);

        Assert.That(found, Is.True);
        Assert.That(actual, Is.SameAs(expected));
    }

    private OreRockAuthoring CreateOreRock(int capacity)
    {
        oreRockObject = new GameObject("Ore Rock Test");
        oreRockObject.SetActive(false);

        OreRockAuthoring oreRock =
            oreRockObject.AddComponent<OreRockAuthoring>();

        oreRock.ConfigureIdentity("ore-test");
        oreRock.Configure(
            world: null,
            position: new Vector3(0f, 4f, 5f),
            rotation: Quaternion.identity,
            size: new Vector3Int(6, 8, 6),
            departureFacing: PuzzleSide.North,
            oreCapacity: capacity,
            configuredVisualPrefab: null,
            isRuntimeCopy: true);

        oreRockObject.SetActive(true);
        return oreRock;
    }

    private static DwarfAgent CreateDwarf(
        ref GameObject dwarfObject,
        Vector3Int anchor)
    {
        dwarfObject = new GameObject("Dwarf Test");
        dwarfObject.SetActive(false);

        GameObject visualRoot = new("VisualRoot");
        visualRoot.transform.SetParent(dwarfObject.transform, false);

        GameObject selection = new("SelectionCollider");
        selection.transform.SetParent(dwarfObject.transform, false);
        selection.AddComponent<BoxCollider>();

        DwarfAgent dwarf = dwarfObject.AddComponent<DwarfAgent>();
        dwarf.Activate(anchor, PuzzleSide.North);
        return dwarf;
    }

    private static void Destroy(GameObject target)
    {
        if (target != null)
        {
            Object.DestroyImmediate(target);
        }
    }
}
