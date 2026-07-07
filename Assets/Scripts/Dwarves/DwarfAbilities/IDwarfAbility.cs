public interface IDwarfAbility
{
    void Enter(DwarfAgent dwarf);

    void Tick();

    void Exit();

    bool IsComplete { get; }

    bool ControlsMovement { get; }
}