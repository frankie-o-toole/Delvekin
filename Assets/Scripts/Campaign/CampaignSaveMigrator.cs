public static class CampaignSaveMigrator
{
    public static bool Migrate(CampaignSaveData data)
    {
        if (data.schemaVersion > CampaignSaveData.CurrentSchemaVersion)
        {
            throw new CampaignSaveUnsupportedVersionException(
                $"Campaign schema {data.schemaVersion} is newer than " +
                $"supported schema " +
                $"{CampaignSaveData.CurrentSchemaVersion}.");
        }

        if (data.schemaVersion < 0)
        {
            return false;
        }

        bool migrated = false;

        while (data.schemaVersion < CampaignSaveData.CurrentSchemaVersion)
        {
            switch (data.schemaVersion)
            {
                case 0:
                    data.schemaVersion = 1;
                    migrated = true;
                    break;

                default:
                    throw new CampaignSaveUnsupportedVersionException(
                        $"No migration exists for campaign schema " +
                        $"{data.schemaVersion}.");
            }
        }

        return migrated;
    }
}
