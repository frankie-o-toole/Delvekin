using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class LevelPersistenceTests
{
    private readonly List<string> createdFiles = new();

    [TearDown]
    public void TearDown()
    {
        foreach (string path in createdFiles)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        createdFiles.Clear();
    }

    [Test]
    public void VersionedRoundTripPreservesCompleteLevel()
    {
        LevelSaveData expected = CreateCompleteLevel();
        string fileName = "level-roundtrip-test";
        createdFiles.Add(LevelSerializer.GetPath(fileName));

        Assert.That(LevelSerializer.Save(expected, fileName), Is.True);

        LevelSaveData actual = LevelSerializer.Load(fileName);

        Assert.That(actual, Is.Not.Null);
        Assert.That(actual.schemaVersion,
            Is.EqualTo(LevelDefinition.CurrentSchemaVersion));
        Assert.That(actual.chunkSize, Is.EqualTo(Chunk.ChunkSize));
        Assert.That(actual.levelId, Is.EqualTo(expected.levelId));
        Assert.That(actual.displayName, Is.EqualTo(expected.displayName));
        Assert.That(actual.totalDwarves, Is.EqualTo(24));
        Assert.That(actual.requiredMinedResources, Is.EqualTo(18));
        Assert.That(actual.jobRules.Count, Is.EqualTo(2));
        Assert.That(actual.jobRules[0].jobType,
            Is.EqualTo(DwarfJobType.Tunneller));
        Assert.That(actual.jobRules[0].defaultCount, Is.EqualTo(3));
        Assert.That(actual.jobRules[0].maximumCount, Is.EqualTo(5));
        Assert.That(actual.originInChunks,
            Is.EqualTo(expected.originInChunks));
        Assert.That(actual.sizeInChunks,
            Is.EqualTo(expected.sizeInChunks));
        Assert.That(actual.gameplayBoundsMinimum,
            Is.EqualTo(expected.gameplayBoundsMinimum));
        Assert.That(actual.gameplayBoundsSize,
            Is.EqualTo(expected.gameplayBoundsSize));
        Assert.That(actual.voxels.Count, Is.EqualTo(3));
        Assert.That(actual.voxels[1].Type, Is.EqualTo(VoxelType.Water));
        Assert.That(actual.voxels[1].Amount,
            Is.EqualTo(WaterAmount.Half));
        Assert.That(actual.entities.Count, Is.EqualTo(4));
        Assert.That(actual.entities[0].entityId, Is.EqualTo("source-a"));
        Assert.That(actual.entities[0].supplyUnitsPerTick, Is.EqualTo(64));
        Assert.That(actual.entities[1].outletCapacityOverride,
            Is.EqualTo(12));
        Assert.That(actual.entities[2].spawnMarkerLocalPosition,
            Is.EqualTo(new Vector3(0.5f, 0f, 1.5f)));
        Assert.That(actual.entities[3].type,
            Is.EqualTo(LevelEntityType.OreRock));
        Assert.That(actual.entities[3].oreCapacity, Is.EqualTo(60));
    }

    [Test]
    public void DefinitionRoundTripPreservesRuntimeSnapshot()
    {
        LevelDefinition original =
            ScriptableObject.CreateInstance<LevelDefinition>();
        LevelDefinition restored =
            ScriptableObject.CreateInstance<LevelDefinition>();

        try
        {
            original.ReplaceAllContent(CreateCompleteLevel());
            restored.ReplaceAllContent(original.CreateSaveData());

            LevelDefinition.RuntimeSnapshot snapshot =
                restored.CreateRuntimeSnapshot();

            Assert.That(snapshot.SchemaVersion,
                Is.EqualTo(LevelDefinition.CurrentSchemaVersion));
            Assert.That(snapshot.LevelId,
                Is.EqualTo("persistence-test-level"));
            Assert.That(snapshot.TotalDwarves, Is.EqualTo(24));
            Assert.That(snapshot.RequiredMinedResources, Is.EqualTo(18));
            Assert.That(snapshot.JobRules.Count, Is.EqualTo(2));
            Assert.That(snapshot.OriginInChunks,
                Is.EqualTo(new Vector3Int(-1, 0, 2)));
            Assert.That(snapshot.GameplayBoundsMinimum,
                Is.EqualTo(new Vector3Int(-10, 1, 35)));
            Assert.That(snapshot.Voxels.Count, Is.EqualTo(3));
            Assert.That(snapshot.Entities.Count, Is.EqualTo(4));
            Assert.That(snapshot.Entities[2].SpawnVoxel,
                Is.EqualTo(new Vector3Int(-3, 2, 39)));
            Assert.That(snapshot.TotalOreCapacity, Is.EqualTo(60));
            Assert.That(restored.TotalOreCapacity, Is.EqualTo(60));
        }
        finally
        {
            Object.DestroyImmediate(original);
            Object.DestroyImmediate(restored);
        }
    }

    [Test]
    public void LegacyVoxelFileImportsWithInferredBounds()
    {
        string fileName = "legacy-level-import-test";
        string path = LevelSerializer.GetPath(fileName);
        createdFiles.Add(path);
        File.WriteAllText(
            path,
            "{\"voxels\":[" +
            "{\"x\":-1,\"y\":2,\"z\":17," +
            "\"type\":1,\"facing\":0}]}" );

        LogAssert.Expect(
            LogType.Warning,
            new Regex("Imported legacy voxel-only level"));

        LevelSaveData imported = LevelSerializer.Load(fileName);

        Assert.That(imported, Is.Not.Null);
        Assert.That(imported.schemaVersion,
            Is.EqualTo(LevelDefinition.CurrentSchemaVersion));
        Assert.That(imported.originInChunks,
            Is.EqualTo(new Vector3Int(-1, 0, 1)));
        Assert.That(imported.sizeInChunks, Is.EqualTo(Vector3Int.one));
        Assert.That(imported.voxels.Count, Is.EqualTo(1));
        Assert.That(imported.voxels[0].Amount,
            Is.EqualTo(WaterAmount.Full));
        Assert.That(imported.entities, Is.Empty);
        Assert.That(imported.levelId, Is.Not.Empty);
        Assert.That(imported.totalDwarves, Is.EqualTo(20));
        Assert.That(imported.requiredMinedResources, Is.EqualTo(1));
    }

    [Test]
    public void ValidatorRejectsDuplicateVoxelsAndEntities()
    {
        LevelSaveData data = CreateCompleteLevel();
        data.voxels.Add(data.voxels[0]);
        data.entities.Add(data.entities[0]);
        List<string> errors = new();

        bool valid = LevelSaveValidator.Validate(data, errors);

        Assert.That(valid, Is.False);
        Assert.That(errors,
            Has.Some.Contains("Duplicate voxel record"));
        Assert.That(errors,
            Has.Some.Contains("unique, non-empty ID"));
    }

    [Test]
    public void ValidatorRejectsEmptyOreRock()
    {
        LevelSaveData data = CreateCompleteLevel();
        data.entities[3].oreCapacity = 0;
        List<string> errors = new();

        bool valid = LevelSaveValidator.Validate(data, errors);

        Assert.That(valid, Is.False);
        Assert.That(errors,
            Has.Some.Contains("must contain at least one unit of ore"));
    }

    [Test]
    public void ValidatorRejectsTargetAboveTotalOreCapacity()
    {
        LevelSaveData data = CreateCompleteLevel();
        data.requiredMinedResources = 20;
        data.entities[3].oreCapacity = 19;
        List<string> errors = new();

        bool valid = LevelSaveValidator.Validate(data, errors);

        Assert.That(valid, Is.False);
        Assert.That(errors,
            Has.Some.Contains("exceed total Ore Rock capacity"));
    }

    [Test]
    public void OreRockCentreSnapsToVoxelGridForEvenVolume()
    {
        LevelEntitySaveRecord saved = new()
        {
            entityId = "off-grid-ore",
            type = LevelEntityType.OreRock,
            position = new Vector3(3.24f, 4.8f, 48.49f),
            volumeSize = new Vector3Int(6, 8, 6),
            oreCapacity = 10
        };

        LevelEntityRecord record = saved.ToRuntime();

        Assert.That(record.Position, Is.EqualTo(new Vector3(3f, 5f, 48f)));
        Assert.That(record.MinimumVoxel, Is.EqualTo(new Vector3Int(0, 1, 45)));
    }

    [Test]
    public void SchemaEightRescueGoalMigratesToMinedResources()
    {
        string fileName = "schema-eight-resource-migration-test";
        string path = LevelSerializer.GetPath(fileName);
        createdFiles.Add(path);

        LevelSaveData source = CreateCompleteLevel();
        source.schemaVersion = 8;

        string json = JsonUtility.ToJson(source)
            .Replace(
                "\"requiredMinedResources\":18",
                "\"requiredRescues\":18");

        File.WriteAllText(path, json);

        LogAssert.Expect(
            LogType.Log,
            new Regex("Migrated.*schema 8.*schema 9"));

        LevelSaveData migrated = LevelSerializer.Load(fileName);

        Assert.That(migrated, Is.Not.Null);
        Assert.That(migrated.requiredMinedResources, Is.EqualTo(18));
        Assert.That(migrated.schemaVersion, Is.EqualTo(9));
    }

    [Test]
    public void NewerSchemaIsRejectedWithoutMutatingWorldData()
    {
        string fileName = "future-level-schema-test";
        string path = LevelSerializer.GetPath(fileName);
        createdFiles.Add(path);
        LevelSaveData data = CreateCompleteLevel();
        data.schemaVersion = LevelDefinition.CurrentSchemaVersion + 1;
        File.WriteAllText(path, JsonUtility.ToJson(data));

        LogAssert.Expect(
            LogType.Error,
            new Regex("newer than supported schema"));

        Assert.That(LevelSerializer.Load(fileName), Is.Null);
    }

    private static LevelSaveData CreateCompleteLevel()
    {
        return new LevelSaveData
        {
            schemaVersion = LevelDefinition.CurrentSchemaVersion,
            chunkSize = Chunk.ChunkSize,
            levelId = "persistence-test-level",
            displayName = "Persistence Test",
            totalDwarves = 24,
            requiredMinedResources = 18,
            jobRules = new List<LevelJobRule>
            {
                new()
                {
                    jobType = DwarfJobType.Tunneller,
                    defaultCount = 3,
                    maximumCount = 5
                },
                new()
                {
                    jobType = DwarfJobType.DirectionAlter,
                    defaultCount = 2,
                    maximumCount = 2
                }
            },
            originInChunks = new Vector3Int(-1, 0, 2),
            sizeInChunks = new Vector3Int(2, 2, 2),
            gameplayBoundsMinimum = new Vector3Int(-10, 1, 35),
            gameplayBoundsSize = new Vector3Int(20, 20, 20),
            voxels = new List<LevelVoxelRecord>
            {
                new(
                    new Vector3Int(-4, 1, 36),
                    VoxelType.Dirt,
                    PuzzleSide.North),
                new(
                    new Vector3Int(-3, 1, 36),
                    VoxelType.Water,
                    PuzzleSide.East,
                    WaterAmount.Half),
                new(
                    new Vector3Int(-2, 1, 36),
                    VoxelType.Granite,
                    PuzzleSide.South)
            },
            entities = new List<LevelEntitySaveRecord>
            {
                new()
                {
                    entityId = "source-a",
                    type = LevelEntityType.WaterSource,
                    position = new Vector3(-4.5f, 2.5f, 37.5f),
                    volumeSize = new Vector3Int(1, 1, 1),
                    facing = PuzzleSide.East,
                    overrideMaximumFillY = true,
                    maximumFillY = 4,
                    supplyUnitsPerTick = 64
                },
                new()
                {
                    entityId = "outlet-a",
                    type = LevelEntityType.WaterOutlet,
                    position = new Vector3(-1.5f, 2.5f, 37.5f),
                    volumeSize = new Vector3Int(1, 1, 1),
                    facing = PuzzleSide.West,
                    outletCapacityOverride = 12
                },
                new()
                {
                    entityId = "spawn-a",
                    type = LevelEntityType.SpawnHouse,
                    position = new Vector3(-3.5f, 2f, 37.5f),
                    volumeSize = new Vector3Int(5, 4, 5),
                    facing = PuzzleSide.North,
                    spawnMarkerLocalPosition =
                        new Vector3(0.5f, 0f, 1.5f)
                },
                new()
                {
                    entityId = "ore-a",
                    type = LevelEntityType.OreRock,
                    position = new Vector3(3f, 4f, 48f),
                    volumeSize = new Vector3Int(6, 8, 6),
                    facing = PuzzleSide.South,
                    oreCapacity = 60
                }
            }
        };
    }
}
