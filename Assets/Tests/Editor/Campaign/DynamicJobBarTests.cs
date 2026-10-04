using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

public sealed class DynamicJobBarTests
{
    [Test]
    public void LockedJobHasLockedSlotState()
    {
        DwarfJobBarUI.JobSlotState state =
            DwarfJobBarUI.ResolveJobSlotState(
                implemented: true,
                unlocked: false,
                offeredInLevel: true,
                count: 5);

        Assert.That(state,
            Is.EqualTo(DwarfJobBarUI.JobSlotState.Locked));
    }

    [Test]
    public void UnlockedJobAbsentFromLevelIsUnavailable()
    {
        DwarfJobBarUI.JobSlotState state =
            DwarfJobBarUI.ResolveJobSlotState(
                implemented: true,
                unlocked: true,
                offeredInLevel: false,
                count: 0);

        Assert.That(state, Is.EqualTo(
            DwarfJobBarUI.JobSlotState.UnavailableInLevel));
    }

    [Test]
    public void EmptyOfferedJobIsExhausted()
    {
        DwarfJobBarUI.JobSlotState state =
            DwarfJobBarUI.ResolveJobSlotState(
                implemented: true,
                unlocked: true,
                offeredInLevel: true,
                count: 0);

        Assert.That(state,
            Is.EqualTo(DwarfJobBarUI.JobSlotState.Exhausted));
    }

    [Test]
    public void StockedOfferedJobIsAvailable()
    {
        DwarfJobBarUI.JobSlotState state =
            DwarfJobBarUI.ResolveJobSlotState(
                implemented: true,
                unlocked: true,
                offeredInLevel: true,
                count: 3);

        Assert.That(state,
            Is.EqualTo(DwarfJobBarUI.JobSlotState.Available));
    }

    [Test]
    public void ApplyingSlotStateDoesNotChangeEditorLayout()
    {
        GameObject slotObject = new(
            "JobSlot",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Button),
            typeof(JobSlotUI));

        RectTransform rect =
            slotObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.25f, 0.1f);
        rect.anchorMax = new Vector2(0.75f, 0.2f);
        rect.pivot = new Vector2(0.3f, 0.4f);
        rect.anchoredPosition = new Vector2(123f, 45f);
        rect.sizeDelta = new Vector2(91f, 63f);

        Vector2 anchorMin = rect.anchorMin;
        Vector2 anchorMax = rect.anchorMax;
        Vector2 pivot = rect.pivot;
        Vector2 position = rect.anchoredPosition;
        Vector2 size = rect.sizeDelta;

        slotObject.GetComponent<JobSlotUI>().ApplyJobState(
            "Tunneller",
            null,
            DwarfJobBarUI.JobSlotState.Available,
            5,
            true,
            false);

        Assert.That(rect.anchorMin, Is.EqualTo(anchorMin));
        Assert.That(rect.anchorMax, Is.EqualTo(anchorMax));
        Assert.That(rect.pivot, Is.EqualTo(pivot));
        Assert.That(rect.anchoredPosition, Is.EqualTo(position));
        Assert.That(rect.sizeDelta, Is.EqualTo(size));

        Object.DestroyImmediate(slotObject);
    }
}
