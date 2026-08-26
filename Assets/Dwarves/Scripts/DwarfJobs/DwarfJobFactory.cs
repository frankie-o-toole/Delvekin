public static class DwarfJobFactory
{
    private const float DefaultTunnelCycleDuration = 0.65f;

    private const float DefaultDiggerCycleDuration = 0.65f;
    public static bool IsImplemented(
        DwarfJobType type)
    {
        return type ==
                   DwarfJobType.DirectionAlter ||
               type ==
                   DwarfJobType.Tunneller ||
               type ==
                   DwarfJobType.Digger;
    }

    public static bool TryCreate(
        DwarfJobType type,
        DwarfAgent target,
        out IDwarfJob job,
        out string failureReason)
    {
        job = null;

        if (target == null)
        {
            failureReason =
                "No dwarf was provided.";

            return false;
        }

        switch (type)
        {
            case DwarfJobType.DirectionAlter:
                {
                    PuzzleSide outputDirection =
                        DirectionUtility.Opposite(
                            target.Facing);

                    job =
                        new DirectionAltererJob(
                            outputDirection);

                    failureReason =
                        string.Empty;

                    return true;
                }

            case DwarfJobType.Tunneller:
                {
                    DwarfJobTuning tuning =
                        target.GetComponent<DwarfJobTuning>();

                    float cycleDuration =
                        tuning != null
                            ? tuning.TunnelDepthCycleDuration
                            : DefaultTunnelCycleDuration;

                    job =
                        new TunnellerJob(
                            cycleDuration);

                    failureReason =
                        string.Empty;

                    return true;
                }

            case DwarfJobType.Digger:
                {
                    DwarfJobTuning tuning =
                        target.GetComponent<DwarfJobTuning>();

                    float cycleDuration =
                        tuning != null
                            ? tuning.DiggerDepthCycleDuration
                            : DefaultDiggerCycleDuration;

                    job =
                        new DiggerJob(
                            cycleDuration);

                    failureReason =
                        string.Empty;

                    return true;
                }

            case DwarfJobType.None:
                failureReason =
                    "No job is selected.";

                return false;

            default:
                failureReason =
                    $"{type} has not been implemented yet.";

                return false;
        }
    }
}