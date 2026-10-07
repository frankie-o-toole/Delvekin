using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

[DefaultExecutionOrder(-500)]
[AddComponentMenu("Delvekin/Campaign Progress Service")]
public sealed class CampaignProgressService : MonoBehaviour
{
    [SerializeField]
    private CampaignStartDefinition startDefinition;

    [SerializeField]
    private bool initializeOnAwake = true;

    [SerializeField]
    private bool persistAcrossScenes = true;

    [Header("Persistence")]
    [SerializeField]
    private bool automaticPersistence = true;

    [SerializeField]
    private string campaignSaveFileName =
        CampaignSaveStore.DefaultFileName;

    private CampaignSaveData current;
    private bool persistenceBlocked;

    public static CampaignProgressService Instance { get; private set; }

    public bool IsInitialized => current != null;
    public string CampaignId => current?.campaignId ?? string.Empty;
    public int TotalEarnedOre => current?.earnedOre ?? 0;
    public int TotalSpentOre => current?.spentOre ?? 0;
    public int AvailableOre => current?.AvailableOre ?? 0;
    public string SavePath => Path.Combine(
        Application.persistentDataPath,
        string.IsNullOrWhiteSpace(campaignSaveFileName)
            ? CampaignSaveStore.DefaultFileName
            : Path.GetFileName(campaignSaveFileName));

    public event Action CampaignChanged;
    public event Action<int, string> OreSpent;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError(
                "Only one CampaignProgressService may be active.",
                this);
            enabled = false;
            return;
        }

        Instance = this;

        if (persistAcrossScenes && Application.isPlaying)
        {
            DontDestroyOnLoad(gameObject);
        }

        if (initializeOnAwake)
        {
            if (ShouldUsePersistence)
            {
                LoadPersistentCampaign();
            }
            else
            {
                InitializeNewCampaign();
            }
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void InitializeNewCampaign()
    {
        current = startDefinition != null
            ? startDefinition.CreateNewCampaign()
            : CampaignSaveData.CreateNew();

        PublishCampaignChange();
    }

    public void LoadPersistentCampaign()
    {
        persistenceBlocked = false;

        try
        {
            CampaignSaveLoadResult loaded =
                CampaignSaveStore.LoadOrCreate(
                    SavePath,
                    CreateFreshCampaignData);
            current = loaded.Data.Clone();
            current.Normalize();

            if (!string.IsNullOrWhiteSpace(
                    loaded.ArchivedCorruptPath))
            {
                Debug.LogWarning(
                    "A corrupt campaign save was archived to '" +
                    loaded.ArchivedCorruptPath + "'.");
            }

            CampaignChanged?.Invoke();
        }
        catch (CampaignSaveUnsupportedVersionException exception)
        {
            persistenceBlocked = true;
            current = CreateFreshCampaignData();
            Debug.LogError(
                $"Campaign save was preserved but cannot be loaded: " +
                exception.Message,
                this);
            CampaignChanged?.Invoke();
        }
        catch (Exception exception)
        {
            persistenceBlocked = true;
            current = CreateFreshCampaignData();
            Debug.LogError(
                $"Campaign persistence failed without overwriting the " +
                $"existing save: {exception.Message}",
                this);
            CampaignChanged?.Invoke();
        }
    }

    public void LoadFromData(CampaignSaveData data)
    {
        if (data == null)
        {
            throw new ArgumentNullException(nameof(data));
        }

        if (data.schemaVersion > CampaignSaveData.CurrentSchemaVersion)
        {
            throw new InvalidOperationException(
                $"Campaign schema {data.schemaVersion} is newer than " +
                $"supported schema " +
                $"{CampaignSaveData.CurrentSchemaVersion}.");
        }

        current = data.Clone();
        current.Normalize();
        PublishCampaignChange();
    }

    public CampaignSaveData CreateSnapshot()
    {
        EnsureInitialized();
        return current.Clone();
    }

    public bool IsJobUnlocked(DwarfJobType jobType)
    {
        if (current == null ||
            !DwarfJobIdUtility.TryGetId(jobType, out string jobId))
        {
            return false;
        }

        return current.unlockedJobIds.Contains(jobId);
    }

    public bool UnlockJob(DwarfJobType jobType)
    {
        EnsureInitialized();

        if (!DwarfJobFactory.IsImplemented(jobType) ||
            !DwarfJobIdUtility.TryGetId(jobType, out string jobId) ||
            current.unlockedJobIds.Contains(jobId))
        {
            return false;
        }

        current.unlockedJobIds.Add(jobId);
        PublishCampaignChange();
        return true;
    }

    public IReadOnlyList<DwarfJobType> GetUnlockedJobs()
    {
        if (current == null)
        {
            return Array.Empty<DwarfJobType>();
        }

        List<DwarfJobType> result = new();

        foreach (string jobId in current.unlockedJobIds)
        {
            if (DwarfJobIdUtility.TryGetJobType(
                    jobId,
                    out DwarfJobType jobType))
            {
                result.Add(jobType);
            }
        }

        return result;
    }

    public CampaignLevelProgressData GetLevelProgress(
        string levelId)
    {
        if (current == null || string.IsNullOrWhiteSpace(levelId))
        {
            return null;
        }

        string normalizedId = levelId.Trim();

        foreach (CampaignLevelProgressData progress in
                 current.levelProgress)
        {
            if (string.Equals(
                    progress.levelId,
                    normalizedId,
                    StringComparison.Ordinal))
            {
                return progress.Clone();
            }
        }

        return null;
    }

    public LevelRewardResult ResolveLevelReward(
        string levelId,
        LevelOutcome outcome,
        int deliveredResources,
        int totalOreCapacity)
    {
        if (string.IsNullOrWhiteSpace(levelId))
        {
            throw new ArgumentException(
                "A stable level ID is required.",
                nameof(levelId));
        }

        EnsureInitialized();

        string normalizedId = levelId.Trim();
        CampaignLevelProgressData previous =
            GetLevelProgress(normalizedId);

        return LevelRewardCalculator.Calculate(
            normalizedId,
            outcome,
            deliveredResources,
            totalOreCapacity,
            previous?.completed ?? false,
            previous?.bestDeliveredResources ?? 0);
    }

    public LevelRewardResult CommitLevelAttempt(
        string levelId,
        LevelOutcome outcome,
        int deliveredResources,
        int totalOreCapacity,
        double simulationSeconds)
    {
        LevelRewardResult reward = ResolveLevelReward(
            levelId,
            outcome,
            deliveredResources,
            totalOreCapacity);

        int updatedEarnedOre = current.earnedOre;

        if (reward.NewlyEarnedResources > 0)
        {
            updatedEarnedOre = checked(
                current.earnedOre + reward.NewlyEarnedResources);
        }

        bool progressChanged = ApplyLevelProgress(
            reward.LevelId,
            outcome,
            reward.AttemptDelivered,
            reward.TotalOreCapacity,
            simulationSeconds);
        bool walletChanged = updatedEarnedOre != current.earnedOre;

        current.earnedOre = updatedEarnedOre;

        if (progressChanged || walletChanged)
        {
            PublishCampaignChange();
        }

        return reward;
    }

    public bool TrySpendOre(
        int amount,
        string reason)
    {
        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(amount),
                "Ore spending must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException(
                "A spending reason is required.",
                nameof(reason));
        }

        EnsureInitialized();

        if (amount > current.AvailableOre)
        {
            return false;
        }

        current.spentOre = checked(current.spentOre + amount);
        string normalizedReason = reason.Trim();

        OreSpent?.Invoke(amount, normalizedReason);
        PublishCampaignChange();
        return true;
    }

    public bool RecordLevelAttempt(
        string levelId,
        LevelAttemptResult attempt)
    {
        if (string.IsNullOrWhiteSpace(levelId))
        {
            throw new ArgumentException(
                "A stable level ID is required.",
                nameof(levelId));
        }

        if (attempt == null)
        {
            throw new ArgumentNullException(nameof(attempt));
        }

        EnsureInitialized();

        bool changed = ApplyLevelProgress(
            levelId.Trim(),
            attempt.Outcome,
            attempt.DeliveredResources,
            attempt.TotalOreCapacity,
            attempt.SimulationSeconds);

        if (changed)
        {
            PublishCampaignChange();
        }

        return changed;
    }

    private bool ApplyLevelProgress(
        string normalizedId,
        LevelOutcome outcome,
        int deliveredResources,
        int totalOreCapacity,
        double simulationSeconds)
    {
        if (outcome != LevelOutcome.Success)
        {
            return false;
        }

        CampaignLevelProgressData progress = null;

        foreach (CampaignLevelProgressData candidate in
                 current.levelProgress)
        {
            if (string.Equals(
                    candidate.levelId,
                    normalizedId,
                    StringComparison.Ordinal))
            {
                progress = candidate;
                break;
            }
        }

        if (progress == null)
        {
            progress = new CampaignLevelProgressData
            {
                levelId = normalizedId
            };
            current.levelProgress.Add(progress);
        }

        int capacity = Math.Max(0, totalOreCapacity);
        int delivered = Math.Min(
            capacity,
            Math.Max(0, deliveredResources));
        double elapsed = Math.Max(0d, simulationSeconds);

        bool changed = !progress.completed;
        progress.completed = true;

        if (delivered >
            progress.bestDeliveredResources)
        {
            progress.bestDeliveredResources =
                delivered;
            changed = true;
        }

        bool perfectRun = capacity > 0 && delivered == capacity;

        if (perfectRun &&
            !progress.perfectResourceRun)
        {
            progress.perfectResourceRun = true;
            changed = true;
        }

        if (elapsed > 0d &&
            (progress.bestSimulationSeconds <= 0d ||
             elapsed <
             progress.bestSimulationSeconds))
        {
            progress.bestSimulationSeconds =
                elapsed;
            changed = true;
        }

        return changed;
    }

    private void EnsureInitialized()
    {
        if (current == null)
        {
            InitializeNewCampaign();
        }
    }

    [ContextMenu("Development/Reset Campaign Save")]
    public void ResetCampaignSaveForDevelopment()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        CampaignSaveStore.DeleteDevelopmentSave(SavePath);
        persistenceBlocked = false;
        InitializeNewCampaign();
        Debug.Log($"Campaign progress reset at '{SavePath}'.", this);
#else
        Debug.LogWarning(
            "Campaign reset is only available in the Editor or a " +
            "Development Build.",
            this);
#endif
    }

    public bool SaveNow()
    {
        if (!ShouldUsePersistence || persistenceBlocked || current == null)
        {
            return false;
        }

        try
        {
            CampaignSaveStore.Save(SavePath, current);
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogError(
                $"Failed to save campaign to '{SavePath}': " +
                exception.Message,
                this);
            return false;
        }
    }

    private bool ShouldUsePersistence =>
        automaticPersistence && Application.isPlaying;

    private CampaignSaveData CreateFreshCampaignData()
    {
        return startDefinition != null
            ? startDefinition.CreateNewCampaign()
            : CampaignSaveData.CreateNew();
    }

    private void PublishCampaignChange()
    {
        SaveNow();
        CampaignChanged?.Invoke();
    }
}
