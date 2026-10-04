using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Serialization;

public class DwarfJobBarUI : MonoBehaviour
{
    public enum JobSlotState
    {
        Locked,
        UnavailableInLevel,
        Exhausted,
        Available
    }

    [Serializable]
    private class JobButtonBinding
    {
        public DwarfJobType jobType;
        public string displayName;
        public Sprite icon;
        public Button button;
        public TMP_Text countLabel;
        public JobSlotUI slotView;

        [NonSerialized]
        public UnityAction callback;
    }

    [SerializeField]
    private DwarfJobAssignmentManager assignmentManager;

    [SerializeField]
    private VoxelWorld voxelWorld;

    [Header("Editor-authored Job Slots")]
    [FormerlySerializedAs("jobButtons")]
    [SerializeField]
    private List<JobButtonBinding> jobButtonDefinitions =
        new();

    [Header("Stop Job")]
    [SerializeField]
    private Button stopJobButton;

    [SerializeField]
    private JobSlotUI stopJobSlotView;

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
    private CanvasGroup canvasGroup;
    private readonly List<JobButtonBinding> runtimeJobButtons =
        new();

    private void Awake()
    {
        Transform legacyFeedback = transform.Find("FeedbackLabel");
        if (legacyFeedback != null)
        {
            legacyFeedback.gameObject.SetActive(false);
        }

        ResolveInventory();
        ResolveLoadingReferences();
    }

    private void Start()
    {
        ResolveInventory();
        SubscribeToInventory();
        RebuildJobButtons();
        RefreshAllButtons();
        RefreshLoadingVisibility();
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

    private void SubscribeToInventory()
    {
        if (inventory == null)
        {
            return;
        }

        inventory.CountChanged -= HandleCountChanged;
        inventory.CountChanged += HandleCountChanged;

        inventory.ConfigurationChanged -=
            HandleInventoryConfigurationChanged;
        inventory.ConfigurationChanged +=
            HandleInventoryConfigurationChanged;
    }

    private void OnEnable()
    {
        ResolveLoadingReferences();

        if (voxelWorld != null)
        {
            voxelWorld.LoadingProgressChanged +=
                HandleLoadingProgressChanged;
        }

        if (assignmentManager == null)
            return;

        assignmentManager.SelectedJobChanged +=
            HandleSelectedJobChanged;

        assignmentManager.StopJobSelectionChanged +=
            HandleStopJobSelectionChanged;

        assignmentManager.DirectionAltererSelectionChanged +=
            HandleDirectionAltererSelectionChanged;

        assignmentManager.InteractionEnabledChanged +=
            HandleInteractionEnabledChanged;

        if (inventory != null)
        {
            SubscribeToInventory();
        }

        if (CampaignProgressService.Instance != null)
        {
            CampaignProgressService.Instance.CampaignChanged +=
                HandleCampaignChanged;
        }

        RebuildJobButtons();
        BindStopJobButton();
        BindDirectionAltererOptionButtons();
        RefreshAllButtons();
    }

    private void OnDisable()
    {
        if (voxelWorld != null)
        {
            voxelWorld.LoadingProgressChanged -=
                HandleLoadingProgressChanged;
        }

        if (assignmentManager != null)
        {
            assignmentManager.SelectedJobChanged -=
                HandleSelectedJobChanged;

            assignmentManager.StopJobSelectionChanged -=
                HandleStopJobSelectionChanged;

            assignmentManager.DirectionAltererSelectionChanged -=
                HandleDirectionAltererSelectionChanged;

            assignmentManager.InteractionEnabledChanged -=
                HandleInteractionEnabledChanged;
        }

        if (inventory != null)
        {
            inventory.CountChanged -=
                HandleCountChanged;

            inventory.ConfigurationChanged -=
                HandleInventoryConfigurationChanged;
        }

        if (CampaignProgressService.Instance != null)
        {
            CampaignProgressService.Instance.CampaignChanged -=
                HandleCampaignChanged;
        }

        UnbindButtons();
        UnbindStopJobButton();
        UnbindDirectionAltererOptionButtons();
    }

    private void ResolveLoadingReferences()
    {
        if (voxelWorld == null)
        {
            voxelWorld = FindFirstObjectByType<VoxelWorld>();
        }

        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();

            if (canvasGroup == null)
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
        }
    }

    private void HandleLoadingProgressChanged(
        LevelLoadingProgress progress)
    {
        RefreshLoadingVisibility();
    }

    private void RefreshLoadingVisibility()
    {
        if (canvasGroup == null)
        {
            return;
        }

        bool visible = voxelWorld == null || voxelWorld.IsLevelReady;
        canvasGroup.alpha = visible ? 1f : 0f;
        canvasGroup.interactable = visible;
        canvasGroup.blocksRaycasts = visible;
    }

    public void RebuildJobButtons()
    {
        UnbindButtons();
        runtimeJobButtons.Clear();

        foreach (JobButtonBinding definition in jobButtonDefinitions)
        {
            if (definition == null || definition.button == null)
            {
                continue;
            }

            if (definition.slotView == null)
            {
                definition.slotView =
                    definition.button.GetComponent<JobSlotUI>();
            }

            runtimeJobButtons.Add(definition);
        }

        BindButtons();
    }

    private void BindButtons()
    {
        foreach (JobButtonBinding binding in runtimeJobButtons)
        {
            if (binding.button == null ||
                binding.jobType == DwarfJobType.None ||
                assignmentManager == null)
            {
                continue;
            }

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
        foreach (JobButtonBinding binding in runtimeJobButtons)
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

        BindDirectionPreview(
            directionAltererLeftButton,
            DirectionAltererTurn.Left);

        BindDirectionPreview(
            directionAltererReverseButton,
            DirectionAltererTurn.Reverse);

        BindDirectionPreview(
            directionAltererRightButton,
            DirectionAltererTurn.Right);
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

        UnbindDirectionPreview(directionAltererLeftButton);
        UnbindDirectionPreview(directionAltererReverseButton);
        UnbindDirectionPreview(directionAltererRightButton);
    }

    private void BindDirectionPreview(
        Button button,
        DirectionAltererTurn turn)
    {
        if (button == null ||
            assignmentManager == null)
        {
            return;
        }

        DirectionAltererOptionHover hover =
            button.GetComponent<DirectionAltererOptionHover>();

        if (hover == null)
        {
            hover =
                button.gameObject.AddComponent<
                    DirectionAltererOptionHover>();
        }

        hover.Initialize(
            assignmentManager,
            turn);
    }

    private static void UnbindDirectionPreview(
        Button button)
    {
        button
            ?.GetComponent<DirectionAltererOptionHover>()
            ?.Clear();
    }

    private void RefreshAllButtons()
    {
        foreach (JobButtonBinding binding in runtimeJobButtons)
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

        bool interactionEnabled =
            assignmentManager != null &&
            assignmentManager.InteractionEnabled;
        bool selected =
            assignmentManager != null &&
            assignmentManager.IsStopJobSelected;

        stopJobSlotView ??=
            stopJobButton.GetComponent<JobSlotUI>();

        if (stopJobSlotView != null)
        {
            stopJobSlotView.ApplyStopState(
                stopJobDisplayName,
                interactionEnabled,
                selected);
        }
        else
        {
            stopJobButton.interactable = interactionEnabled;

            if (stopJobButton.targetGraphic != null)
            {
                stopJobButton.targetGraphic.color =
                    selected ? selectedColour : normalColour;
            }
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

        bool unlocked =
            implemented &&
            CampaignProgressService.Instance != null &&
            CampaignProgressService.Instance.IsJobUnlocked(
                binding.jobType);

        bool offeredInLevel =
            unlocked &&
            IsOfferedInLevel(binding.jobType);

        JobSlotState state = ResolveJobSlotState(
            implemented,
            unlocked,
            offeredInLevel,
            count);

        bool available =
            assignmentManager != null &&
            assignmentManager.InteractionEnabled &&
            state == JobSlotState.Available;

        bool selected =
            assignmentManager != null &&
            (assignmentManager.SelectedJob ==
                binding.jobType ||
            binding.jobType == DwarfJobType.DirectionAlter &&
            assignmentManager.AreDirectionAltererOptionsOpen);

        string displayName =
            string.IsNullOrWhiteSpace(binding.displayName)
                ? binding.jobType.ToString()
                : binding.displayName;

        if (binding.slotView != null)
        {
            binding.slotView.ApplyJobState(
                displayName,
                binding.icon,
                state,
                count,
                available,
                selected);
            return;
        }

        binding.button.interactable = available;

        if (binding.button.targetGraphic != null)
        {
            binding.button.targetGraphic.color = !available
                ? unavailableColour
                : selected
                    ? selectedColour
                    : normalColour;
        }

        if (binding.countLabel != null)
        {
            binding.countLabel.text = state switch
            {
                JobSlotState.Locked => "?",
                JobSlotState.UnavailableInLevel =>
                    $"{displayName}\n—",
                JobSlotState.Exhausted =>
                    $"{displayName}\n0",
                _ => $"{displayName}\n{count}"
            };
        }
    }

    private bool IsOfferedInLevel(
        DwarfJobType jobType)
    {
        foreach (EffectiveJobAvailability job in inventory.EffectiveJobs)
        {
            if (job.JobType == jobType)
            {
                return true;
            }
        }

        return false;
    }

    public static JobSlotState ResolveJobSlotState(
        bool implemented,
        bool unlocked,
        bool offeredInLevel,
        int count)
    {
        if (!implemented || !unlocked)
        {
            return JobSlotState.Locked;
        }

        if (!offeredInLevel)
        {
            return JobSlotState.UnavailableInLevel;
        }

        return count > 0
            ? JobSlotState.Available
            : JobSlotState.Exhausted;
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

    private void HandleInventoryConfigurationChanged()
    {
        RebuildJobButtons();
        RefreshAllButtons();
    }

    private void HandleCampaignChanged()
    {
        RefreshAllButtons();
    }

    private void HandleStopJobSelectionChanged(
        bool selected)
    {
        RefreshAllButtons();
    }

    private void HandleDirectionAltererSelectionChanged(
        bool optionsOpen,
        DirectionAltererTurn? selectedTurn)
    {
        RefreshAllButtons();
    }

    private void HandleInteractionEnabledChanged(bool enabled)
    {
        RefreshAllButtons();
    }
}
