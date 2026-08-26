using System;
using UnityEngine;

[RequireComponent(typeof(DwarfAgent))]
public class DwarfJobController : MonoBehaviour
{
    private DwarfAgent agent;
    private DwarfMovement movement;
    private VoxelWorld world;

    private DwarfJobContext context;

    private IDwarfJob pendingJob;
    private IDwarfJob activeJob;

    private DwarfJobInventory pendingInventory;

    public event Action<DwarfJobController> StateChanged;

    public DwarfJobStatus Status
    {
        get
        {
            if (activeJob != null)
            {
                return DwarfJobStatus.Active;
            }

            if (pendingJob != null)
            {
                return DwarfJobStatus.Pending;
            }

            return DwarfJobStatus.None;
        }
    }

    public bool HasPendingJob =>
        pendingJob != null;

    public bool HasActiveJob =>
        activeJob != null;

    public bool ControlsMovement =>
        activeJob != null &&
        activeJob.ControlsMovement;

    public DwarfJobType PendingJobType =>
        pendingJob?.Type ??
        DwarfJobType.None;

    public DwarfJobType ActiveJobType =>
        activeJob?.Type ??
        DwarfJobType.None;

    public string PendingActivationFailure
    {
        get;
        private set;
    }

    private void Awake()
    {
        agent =
            GetComponent<DwarfAgent>();

        movement =
            GetComponent<DwarfMovement>();

        world =
            FindFirstObjectByType<VoxelWorld>();

        context =
            new DwarfJobContext(
                agent,
                movement,
                world,
                this);
    }

    private void Update()
    {
        if (activeJob == null ||
            agent == null ||
            !agent.IsActive)
        {
            return;
        }

        activeJob.Tick(context);

        if (activeJob.IsComplete)
        {
            EndActiveJob(
                DwarfJobEndReason.Completed);
        }
    }

    private void OnDisable()
    {
        // Pending means the player never received the job's effect.
        CancelPendingJob(
            DwarfJobEndReason.DwarfDeactivated,
            refund: true);

        // Active jobs have already been used, so no refund occurs.
        EndActiveJob(
            DwarfJobEndReason.DwarfDeactivated);
    }

    public bool CanAssignJob(
    IDwarfJob job,
    out string failureReason)
    {
        if (job == null)
        {
            failureReason =
                "The assigned job is null.";

            return false;
        }

        if (agent == null ||
            !agent.IsActive)
        {
            failureReason =
                "The dwarf is not active.";

            return false;
        }

        if (activeJob != null)
        {
            failureReason =
                $"{agent.name} is already performing "
                + $"{activeJob.Type}.";

            return false;
        }

        return job.CanAssign(
            context,
            out failureReason);
    }

    public bool TryAssignJob(
        IDwarfJob job,
        DwarfJobInventory inventory,
        out string failureReason)
    {
        if (inventory == null)
        {
            failureReason =
                "No job inventory was provided.";

            return false;
        }

        if (!CanAssignJob(
                job,
                out failureReason))
        {
            return false;
        }

        if (!inventory.TryConsume(job.Type))
        {
            failureReason =
                $"No {job.Type} jobs remain.";

            return false;
        }

        // A replaced pending job never activated, so refund it.
        CancelPendingJob(
            DwarfJobEndReason.Replaced,
            refund: true);

        pendingJob = job;
        pendingInventory = inventory;
        PendingActivationFailure = string.Empty;

        failureReason = string.Empty;

        NotifyStateChanged();
        return true;
    }

    /// <summary>
    /// Attempts to start the pending job at the dwarf's current anchor.
    ///
    /// If this anchor is unsuitable, the job remains pending and will
    /// be tested again at the next stable anchor.
    /// </summary>
    public bool ActivatePendingJob()
    {
        if (pendingJob == null ||
            activeJob != null)
        {
            return false;
        }

        if (!pendingJob.CanActivate(
                context,
                out string failureReason))
        {
            PendingActivationFailure =
                failureReason;

            return false;
        }

        activeJob = pendingJob;

        pendingJob = null;
        pendingInventory = null;
        PendingActivationFailure = string.Empty;

        activeJob.Enter(context);

        NotifyStateChanged();

        if (activeJob != null &&
            activeJob.IsComplete)
        {
            EndActiveJob(
                DwarfJobEndReason.Completed);
        }

        return true;
    }

    public bool TryHandleMovementDecision()
    {
        if (activeJob is not
            IDwarfMovementDecisionJob movementDecisionJob)
        {
            return false;
        }

        return movementDecisionJob
            .TryHandleMovementDecision(
                context);
    }

    public bool TryCancelActiveJob(
        out string failureReason)
    {
        if (activeJob == null)
        {
            failureReason =
                "The dwarf has no active job.";

            return false;
        }

        if (!activeJob.CanBeCancelled)
        {
            failureReason =
                $"{activeJob.Type} cannot be cancelled.";

            return false;
        }

        EndActiveJob(
            DwarfJobEndReason.Cancelled);

        failureReason = string.Empty;
        return true;
    }

    public void CancelPendingJob()
    {
        CancelPendingJob(
            DwarfJobEndReason.Cancelled,
            refund: true);
    }

    private void CancelPendingJob(
        DwarfJobEndReason reason,
        bool refund)
    {
        if (pendingJob == null)
        {
            return;
        }

        IDwarfJob cancelledJob =
            pendingJob;

        DwarfJobInventory inventory =
            pendingInventory;

        pendingJob = null;
        pendingInventory = null;
        PendingActivationFailure = string.Empty;

        cancelledJob.Exit(
            context,
            reason);

        if (refund &&
            inventory != null)
        {
            inventory.Refund(
                cancelledJob.Type);
        }

        NotifyStateChanged();
    }

    private void EndActiveJob(
        DwarfJobEndReason reason)
    {
        if (activeJob == null)
        {
            return;
        }

        IDwarfJob finishedJob =
            activeJob;

        activeJob = null;

        finishedJob.Exit(
            context,
            reason);

        NotifyStateChanged();
    }

    private void NotifyStateChanged()
    {
        StateChanged?.Invoke(this);
    }
}