public enum DwarfJobType
{
    None,
    DirectionAlter,
    Tunneller,
    Digger,
    StairBuilder,
    LadderBuilder
}

public enum DwarfJobStatus
{
    None,
    Pending,
    Active
}

public enum DwarfJobEndReason
{
    Completed,
    Cancelled,
    Replaced,
    DwarfDeactivated
}