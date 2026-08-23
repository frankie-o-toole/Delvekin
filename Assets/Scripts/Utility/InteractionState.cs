public static class InteractionState
{
    public static DwarfAgent HoveredDwarf
    {
        get;
        private set;
    }

    public static bool IsHoveringDwarf =>
        HoveredDwarf != null;

    public static void SetHoveredDwarf(
        DwarfAgent dwarf)
    {
        HoveredDwarf = dwarf;
    }

    public static void ClearHoveredDwarf(
        DwarfAgent dwarf = null)
    {
        if (dwarf != null &&
            HoveredDwarf != dwarf)
        {
            return;
        }

        HoveredDwarf = null;
    }
}