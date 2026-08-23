using UnityEngine;

public class DirectionAlterAbility : IDwarfAbility
{
    private DwarfAgent dwarf;

    public bool IsComplete { get; private set; }

    public bool ControlsMovement => false;


    public void Enter(DwarfAgent dwarf)
    {
        this.dwarf = dwarf;
    }


    public void Tick()
    {
        if (IsComplete)
            return;


        // temporary:
        // immediately place sign

        dwarf.Freeze();

        IsComplete = true;
    }


    public void Exit()
    {

    }
}