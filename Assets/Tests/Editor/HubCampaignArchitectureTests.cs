using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public sealed class HubCampaignArchitectureTests
{
    private readonly List<Object> createdObjects = new();
    private CampaignProgressService campaign;

    [SetUp]
    public void SetUp()
    {
        GameObject serviceObject = CreateGameObject("Campaign Service");
        campaign = serviceObject.AddComponent<CampaignProgressService>();
        campaign.LoadFromData(CampaignSaveData.CreateNew());
    }

    [TearDown]
    public void TearDown()
    {
        for (int index = createdObjects.Count - 1; index >= 0; index--)
        {
            if (createdObjects[index] != null)
            {
                Object.DestroyImmediate(createdObjects[index]);
            }
        }

        createdObjects.Clear();
    }

    [Test]
    public void StartingNodeIsAvailableWithoutProgress()
    {
        LevelDefinition level = CreateLevel("level-a", "Level A");
        HubLevelNode node = new(
            level,
            startsUnlocked: true,
            HubLevelUnlockMode.CompleteAllPrerequisites);

        HubLevelState state =
            HubLevelStateResolver.Resolve(node, campaign);

        Assert.That(
            state.Availability,
            Is.EqualTo(HubLevelAvailability.Available));
        Assert.That(state.IsLaunchable, Is.True);
    }

    [Test]
    public void LockedNodeRequiresCompletedPrerequisite()
    {
        LevelDefinition prerequisite =
            CreateLevel("level-a", "Level A");
        LevelDefinition level = CreateLevel("level-b", "Level B");
        HubLevelNode node = new(
            level,
            startsUnlocked: false,
            HubLevelUnlockMode.CompleteAllPrerequisites,
            new[] { prerequisite });

        HubLevelState locked =
            HubLevelStateResolver.Resolve(node, campaign);
        campaign.RecordLevelAttempt(
            prerequisite.LevelId,
            CreateSuccessfulAttempt(20, 30));
        HubLevelState available =
            HubLevelStateResolver.Resolve(node, campaign);

        Assert.That(
            locked.Availability,
            Is.EqualTo(HubLevelAvailability.Locked));
        Assert.That(
            available.Availability,
            Is.EqualTo(HubLevelAvailability.Available));
    }

    [Test]
    public void AnyPrerequisiteSupportsCampaignBranches()
    {
        LevelDefinition branchA = CreateLevel("branch-a", "Branch A");
        LevelDefinition branchB = CreateLevel("branch-b", "Branch B");
        LevelDefinition merge = CreateLevel("merge", "Merge");
        HubLevelNode node = new(
            merge,
            startsUnlocked: false,
            HubLevelUnlockMode.CompleteAnyPrerequisite,
            new[] { branchA, branchB });

        campaign.RecordLevelAttempt(
            branchB.LevelId,
            CreateSuccessfulAttempt(10, 20));

        HubLevelState state =
            HubLevelStateResolver.Resolve(node, campaign);

        Assert.That(
            state.Availability,
            Is.EqualTo(HubLevelAvailability.Available));
    }

    [Test]
    public void CompletedAndPerfectProgressMapToDistinctHubStates()
    {
        LevelDefinition completedLevel =
            CreateLevel("completed", "Completed");
        LevelDefinition perfectLevel =
            CreateLevel("perfect", "Perfect");
        campaign.RecordLevelAttempt(
            completedLevel.LevelId,
            CreateSuccessfulAttempt(20, 30));
        campaign.RecordLevelAttempt(
            perfectLevel.LevelId,
            CreateSuccessfulAttempt(30, 30));

        HubLevelState completed = HubLevelStateResolver.Resolve(
            new HubLevelNode(
                completedLevel,
                true,
                HubLevelUnlockMode.CompleteAllPrerequisites),
            campaign);
        HubLevelState perfect = HubLevelStateResolver.Resolve(
            new HubLevelNode(
                perfectLevel,
                true,
                HubLevelUnlockMode.CompleteAllPrerequisites),
            campaign);

        Assert.That(
            completed.Availability,
            Is.EqualTo(HubLevelAvailability.Completed));
        Assert.That(
            perfect.Availability,
            Is.EqualTo(HubLevelAvailability.Perfect));
        Assert.That(perfect.BestDeliveredResources, Is.EqualTo(30));
    }

    [Test]
    public void CampaignDefinitionReportsDuplicateAndUnreachableNodes()
    {
        LevelDefinition duplicateA =
            CreateLevel("duplicate", "Duplicate A");
        LevelDefinition duplicateB =
            CreateLevel("duplicate", "Duplicate B");
        HubCampaignDefinition definition =
            CreateScriptable<HubCampaignDefinition>();
        definition.ReplaceLevels(new[]
        {
            new HubLevelNode(
                duplicateA,
                true,
                HubLevelUnlockMode.CompleteAllPrerequisites),
            new HubLevelNode(
                duplicateB,
                false,
                HubLevelUnlockMode.CompleteAllPrerequisites)
        });

        IReadOnlyList<string> errors =
            definition.GetValidationErrors();

        Assert.That(errors, Has.Count.EqualTo(2));
        Assert.That(errors[0] + errors[1], Does.Contain("duplicate"));
        Assert.That(errors[0] + errors[1], Does.Contain("unreachable"));
    }

    [Test]
    public void EntranceActivationSelectsButDoesNotAutoLaunchLevel()
    {
        LevelDefinition level = CreateLevel("level-a", "Level A");
        HubCampaignDefinition definition =
            CreateScriptable<HubCampaignDefinition>();
        definition.ReplaceLevels(new[]
        {
            new HubLevelNode(
                level,
                true,
                HubLevelUnlockMode.CompleteAllPrerequisites)
        });

        GameObject controllerObject = CreateGameObject("Hub Controller");
        HubCampaignController controller =
            controllerObject.AddComponent<HubCampaignController>();
        controller.Configure(definition, campaign);

        GameObject entranceObject = CreateGameObject("Mine Entrance");
        MineEntrance entrance = entranceObject.AddComponent<MineEntrance>();
        entrance.Configure(level);
        controller.RegisterEntrance(entrance);

        bool activated = entrance.RequestActivation();

        Assert.That(activated, Is.True);
        Assert.That(controller.SelectedLevelState.LevelId, Is.EqualTo("level-a"));
    }

    private LevelDefinition CreateLevel(
        string levelId,
        string displayName)
    {
        LevelDefinition level = CreateScriptable<LevelDefinition>();
        level.ReplaceAllContent(new LevelSaveData
        {
            schemaVersion = LevelDefinition.CurrentSchemaVersion,
            chunkSize = Chunk.ChunkSize,
            levelId = levelId,
            displayName = displayName,
            totalDwarves = 10,
            requiredMinedResources = 1,
            sizeInChunks = Vector3Int.one,
            gameplayBoundsSize =
                Vector3Int.one * Chunk.ChunkSize
        });
        return level;
    }

    private static LevelAttemptResult CreateSuccessfulAttempt(
        int delivered,
        int capacity)
    {
        return new LevelAttemptResult(
            LevelOutcome.Success,
            totalDwarves: 10,
            totalOreCapacity: capacity,
            requiredMinedResources: 1,
            spawned: 10,
            deliveredResources: delivered,
            died: 0,
            recalled: 0,
            activeAtEnd: 0,
            unspawnedAtEnd: 0,
            simulationSeconds: 60d);
    }

    private GameObject CreateGameObject(string objectName)
    {
        GameObject value = new(objectName);
        createdObjects.Add(value);
        return value;
    }

    private T CreateScriptable<T>() where T : ScriptableObject
    {
        T value = ScriptableObject.CreateInstance<T>();
        createdObjects.Add(value);
        return value;
    }
}
