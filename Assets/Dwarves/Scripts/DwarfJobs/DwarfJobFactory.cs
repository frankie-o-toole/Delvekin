public static class DwarfJobFactory
{
    private const float DefaultTunnelCycleDuration = 0.65f;

    private const float DefaultDiggerCycleDuration = 0.65f;

    private const float DefaultStairBuildInterval = 0.65f;

    private const float DefaultLadderBuildInterval = 0.65f;
    public static bool IsImplemented(
        DwarfJobType type)
    {
        return type ==
                   DwarfJobType.DirectionAlter ||
               type ==
                   DwarfJobType.Tunneller ||
               type ==
                   DwarfJobType.Digger ||
               type ==
                   DwarfJobType.StairBuilder ||
               type ==
                   DwarfJobType.LadderBuilder;
    }

    public static bool TryCreate(
        DwarfJobType type,
        DwarfAgent target,
        out IDwarfJob job,
        out string failureReason)
    {
        return TryCreate(
            type,
            target,
            DirectionAltererTurn.Reverse,
            out job,
            out failureReason);
    }

    public static bool TryCreate(
        DwarfJobType type,
        DwarfAgent target,
        DirectionAltererTurn directionAltererTurn,
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
                    job =
                        new DirectionAltererJob(
                            directionAltererTurn);

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

            case DwarfJobType.StairBuilder:
                {
                    DwarfJobTuning tuning =
                        target.GetComponent<DwarfJobTuning>();

                    float buildInterval =
                        tuning != null
                            ? tuning.StairBuildInterval
                            : DefaultStairBuildInterval;

                    job =
                        new StairBuilderJob(
                            buildInterval);

                    failureReason =
                        string.Empty;

                    return true;
                }

            case DwarfJobType.LadderBuilder:
                {
                    DwarfJobTuning tuning =
                        target.GetComponent<DwarfJobTuning>();

                    float buildInterval =
                        tuning != null
                            ? tuning.LadderBuildInterval
                            : DefaultLadderBuildInterval;

                    job =
                        new LadderBuilderJob(
                            buildInterval);

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
