using UnityEngine;

public sealed class LevelLoadingOverlay : MonoBehaviour
{
    private ILevelLoadingProgress source;

    public void Configure(ILevelLoadingProgress loadingSource)
    {
        source = loadingSource;
    }

    private void OnGUI()
    {
        if (source == null || source.IsLevelReady)
        {
            return;
        }

        DrawLoadingScreen(source.LoadingProgress);
    }

    private static void DrawLoadingScreen(
        LevelLoadingProgress progress)
    {
        GUI.matrix = Matrix4x4.identity;

        Rect screen = new(0f, 0f, Screen.width, Screen.height);
        Color previousColor = GUI.color;

        GUI.color = new Color(0.035f, 0.035f, 0.045f, 1f);
        GUI.DrawTexture(screen, Texture2D.whiteTexture);
        GUI.color = previousColor;

        const float panelWidth = 560f;
        const float panelHeight = 170f;
        const float padding = 24f;

        Rect panel = new(
            (Screen.width - panelWidth) * 0.5f,
            (Screen.height - panelHeight) * 0.5f,
            panelWidth,
            panelHeight);

        GUI.Box(panel, "LOADING LEVEL");

        string phase = progress.State == LevelLoadingState.Failed
            ? "Loading failed"
            : progress.Phase;

        GUI.Label(
            new Rect(
                panel.x + padding,
                panel.y + 38f,
                panel.width - padding * 2f,
                26f),
            phase);

        Rect barBackground = new(
            panel.x + padding,
            panel.y + 76f,
            panel.width - padding * 2f,
            30f);

        GUI.Box(barBackground, GUIContent.none);

        Rect barFill = new(
            barBackground.x + 3f,
            barBackground.y + 3f,
            Mathf.Max(
                0f,
                (barBackground.width - 6f) * progress.Progress),
            barBackground.height - 6f);

        GUI.color = new Color(0.92f, 0.67f, 0.16f, 1f);
        GUI.DrawTexture(barFill, Texture2D.whiteTexture);
        GUI.color = previousColor;

        string work = progress.HasDeterminateProgress
            ? $"{progress.CompletedWork}/{progress.TotalWork}"
            : "Preparing…";

        GUI.Label(
            new Rect(
                panel.x + padding,
                panel.y + 116f,
                panel.width - padding * 2f,
                24f),
            work);
    }
}
