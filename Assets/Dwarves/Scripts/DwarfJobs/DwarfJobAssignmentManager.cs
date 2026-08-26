using System;
using UnityEngine;

public class DwarfJobAssignmentManager : MonoBehaviour
{
    [SerializeField]
    private DwarfJobInventory inventory;

    private DwarfAgent selectedDwarf;

    private DwarfJobType selectedJob =
        DwarfJobType.None;

    public event Action<DwarfAgent> SelectedDwarfChanged;
    public event Action<DwarfJobType> SelectedJobChanged;

    public event Action<
        DwarfAgent,
        DwarfJobType> AssignmentSucceeded;

    public event Action<string> AssignmentFailed;

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

    public void ClearAllSelections()
    {
        ClearSelectedDwarf();
        ClearSelectedJob();
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
        if (selectedDwarf == null ||
            selectedJob == DwarfJobType.None)
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