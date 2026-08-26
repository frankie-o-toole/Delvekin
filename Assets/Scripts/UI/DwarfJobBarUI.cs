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

        if (inventory != null)
        {
            inventory.CountChanged +=
                HandleCountChanged;
        }

        BindButtons();
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
        }

        if (inventory != null)
        {
            inventory.CountChanged -=
                HandleCountChanged;
        }

        UnbindButtons();
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

    private void RefreshAllButtons()
    {
        foreach (JobButtonBinding binding in jobButtons)
        {
            RefreshButton(binding);
        }
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
            binding.jobType;

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
}