using System;
using UnityEngine;

public class DwarfJobAssignmentManager : MonoBehaviour
{
    [SerializeField]
    private DwarfJobInventory inventory;

    private DwarfAgent selectedDwarf;

    private DwarfJobType selectedJob =
        DwarfJobType.None;

    private bool stopJobSelected;

    private bool directionAltererOptionsOpen;
    private DirectionAltererTurn selectedDirectionAltererTurn =
        DirectionAltererTurn.Reverse;

    public event Action<DwarfAgent> SelectedDwarfChanged;
    public event Action<DwarfJobType> SelectedJobChanged;
    public event Action<bool> StopJobSelectionChanged;
    public event Action<
        bool,
        DirectionAltererTurn?> DirectionAltererSelectionChanged;

    public event Action<
        DwarfAgent,
        DwarfJobType> AssignmentSucceeded;

    public event Action<string> AssignmentFailed;

    public event Action<
        DwarfAgent,
        DwarfJobType> JobStopped;

    public DwarfAgent SelectedDwarf =>
        selectedDwarf;

    public DwarfJobType SelectedJob =>
        selectedJob;

    public DwarfJobInventory Inventory
    {
        get
        {
            if (inventory == null)
            {
                inventory =
                    GetComponent<DwarfJobInventory>();
            }

            return inventory;
        }
    }

    public bool HasSelectedJob =>
        selectedJob != DwarfJobType.None;

    public bool IsStopJobSelected =>
        stopJobSelected;

    public bool HasSelectedAction =>
        HasSelectedJob ||
        stopJobSelected;

    public bool AreDirectionAltererOptionsOpen =>
        directionAltererOptionsOpen;

    public DirectionAltererTurn? SelectedDirectionAltererTurn =>
        selectedJob == DwarfJobType.DirectionAlter
            ? selectedDirectionAltererTurn
            : null;

    private void Awake()
    {
        if (inventory == null)
        {
            inventory =
                GetComponent<DwarfJobInventory>();
        }
    }

    public void ToggleJob(
        DwarfJobType jobType)
    {
        if (jobType == DwarfJobType.DirectionAlter)
        {
            ToggleDirectionAltererOptions();
            return;
        }

        ClearStopJobSelection();
        ClearDirectionAltererSelection();

        if (jobType == DwarfJobType.None)
        {
            ClearSelectedJob();
            return;
        }

        if (selectedJob == jobType)
        {
            ClearSelectedJob();
            return;
        }

        if (!DwarfJobFactory.IsImplemented(jobType))
        {
            ReportFailure(
                $"{jobType} has not been implemented yet.");

            return;
        }

        if (inventory == null ||
            !inventory.HasAvailable(jobType))
        {
            ReportFailure(
                $"No {jobType} jobs remain.");

            return;
        }

        selectedJob = jobType;

        SelectedJobChanged?.Invoke(
            selectedJob);

        TryResolveSelection();
    }

    public void ToggleDirectionAltererOptions()
    {
        if (directionAltererOptionsOpen ||
            selectedJob == DwarfJobType.DirectionAlter)
        {
            ClearSelectedJob();
            ClearDirectionAltererSelection();
            return;
        }

        if (!DwarfJobFactory.IsImplemented(
                DwarfJobType.DirectionAlter))
        {
            ReportFailure(
                "DirectionAlter has not been implemented yet.");

            return;
        }

        if (inventory == null ||
            !inventory.HasAvailable(
                DwarfJobType.DirectionAlter))
        {
            ReportFailure(
                "No DirectionAlter jobs remain.");

            return;
        }

        ClearStopJobSelection();
        ClearSelectedJob();

        directionAltererOptionsOpen = true;

        DirectionAltererSelectionChanged?.Invoke(
            true,
            null);
    }

    public void SelectDirectionAltererTurn(
        DirectionAltererTurn turn)
    {
        if (!directionAltererOptionsOpen)
        {
            return;
        }

        selectedDirectionAltererTurn = turn;
        selectedJob = DwarfJobType.DirectionAlter;

        DirectionAltererSelectionChanged?.Invoke(
            true,
            selectedDirectionAltererTurn);

        SelectedJobChanged?.Invoke(
            selectedJob);

        TryResolveSelection();
    }

    public void ToggleStopJob()
    {
        if (stopJobSelected)
        {
            ClearStopJobSelection();
            return;
        }

        ClearSelectedJob();
        ClearDirectionAltererSelection();

        stopJobSelected = true;

        StopJobSelectionChanged?.Invoke(true);

        TryResolveSelection();
    }

    public void ToggleDwarf(
        DwarfAgent dwarf)
    {
        if (dwarf == null)
        {
            ClearSelectedDwarf();
            return;
        }

        if (selectedDwarf == dwarf)
        {
            ClearSelectedDwarf();
            return;
        }

        selectedDwarf = dwarf;

        SelectedDwarfChanged?.Invoke(
            selectedDwarf);

        TryResolveSelection();
    }

    public void ClearSelectedDwarf()
    {
        if (selectedDwarf == null)
        {
            return;
        }

        selectedDwarf = null;

        SelectedDwarfChanged?.Invoke(null);
    }

    public void ClearSelectedJob()
    {
        if (selectedJob ==
            DwarfJobType.None)
        {
            return;
        }

        selectedJob =
            DwarfJobType.None;

        SelectedJobChanged?.Invoke(
            selectedJob);
    }

    public void ClearDirectionAltererSelection()
    {
        bool hadDirectionAltererState =
            directionAltererOptionsOpen ||
            selectedJob == DwarfJobType.DirectionAlter;

        directionAltererOptionsOpen = false;

        if (hadDirectionAltererState)
        {
            DirectionAltererSelectionChanged?.Invoke(
                false,
                null);
        }
    }

    public void ClearStopJobSelection()
    {
        if (!stopJobSelected)
        {
            return;
        }

        stopJobSelected = false;

        StopJobSelectionChanged?.Invoke(false);
    }

    public void ClearAllSelections()
    {
        ClearSelectedDwarf();
        ClearSelectedJob();
        ClearDirectionAltererSelection();
        ClearStopJobSelection();
    }

    public bool CanStopJob(
        DwarfAgent dwarf,
        out string failureReason)
    {
        if (dwarf == null ||
            !dwarf.IsActive)
        {
            failureReason =
                "The dwarf is not active.";

            return false;
        }

        DwarfJobController controller =
            dwarf.GetComponent<DwarfJobController>();

        if (controller == null)
        {
            failureReason =
                $"{dwarf.name} has no DwarfJobController.";

            return false;
        }

        return controller.CanStopCurrentJob(
            out failureReason);
    }

    public bool CanAssignSelectedJob(
        DwarfAgent dwarf,
        out string failureReason)
    {
        if (!HasSelectedJob)
        {
            failureReason =
                "No job is selected.";

            return false;
        }

        if (dwarf == null ||
            !dwarf.IsActive)
        {
            failureReason =
                "The dwarf is not active.";

            return false;
        }

        if (inventory == null ||
            !inventory.HasAvailable(
                selectedJob))
        {
            failureReason =
                $"No {selectedJob} jobs remain.";

            return false;
        }

        if (!DwarfJobFactory.TryCreate(
                selectedJob,
                dwarf,
                selectedDirectionAltererTurn,
                out IDwarfJob job,
                out failureReason))
        {
            return false;
        }

        DwarfJobController controller =
            dwarf.GetComponent<DwarfJobController>();

        if (controller == null)
        {
            failureReason =
                $"{dwarf.name} has no "
                + "DwarfJobController.";

            return false;
        }

        return controller.CanAssignJob(
            job,
            out failureReason);
    }

    private void TryResolveSelection()
    {
        if (selectedDwarf == null)
        {
            return;
        }

        if (stopJobSelected)
        {
            TryStopSelectedDwarfJob();
            return;
        }

        if (selectedJob == DwarfJobType.None)
        {
            return;
        }

        DwarfAgent target =
            selectedDwarf;

        DwarfJobType jobType =
            selectedJob;

        if (!CanAssignSelectedJob(
                target,
                out string failureReason))
        {
            ReportFailure(failureReason);
            ClearSelectedDwarf();
            return;
        }

        if (!DwarfJobFactory.TryCreate(
                jobType,
                target,
                selectedDirectionAltererTurn,
                out IDwarfJob job,
                out failureReason))
        {
            ReportFailure(failureReason);
            ClearSelectedDwarf();
            return;
        }

        DwarfJobController controller =
            target.GetComponent<DwarfJobController>();

        bool assigned =
            controller.TryAssignJob(
                job,
                inventory,
                out failureReason);

        if (!assigned)
        {
            ReportFailure(failureReason);
            ClearSelectedDwarf();
            return;
        }

        AssignmentSucceeded?.Invoke(
            target,
            jobType);

        // A successful assignment completes the interaction.
        ClearSelectedDwarf();
        ClearSelectedJob();
        ClearDirectionAltererSelection();
    }

    private void TryStopSelectedDwarfJob()
    {
        DwarfAgent target =
            selectedDwarf;

        if (!CanStopJob(
                target,
                out string failureReason))
        {
            ReportFailure(failureReason);
            ClearSelectedDwarf();
            return;
        }

        DwarfJobController controller =
            target.GetComponent<DwarfJobController>();

        if (!controller.TryStopCurrentJob(
                out DwarfJobType stoppedJobType,
                out failureReason))
        {
            ReportFailure(failureReason);
            ClearSelectedDwarf();
            return;
        }

        JobStopped?.Invoke(
            target,
            stoppedJobType);

        ClearSelectedDwarf();
        ClearStopJobSelection();
    }

    private void ReportFailure(
        string failureReason)
    {
        Debug.LogWarning(
            $"Job assignment failed: "
            + failureReason);

        AssignmentFailed?.Invoke(
            failureReason);
    }
}
