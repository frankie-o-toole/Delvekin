using System;
using System.IO;
using UnityEngine;

public enum CampaignLoadSource
{
    Primary,
    Backup,
    CreatedNew
}

public sealed class CampaignSaveLoadResult
{
    public CampaignSaveData Data { get; }
    public CampaignLoadSource Source { get; }
    public bool WasMigrated { get; }
    public string ArchivedCorruptPath { get; }

    public CampaignSaveLoadResult(
        CampaignSaveData data,
        CampaignLoadSource source,
        bool wasMigrated,
        string archivedCorruptPath = null)
    {
        Data = data;
        Source = source;
        WasMigrated = wasMigrated;
        ArchivedCorruptPath = archivedCorruptPath;
    }
}

public sealed class CampaignSaveUnsupportedVersionException :
    Exception
{
    public CampaignSaveUnsupportedVersionException(string message)
        : base(message)
    {
    }
}

public static class CampaignSaveStore
{
    public const string DefaultFileName = "campaign.json";

    public static string GetDefaultPath()
    {
        return Path.Combine(
            Application.persistentDataPath,
            DefaultFileName);
    }

    public static CampaignSaveLoadResult LoadOrCreate(
        string path,
        Func<CampaignSaveData> createNew)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException(
                "A campaign save path is required.",
                nameof(path));
        }

        if (createNew == null)
        {
            throw new ArgumentNullException(nameof(createNew));
        }

        string backupPath = GetBackupPath(path);

        if (!File.Exists(path))
        {
            if (TryLoadFile(
                    backupPath,
                    out CampaignSaveData backup,
                    out bool backupMigrated,
                    out _))
            {
                Save(path, backup);
                return new CampaignSaveLoadResult(
                    backup,
                    CampaignLoadSource.Backup,
                    backupMigrated);
            }

            CampaignSaveData created = CreateNormalized(createNew);
            Save(path, created);
            return new CampaignSaveLoadResult(
                created,
                CampaignLoadSource.CreatedNew,
                false);
        }

        if (TryLoadFile(
                path,
                out CampaignSaveData primary,
                out bool primaryMigrated,
                out Exception primaryError))
        {
            if (primaryMigrated)
            {
                Save(path, primary);
            }

            return new CampaignSaveLoadResult(
                primary,
                CampaignLoadSource.Primary,
                primaryMigrated);
        }

        if (primaryError is CampaignSaveUnsupportedVersionException)
        {
            throw primaryError;
        }

        string archivedPath = ArchiveCorruptFile(path);

        if (TryLoadFile(
                backupPath,
                out CampaignSaveData recovered,
                out bool recoveredMigrated,
                out Exception backupError))
        {
            Save(path, recovered);
            return new CampaignSaveLoadResult(
                recovered,
                CampaignLoadSource.Backup,
                recoveredMigrated,
                archivedPath);
        }

        if (backupError is CampaignSaveUnsupportedVersionException)
        {
            throw backupError;
        }

        CampaignSaveData replacement = CreateNormalized(createNew);
        Save(path, replacement);
        return new CampaignSaveLoadResult(
            replacement,
            CampaignLoadSource.CreatedNew,
            false,
            archivedPath);
    }

    public static void Save(
        string path,
        CampaignSaveData data)
    {
        if (data == null)
        {
            throw new ArgumentNullException(nameof(data));
        }

        CampaignSaveData snapshot = data.Clone();
        snapshot.Normalize();

        string directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string temporaryPath = GetTemporaryPath(path);
        string backupPath = GetBackupPath(path);
        string json = JsonUtility.ToJson(snapshot, true);

        try
        {
            File.WriteAllText(temporaryPath, json);

            if (File.Exists(path))
            {
                try
                {
                    File.Replace(temporaryPath, path, backupPath);
                }
                catch (PlatformNotSupportedException)
                {
                    ReplaceWithFallback(
                        temporaryPath,
                        path,
                        backupPath);
                }
            }
            else
            {
                File.Move(temporaryPath, path);
            }
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    public static void DeleteDevelopmentSave(string path)
    {
        DeleteIfPresent(path);
        DeleteIfPresent(GetBackupPath(path));
        DeleteIfPresent(GetTemporaryPath(path));
    }

    public static string GetBackupPath(string path) => path + ".bak";
    public static string GetTemporaryPath(string path) => path + ".tmp";

    private static bool TryLoadFile(
        string path,
        out CampaignSaveData data,
        out bool wasMigrated,
        out Exception error)
    {
        data = null;
        wasMigrated = false;
        error = null;

        if (!File.Exists(path))
        {
            return false;
        }

        try
        {
            string json = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new InvalidDataException(
                    "Campaign save is empty.");
            }

            data = JsonUtility.FromJson<CampaignSaveData>(json);
            if (data == null)
            {
                throw new InvalidDataException(
                    "JSON did not contain campaign data.");
            }

            int loadedVersion = data.schemaVersion;
            wasMigrated = CampaignSaveMigrator.Migrate(data);
            data.Normalize();

            if (loadedVersion < 0)
            {
                throw new InvalidDataException(
                    $"Campaign schema {loadedVersion} is invalid.");
            }

            return true;
        }
        catch (CampaignSaveUnsupportedVersionException exception)
        {
            error = exception;
            return false;
        }
        catch (Exception exception)
        {
            error = exception;
            data = null;
            return false;
        }
    }

    private static CampaignSaveData CreateNormalized(
        Func<CampaignSaveData> createNew)
    {
        CampaignSaveData data = createNew();
        if (data == null)
        {
            throw new InvalidOperationException(
                "Campaign factory returned null.");
        }

        data.Normalize();
        return data;
    }

    private static string ArchiveCorruptFile(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        string timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmssfff");
        string archivedPath = path + $".corrupt-{timestamp}";
        File.Move(path, archivedPath);
        return archivedPath;
    }

    private static void ReplaceWithFallback(
        string temporaryPath,
        string path,
        string backupPath)
    {
        File.Copy(path, backupPath, true);
        File.Delete(path);
        File.Move(temporaryPath, path);
    }

    private static void DeleteIfPresent(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
