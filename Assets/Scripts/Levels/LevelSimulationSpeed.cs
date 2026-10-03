public enum LevelSimulationSpeed
{
    Normal = 1,
    Fast = 2,
    VeryFast = 4
}

public static class LevelSimulationSpeedUtility
{
    public static bool IsSupported(LevelSimulationSpeed speed)
    {
        return speed == LevelSimulationSpeed.Normal ||
               speed == LevelSimulationSpeed.Fast ||
               speed == LevelSimulationSpeed.VeryFast;
    }

    public static float ToTimeScale(LevelSimulationSpeed speed)
    {
        return IsSupported(speed)
            ? (float)speed
            : 1f;
    }
}
