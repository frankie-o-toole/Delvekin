public interface IDwarfJob
{
    DwarfJobType Type { get; }

    bool IsComplete { get; }

    bool ControlsMovement { get; }

    bool CanBeCancelled { get; }

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