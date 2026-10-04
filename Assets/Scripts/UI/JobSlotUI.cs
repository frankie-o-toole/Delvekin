using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
public sealed class JobSlotUI : MonoBehaviour
{
    [Header("Content")]
    [SerializeField]
    private Button button;

    [SerializeField]
    private Image iconImage;

    [SerializeField]
    private TMP_Text nameLabel;

    [SerializeField]
    private TMP_Text stockLabel;

    [Header("State Layers")]
    [Tooltip("Optional editor-authored visual shown while this slot is selected.")]
    [SerializeField]
    private GameObject selectedVisual;

    [Tooltip("Optional editor-authored visual shown when the job is locked.")]
    [SerializeField]
    private GameObject lockedVisual;

    [Tooltip("Optional editor-authored visual shown when the slot is disabled.")]
    [SerializeField]
    private GameObject unavailableVisual;

    private void Reset()
    {
        button = GetComponent<Button>();
    }

    private void Awake()
    {
        button ??= GetComponent<Button>();
    }

    public void ApplyJobState(
        string displayName,
        Sprite icon,
        DwarfJobBarUI.JobSlotState state,
        int count,
        bool interactable,
        bool selected)
    {
        button ??= GetComponent<Button>();
        button.interactable = interactable;

        bool locked = state == DwarfJobBarUI.JobSlotState.Locked;
        bool unavailable =
            state == DwarfJobBarUI.JobSlotState.UnavailableInLevel ||
            state == DwarfJobBarUI.JobSlotState.Exhausted;

        SetActive(selectedVisual, selected);
        SetActive(lockedVisual, locked);
        SetActive(unavailableVisual, unavailable);

        if (nameLabel != null)
        {
            nameLabel.text = locked ? "?" : displayName;
        }

        if (iconImage != null)
        {
            iconImage.sprite = icon;
            iconImage.enabled = !locked && icon != null;
        }

        if (stockLabel != null)
        {
            stockLabel.text = state switch
            {
                DwarfJobBarUI.JobSlotState.Available => count.ToString(),
                DwarfJobBarUI.JobSlotState.Exhausted => "0",
                DwarfJobBarUI.JobSlotState.UnavailableInLevel => "—",
                _ => string.Empty
            };
        }
    }

    public void ApplyStopState(
        string displayName,
        bool interactable,
        bool selected)
    {
        button ??= GetComponent<Button>();
        button.interactable = interactable;

        SetActive(selectedVisual, selected);
        SetActive(lockedVisual, false);
        SetActive(unavailableVisual, !interactable);

        if (nameLabel != null)
        {
            nameLabel.text = displayName;
        }

        if (stockLabel != null)
        {
            stockLabel.text = string.Empty;
        }
    }

    private static void SetActive(
        GameObject target,
        bool active)
    {
        if (target != null && target.activeSelf != active)
        {
            target.SetActive(active);
        }
    }
}
