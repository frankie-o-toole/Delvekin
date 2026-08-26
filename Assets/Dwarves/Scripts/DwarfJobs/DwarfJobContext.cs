public sealed class DwarfJobContext
{
    public DwarfAgent Agent { get; }
    public DwarfMovement Movement { get; }
    public VoxelWorld World { get; }
    public DwarfJobController Controller { get; }

    public DwarfJobContext(
        DwarfAgent agent,
        DwarfMovement movement,
        VoxelWorld world,
        DwarfJobController controller)
    {
        Agent = agent;
        Movement = movement;
        World = world;
        Controller = controller;
    }
}