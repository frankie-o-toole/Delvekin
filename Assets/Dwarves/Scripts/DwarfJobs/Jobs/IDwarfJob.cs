public interface IDwarfJob
{
    DwarfJobType Type { get; }

    bool IsComplete { get; }

    bool ControlsMovement { get; }

    bool CanBeCancelled { get; }

    /// <summary>
    /// Stationary service jobs remove their dwarf from the run when stopped.
    /// Mobile jobs simply hand control back to ordinary movement.
    /// </summary>
    bool RecallOnCancel { get; }

    bool CanAssign(
        DwarfJobContext context,
        out string failureReason);

    bool CanActivate(
        DwarfJobContext context,
        out string failureReason);

    void Enter(
        DwarfJobContext context);

    void Tick(
        DwarfJobContext context);

    void Exit(
        DwarfJobContext context,
        DwarfJobEndReason reason);
}
