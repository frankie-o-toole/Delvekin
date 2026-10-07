using System;
using System.Collections.Generic;
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

    private CampaignSaveData current;

    public static CampaignProgressService Instance { get; private set; }

    public bool IsInitialized => current != null;
    public string CampaignId => current?.campaignId ?? string.Empty;
    public int AvailableOre => current?.AvailableOre ?? 0;

    public event Action CampaignChanged;

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
            InitializeNewCampaign();
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

        CampaignChanged?.Invoke();
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
        CampaignChanged?.Invoke();
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
        CampaignChanged?.Invoke();
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

        if (attempt.Outcome != LevelOutcome.Success)
        {
            return false;
        }

        string normalizedId = levelId.Trim();
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

        bool changed = !progress.completed;
        progress.completed = true;

        if (attempt.DeliveredResources >
            progress.bestDeliveredResources)
        {
            progress.bestDeliveredResources =
                attempt.DeliveredResources;
            changed = true;
        }

        if (attempt.IsPerfectResourceRun &&
            !progress.perfectResourceRun)
        {
            progress.perfectResourceRun = true;
            changed = true;
        }

        if (attempt.SimulationSeconds > 0d &&
            (progress.bestSimulationSeconds <= 0d ||
             attempt.SimulationSeconds <
             progress.bestSimulationSeconds))
        {
            progress.bestSimulationSeconds =
                attempt.SimulationSeconds;
            changed = true;
        }

        if (changed)
        {
            CampaignChanged?.Invoke();
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
}
