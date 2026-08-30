using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using TMPro;

public class DwarfJobBarUI : MonoBehaviour
{
    [Serializable]
    private class JobButtonBinding
    {
        public DwarfJobType jobType;
        public string displayName;
        public Button button;
        public TMP_Text countLabel;

        [NonSerialized]
        public UnityAction callback;
    }

    [SerializeField]
    private DwarfJobAssignmentManager assignmentManager;

    [SerializeField]
    private List<JobButtonBinding> jobButtons =
        new();

    [SerializeField]
    private TMP_Text feedbackLabel;

    [Header("Stop Job")]
    [SerializeField]
    private Button stopJobButton;

    [SerializeField]
    private TMP_Text stopJobLabel;

    [SerializeField]
    private string stopJobDisplayName =
        "Stop Job";

    [Header("Direction Alterer Options")]
    [SerializeField]
    private GameObject directionAltererOptionsPanel;

    [SerializeField]
    private Button directionAltererLeftButton;

    [SerializeField]
    private Button directionAltererReverseButton;

    [SerializeField]
    private Button directionAltererRightButton;

    [Header("Colours")]
    [SerializeField]
    private Color normalColour =
        new Color(0.25f, 0.25f, 0.25f, 1f);

    [SerializeField]
    private Color selectedColour =
        new Color(0.9f, 0.65f, 0.15f, 1f);

    [SerializeField]
    private Color unavailableColour =
        new Color(0.15f, 0.15f, 0.15f, 0.65f);

    private DwarfJobInventory inventory;
    private UnityAction stopJobCallback;
    private UnityAction directionAltererLeftCallback;
    private UnityAction directionAltererReverseCallback;
    private UnityAction directionAltererRightCallback;

    private void Awake()
    {
        ResolveInventory();
    }

    private void Start()
    {
        ResolveInventory();

        if (inventory != null)
        {
            // Prevent duplicate subscriptions.
            inventory.CountChanged -= HandleCountChanged;
            inventory.CountChanged += HandleCountChanged;
        }

        RefreshAllButtons();
    }

    private void ResolveInventory()
    {
        if (assignmentManager == null)
        {
            assignmentManager =
                FindFirstObjectByType<DwarfJobAssignmentManager>();
        }

        if (assignmentManager != null)
        {
            inventory = assignmentManager.Inventory;
        }

        if (inventory == null)
        {
            inventory =
                FindFirstObjectByType<DwarfJobInventory>();
        }
    }

    private void OnEnable()
    {
        if (assignmentManager == null)
            return;

        assignmentManager.SelectedJobChanged +=
            HandleSelectedJobChanged;

        assignmentManager.AssignmentSucceeded +=
            HandleAssignmentSucceeded;

        assignmentManager.AssignmentFailed +=
            HandleAssignmentFailed;

        assignmentManager.StopJobSelectionChanged +=
            HandleStopJobSelectionChanged;

        assignmentManager.JobStopped +=
            HandleJobStopped;

        assignmentManager.DirectionAltererSelectionChanged +=
            HandleDirectionAltererSelectionChanged;

        if (inventory != null)
        {
            inventory.CountChanged +=
                HandleCountChanged;
        }

        BindButtons();
        BindStopJobButton();
        BindDirectionAltererOptionButtons();
        RefreshAllButtons();
    }

    private void OnDisable()
    {
        if (assignmentManager != null)
        {
            assignmentManager.SelectedJobChanged -=
                HandleSelectedJobChanged;

            assignmentManager.AssignmentSucceeded -=
                HandleAssignmentSucceeded;

            assignmentManager.AssignmentFailed -=
                HandleAssignmentFailed;

            assignmentManager.StopJobSelectionChanged -=
                HandleStopJobSelectionChanged;

            assignmentManager.JobStopped -=
                HandleJobStopped;

            assignmentManager.DirectionAltererSelectionChanged -=
                HandleDirectionAltererSelectionChanged;
        }

        if (inventory != null)
        {
            inventory.CountChanged -=
                HandleCountChanged;
        }

        UnbindButtons();
        UnbindStopJobButton();
        UnbindDirectionAltererOptionButtons();
    }

    private void BindButtons()
    {
        foreach (JobButtonBinding binding in jobButtons)
        {
            if (binding.button == null)
                continue;

            DwarfJobType capturedType =
                binding.jobType;

            binding.callback =
                () => assignmentManager.ToggleJob(
                    capturedType);

            binding.button.onClick.AddListener(
                binding.callback);
        }
    }

    private void UnbindButtons()
    {
        foreach (JobButtonBinding binding in jobButtons)
        {
            if (binding.button == null ||
                binding.callback == null)
            {
                continue;
            }

            binding.button.onClick.RemoveListener(
                binding.callback);

            binding.callback = null;
        }
    }

    private void BindStopJobButton()
    {
        if (stopJobButton == null ||
            assignmentManager == null)
        {
            return;
        }

        stopJobCallback =
            assignmentManager.ToggleStopJob;

        stopJobButton.onClick.AddListener(
            stopJobCallback);
    }

    private void UnbindStopJobButton()
    {
        if (stopJobButton == null ||
            stopJobCallback == null)
        {
            return;
        }

        stopJobButton.onClick.RemoveListener(
            stopJobCallback);

        stopJobCallback = null;
    }

    private void BindDirectionAltererOptionButtons()
    {
        if (assignmentManager == null)
        {
            return;
        }

        directionAltererLeftCallback =
            () => assignmentManager.SelectDirectionAltererTurn(
                DirectionAltererTurn.Left);

        directionAltererReverseCallback =
            () => assignmentManager.SelectDirectionAltererTurn(
                DirectionAltererTurn.Reverse);

        directionAltererRightCallback =
            () => assignmentManager.SelectDirectionAltererTurn(
                DirectionAltererTurn.Right);

        directionAltererLeftButton?.onClick.AddListener(
            directionAltererLeftCallback);

        directionAltererReverseButton?.onClick.AddListener(
            directionAltererReverseCallback);

        directionAltererRightButton?.onClick.AddListener(
            directionAltererRightCallback);
    }

    private void UnbindDirectionAltererOptionButtons()
    {
        if (directionAltererLeftButton != null &&
            directionAltererLeftCallback != null)
        {
            directionAltererLeftButton.onClick.RemoveListener(
                directionAltererLeftCallback);
        }

        if (directionAltererReverseButton != null &&
            directionAltererReverseCallback != null)
        {
            directionAltererReverseButton.onClick.RemoveListener(
                directionAltererReverseCallback);
        }

        if (directionAltererRightButton != null &&
            directionAltererRightCallback != null)
        {
            directionAltererRightButton.onClick.RemoveListener(
                directionAltererRightCallback);
        }

        directionAltererLeftCallback = null;
        directionAltererReverseCallback = null;
        directionAltererRightCallback = null;
    }

    private void RefreshAllButtons()
    {
        foreach (JobButtonBinding binding in jobButtons)
        {
            RefreshButton(binding);
        }

        RefreshStopJobButton();
        RefreshDirectionAltererOptions();
    }

    private void RefreshStopJobButton()
    {
        if (stopJobButton == null)
        {
            return;
        }

        stopJobButton.interactable = true;

        if (stopJobButton.targetGraphic != null)
        {
            stopJobButton.targetGraphic.color =
                assignmentManager != null &&
                assignmentManager.IsStopJobSelected
                    ? selectedColour
                    : normalColour;
        }

        if (stopJobLabel != null)
        {
            stopJobLabel.text =
                stopJobDisplayName;
        }
    }

    private void RefreshDirectionAltererOptions()
    {
        bool optionsOpen =
            assignmentManager != null &&
            assignmentManager.AreDirectionAltererOptionsOpen;

        if (directionAltererOptionsPanel != null)
        {
            directionAltererOptionsPanel.SetActive(
                optionsOpen);
        }

        DirectionAltererTurn? selectedTurn =
            assignmentManager?.SelectedDirectionAltererTurn;

        SetDirectionOptionColour(
            directionAltererLeftButton,
            selectedTurn == DirectionAltererTurn.Left);

        SetDirectionOptionColour(
            directionAltererReverseButton,
            selectedTurn == DirectionAltererTurn.Reverse);

        SetDirectionOptionColour(
            directionAltererRightButton,
            selectedTurn == DirectionAltererTurn.Right);
    }

    private void SetDirectionOptionColour(
        Button button,
        bool selected)
    {
        if (button?.targetGraphic == null)
        {
            return;
        }

        button.targetGraphic.color =
            selected
                ? selectedColour
                : normalColour;
    }

    private void RefreshButton(
        JobButtonBinding binding)
    {
        if (binding.button == null ||
            inventory == null)
        {
            return;
        }

        int count =
            inventory.GetCount(
                binding.jobType);

        bool implemented =
            DwarfJobFactory.IsImplemented(
                binding.jobType);

        bool available =
            implemented &&
            count > 0;

        bool selected =
            assignmentManager.SelectedJob ==
                binding.jobType ||
            binding.jobType == DwarfJobType.DirectionAlter &&
            assignmentManager.AreDirectionAltererOptionsOpen;

        binding.button.interactable =
            available;

        if (binding.button.targetGraphic != null)
        {
            binding.button.targetGraphic.color =
                !available
                    ? unavailableColour
                    : selected
                        ? selectedColour
                        : normalColour;
        }

        if (binding.countLabel != null)
        {
            string displayName =
                string.IsNullOrWhiteSpace(
                    binding.displayName)
                    ? binding.jobType.ToString()
                    : binding.displayName;

            binding.countLabel.text =
                $"{displayName} ({count})";
        }
    }

    private void HandleSelectedJobChanged(
        DwarfJobType jobType)
    {
        RefreshAllButtons();
    }

    private void HandleCountChanged(
        DwarfJobType jobType,
        int count)
    {
        RefreshAllButtons();
    }

    private void HandleAssignmentSucceeded(
        DwarfAgent dwarf,
        DwarfJobType jobType)
    {
        if (feedbackLabel != null)
        {
            feedbackLabel.text =
                $"Assigned {jobType} to {dwarf.name}";
        }
    }

    private void HandleAssignmentFailed(
        string failureReason)
    {
        if (feedbackLabel != null)
        {
            feedbackLabel.text =
                failureReason;
        }
    }

    private void HandleStopJobSelectionChanged(
        bool selected)
    {
        RefreshAllButtons();
    }

    private void HandleJobStopped(
        DwarfAgent dwarf,
        DwarfJobType jobType)
    {
        if (feedbackLabel != null)
        {
            feedbackLabel.text =
                $"Stopped {jobType} on {dwarf.name}";
        }
    }

    private void HandleDirectionAltererSelectionChanged(
        bool optionsOpen,
        DirectionAltererTurn? selectedTurn)
    {
        RefreshAllButtons();
    }
}
