public enum LevelLoadingState
{
    Idle,
    Preparing,
    BuildingChunks,
    ApplyingVoxels,
    CreatingRenderers,
    InitializingSystems,
    BuildingEntities,
    BuildingMeshes,
    Ready,
    Failed
}
