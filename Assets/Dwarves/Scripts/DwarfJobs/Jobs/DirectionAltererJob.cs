using UnityEngine;

/// <summary>
/// Turns the assigned dwarf into a stationary direction alterer.
///
/// Approaching dwarves are redirected before their proposed occupied
/// volume overlaps this dwarf.
/// </summary>
public class DirectionAltererJob : IDwarfJob
{
    private DwarfJobContext context;
    private bool registered;
    private bool warnedAboutInvalidOutput;
    private PuzzleSide approachDirection;
    private bool hasApproachDirection;

    private readonly DirectionAltererTurn selectedTurn;

    public DwarfJobType Type =>
        DwarfJobType.DirectionAlter;

    public PuzzleSide OutputDirection
    {
        get;
        private set;
    }

    public DirectionAltererTurn SelectedTurn =>
        selectedTurn;

    public DwarfAgent Agent =>
        context?.Agent;

    public bool IsOperational =>
        registered &&
        Agent != null &&
        Agent.IsActive;

    public bool IsComplete =>
        false;

    public bool ControlsMovement =>
        true;

    public bool CanBeCancelled =>
        true;

    public DirectionAltererJob(
        DirectionAltererTurn selectedTurn)
    {
        this.selectedTurn =
            selectedTurn;
    }

    public bool CanAssign(
    DwarfJobContext jobContext,
    out string failureReason)
    {
        if (jobContext == null ||
            jobContext.Agent == null ||
            !jobContext.Agent.IsActive)
        {
            failureReason =
                "The dwarf is not active.";

            return false;
        }

        if (jobContext.Movement == null)
        {
            failureReason =
                "The dwarf has no movement component.";

            return false;
        }

        if (jobContext.Movement.State ==
            DwarfMovement.MovementState.Falling)
        {
            failureReason =
                "A falling dwarf cannot become "
                + "a Direction Alterer.";

            return false;
        }

        failureReason = string.Empty;
        return true;
    }

    public bool CanActivate(
        DwarfJobContext jobContext,
        out string failureReason)
    {
        if (jobContext == null ||
            jobContext.Agent == null ||
            !jobContext.Agent.IsActive)
        {
            failureReason =
                "The dwarf is not active.";

            return false;
        }

        if (jobContext.World == null)
        {
            failureReason =
                "The voxel world is unavailable.";

            return false;
        }

        if (DwarfWorldQueries.HasNoSupport(
                jobContext.World,
                jobContext.Agent.CurrentVoxel))
        {
            failureReason =
                "The dwarf is not standing on supported terrain.";

            return false;
        }

        if (!DwarfWorldQueries.CanOccupy(
                jobContext.World,
                jobContext.Agent.CurrentVoxel))
        {
            failureReason =
                "The dwarf does not have sufficient clearance.";

            return false;
        }

        failureReason = string.Empty;
        return true;
    }

    public void Enter(
        DwarfJobContext jobContext)
    {
        context = jobContext;

        approachDirection =
            context.Agent.Facing;

        hasApproachDirection = true;

        OutputDirection =
            DirectionUtility.ApplyTurn(
                approachDirection,
                selectedTurn);

        context.Agent.SetFacing(
            OutputDirection);

        registered = true;

        DirectionAltererRegistry.Register(this);

        Debug.Log(
            $"{context.Agent.name} became a Direction Alterer "
            + $"pointing {OutputDirection}.");
    }

    public void Tick(
        DwarfJobContext jobContext)
    {
        // Persistent stationary job.
        // DirectionAltererRegistry handles approaching dwarves.
    }

    public void Exit(
        DwarfJobContext jobContext,
        DwarfJobEndReason reason)
    {
        registered = false;

        DirectionAltererRegistry.Unregister(this);

        if (reason == DwarfJobEndReason.Cancelled &&
            hasApproachDirection &&
            jobContext?.Agent != null &&
            jobContext.Agent.IsActive)
        {
            jobContext.Agent.SetFacing(
                approachDirection);
        }

        context = null;
    }

    public void ReportInvalidOutputOnce(
        PuzzleSide requested,
        PuzzleSide fallback)
    {
        if (warnedAboutInvalidOutput)
        {
            return;
        }

        warnedAboutInvalidOutput = true;

        Debug.LogWarning(
            $"Direction Alterer output {requested} would direct "
            + $"a dwarf through the alterer. Using {fallback} "
            + "as a temporary safe fallback.");
    }
}
