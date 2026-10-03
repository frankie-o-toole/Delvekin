using System;

public sealed class LevelSimulationClock
{
    public double ElapsedSeconds { get; private set; }

    public TimeSpan Elapsed =>
        TimeSpan.FromSeconds(ElapsedSeconds);

    public void Advance(
        float scaledDeltaTime,
        LevelSimulationState state)
    {
        if (state != LevelSimulationState.Running ||
            float.IsNaN(scaledDeltaTime) ||
            float.IsInfinity(scaledDeltaTime) ||
            scaledDeltaTime <= 0f)
        {
            return;
        }

        ElapsedSeconds += scaledDeltaTime;
    }

    public void Reset()
    {
        ElapsedSeconds = 0d;
    }
}
