using System;
using System.Collections.Generic;

[Serializable]
public sealed class CampaignSaveData
{
    public const int CurrentSchemaVersion = 1;

    public int schemaVersion = CurrentSchemaVersion;
    public string campaignId;
    public int earnedOre;
    public int spentOre;
    public List<string> unlockedJobIds = new();
    public List<CampaignLevelProgressData> levelProgress = new();

    public int AvailableOre =>
        Math.Max(0, earnedOre - spentOre);

    public static CampaignSaveData CreateNew(
        IEnumerable<DwarfJobType> startingJobs = null,
        int startingOre = 0)
    {
        CampaignSaveData data = new()
        {
            schemaVersion = CurrentSchemaVersion,
            campaignId = Guid.NewGuid().ToString("N"),
            earnedOre = Math.Max(0, startingOre),
            spentOre = 0
        };

        if (startingJobs != null)
        {
            foreach (DwarfJobType jobType in startingJobs)
            {
                if (DwarfJobIdUtility.TryGetId(
                        jobType,
                        out string jobId))
                {
                    data.unlockedJobIds.Add(jobId);
                }
            }
        }

        data.Normalize();
        return data;
    }

    public CampaignSaveData Clone()
    {
        CampaignSaveData clone = new()
        {
            schemaVersion = schemaVersion,
            campaignId = campaignId,
            earnedOre = earnedOre,
            spentOre = spentOre,
            unlockedJobIds = unlockedJobIds != null
                ? new List<string>(unlockedJobIds)
                : new List<string>(),
            levelProgress = new List<CampaignLevelProgressData>()
        };

        if (levelProgress != null)
        {
            foreach (CampaignLevelProgressData progress in levelProgress)
            {
                if (progress != null)
                {
                    clone.levelProgress.Add(progress.Clone());
                }
            }
        }

        return clone;
    }

    public void Normalize()
    {
        schemaVersion = CurrentSchemaVersion;
        campaignId = string.IsNullOrWhiteSpace(campaignId)
            ? Guid.NewGuid().ToString("N")
            : campaignId.Trim();
        earnedOre = Math.Max(0, earnedOre);
        spentOre = Math.Min(
            earnedOre,
            Math.Max(0, spentOre));
        unlockedJobIds ??= new List<string>();
        levelProgress ??= new List<CampaignLevelProgressData>();

        HashSet<string> uniqueJobIds =
            new(StringComparer.Ordinal);
        List<string> normalizedJobIds = new();

        foreach (string jobId in unlockedJobIds)
        {
            string normalized = DwarfJobIdUtility.Normalize(jobId);

            if (normalized.Length > 0 &&
                uniqueJobIds.Add(normalized))
            {
                normalizedJobIds.Add(normalized);
            }
        }

        unlockedJobIds = normalizedJobIds;

        Dictionary<string, int> levelIndexes =
            new(StringComparer.Ordinal);
        List<CampaignLevelProgressData> normalizedLevels = new();

        foreach (CampaignLevelProgressData progress in levelProgress)
        {
            if (progress == null)
            {
                continue;
            }

            progress.Normalize();

            if (progress.levelId.Length == 0)
            {
                continue;
            }

            if (levelIndexes.TryGetValue(
                    progress.levelId,
                    out int existingIndex))
            {
                normalizedLevels[existingIndex] = progress;
                continue;
            }

            levelIndexes.Add(
                progress.levelId,
                normalizedLevels.Count);
            normalizedLevels.Add(progress);
        }

        levelProgress = normalizedLevels;
    }
}

[Serializable]
public sealed class CampaignLevelProgressData
{
    public string levelId;
    public bool completed;
    public int bestDeliveredResources;
    public bool perfectResourceRun;
    public double bestSimulationSeconds;

    public CampaignLevelProgressData Clone()
    {
        return new CampaignLevelProgressData
        {
            levelId = levelId,
            completed = completed,
            bestDeliveredResources = bestDeliveredResources,
            perfectResourceRun = perfectResourceRun,
            bestSimulationSeconds = bestSimulationSeconds
        };
    }

    public void Normalize()
    {
        levelId = string.IsNullOrWhiteSpace(levelId)
            ? string.Empty
            : levelId.Trim();
        bestDeliveredResources =
            Math.Max(0, bestDeliveredResources);
        bestSimulationSeconds =
            Math.Max(0d, bestSimulationSeconds);
    }
}
