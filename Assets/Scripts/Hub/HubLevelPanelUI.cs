using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[AddComponentMenu("Delvekin/Hub/Hub Level Panel UI")]
public sealed class HubLevelPanelUI : MonoBehaviour
{
    [Header("Source")]
    [SerializeField]
    private HubCampaignController campaignController;

    [Header("Visibility")]
    [Tooltip(
        "Controls visibility without disabling this component, so a hidden " +
        "panel can still receive the next level-selection event.")]
    [SerializeField]
    private CanvasGroup panelGroup;

    [SerializeField]
    private bool startHidden = true;

    [Header("Level Information")]
    [SerializeField]
    private TMP_Text levelNameLabel;

    [SerializeField]
    private TMP_Text statusLabel;

    [SerializeField]
    private TMP_Text objectiveLabel;

    [SerializeField]
    private TMP_Text rewardLabel;

    [SerializeField]
    private TMP_Text bestResultLabel;

    [SerializeField]
    private TMP_Text lockedReasonLabel;

    [Header("Actions")]
    [SerializeField]
    private Button enterMineButton;

    [SerializeField]
    private Button returnButton;

    public bool IsVisible =>
        panelGroup != null && panelGroup.alpha > 0.001f;
    public HubLevelPresentation CurrentPresentation { get; private set; }

    private void Awake()
    {
        if (panelGroup == null)
        {
            panelGroup = GetComponent<CanvasGroup>();
        }

        if (panelGroup == null)
        {
            panelGroup = gameObject.AddComponent<CanvasGroup>();
        }

        if (campaignController == null)
        {
            campaignController = FindFirstObjectByType<HubCampaignController>(
                FindObjectsInactive.Include);
        }
    }

    private void OnEnable()
    {
        BindController();
        BindButtons();

        if (startHidden || CurrentPresentation == null)
        {
            SetVisible(false);
        }
    }

    private void OnDisable()
    {
        UnbindController();
        UnbindButtons();
    }

    public void Show(HubLevelPresentation presentation)
    {
        if (presentation == null)
        {
            Hide();
            return;
        }

        CurrentPresentation = presentation;
        RefreshLabels(presentation);

        if (enterMineButton != null)
        {
            enterMineButton.interactable = presentation.IsLaunchable;
        }

        SetVisible(true);
    }

    public void Hide()
    {
        CurrentPresentation = null;
        SetVisible(false);
    }

    private void BindController()
    {
        if (campaignController == null)
        {
            return;
        }

        campaignController.LevelPresentationSelected -=
            HandleLevelSelected;
        campaignController.LevelPresentationSelected +=
            HandleLevelSelected;
        campaignController.LevelSelectionCleared -=
            HandleSelectionCleared;
        campaignController.LevelSelectionCleared +=
            HandleSelectionCleared;
    }

    private void UnbindController()
    {
        if (campaignController == null)
        {
            return;
        }

        campaignController.LevelPresentationSelected -=
            HandleLevelSelected;
        campaignController.LevelSelectionCleared -=
            HandleSelectionCleared;
    }

    private void BindButtons()
    {
        if (enterMineButton != null)
        {
            enterMineButton.onClick.RemoveListener(HandleEnterMine);
            enterMineButton.onClick.AddListener(HandleEnterMine);
        }

        if (returnButton != null)
        {
            returnButton.onClick.RemoveListener(HandleReturn);
            returnButton.onClick.AddListener(HandleReturn);
        }
    }

    private void UnbindButtons()
    {
        enterMineButton?.onClick.RemoveListener(HandleEnterMine);
        returnButton?.onClick.RemoveListener(HandleReturn);
    }

    private void HandleLevelSelected(HubLevelPresentation presentation)
    {
        Show(presentation);
    }

    private void HandleSelectionCleared()
    {
        Hide();
    }

    private void HandleEnterMine()
    {
        campaignController?.LaunchSelectedLevel();
    }

    private void HandleReturn()
    {
        if (campaignController != null)
        {
            campaignController.ClearSelection();
        }
        else
        {
            Hide();
        }
    }

    private void RefreshLabels(HubLevelPresentation presentation)
    {
        SetText(levelNameLabel, presentation.DisplayName);
        SetText(statusLabel, presentation.StatusLabel);
        SetText(
            objectiveLabel,
            $"Target: {presentation.RequiredMinedResources} ore\n" +
            $"Dwarves: {presentation.TotalDwarves}\n" +
            $"Ore in level: {presentation.TotalOreCapacity}");
        SetText(
            rewardLabel,
            $"Ore still earnable: " +
            $"{presentation.RemainingRewardPotential}");
        SetText(
            bestResultLabel,
            BuildBestResultText(presentation));
        SetText(lockedReasonLabel, presentation.LockedReason);

        if (lockedReasonLabel != null)
        {
            lockedReasonLabel.gameObject.SetActive(
                !string.IsNullOrWhiteSpace(presentation.LockedReason));
        }
    }

    private static string BuildBestResultText(
        HubLevelPresentation presentation)
    {
        if (!presentation.IsCompleted)
        {
            return "No completed run yet.";
        }

        int percentage = presentation.TotalOreCapacity > 0
            ? Mathf.RoundToInt(
                100f * presentation.BestDeliveredResources /
                presentation.TotalOreCapacity)
            : 0;
        string perfect = presentation.PerfectResourceRun
            ? " | Perfect"
            : string.Empty;
        string time = presentation.BestSimulationSeconds > 0d
            ? $"\nBest simulation time: " +
              FormatTime(presentation.BestSimulationSeconds)
            : string.Empty;

        return
            $"Best: {presentation.BestDeliveredResources}/" +
            $"{presentation.TotalOreCapacity} ore ({percentage}%)" +
            perfect +
            time;
    }

    private static string FormatTime(double totalSeconds)
    {
        TimeSpan time = TimeSpan.FromSeconds(Math.Max(0d, totalSeconds));
        return time.TotalHours >= 1d
            ? $"{(int)time.TotalHours:00}:{time.Minutes:00}:" +
              $"{time.Seconds:00}"
            : $"{time.Minutes:00}:{time.Seconds:00}";
    }

    private void SetVisible(bool visible)
    {
        if (panelGroup == null)
        {
            return;
        }

        panelGroup.alpha = visible ? 1f : 0f;
        panelGroup.interactable = visible;
        panelGroup.blocksRaycasts = visible;
    }

    private static void SetText(TMP_Text label, string value)
    {
        if (label != null)
        {
            label.text = value ?? string.Empty;
        }
    }
}
