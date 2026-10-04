using NUnit.Framework;

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
}
