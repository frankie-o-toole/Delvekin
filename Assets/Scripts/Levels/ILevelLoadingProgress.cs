public interface ILevelLoadingProgress
{
    LevelLoadingProgress LoadingProgress { get; }
    bool IsLevelReady { get; }
    bool IsLevelLoading { get; }
}
