using UnityEditor;
using UnityEngine;

public sealed class DwarfIslandTerrainBlockoutWindow : EditorWindow
{
    private enum CityFaceDirection
    {
        East,
        West,
        North,
        South
    }

    private const string UndoName = "Generate Dwarf Island Blockout";

    private Vector2 scrollPosition;

    [SerializeField] private Terrain targetTerrain;
    [SerializeField] private int seed = 2718;
    [SerializeField] private CityFaceDirection cityFace =
        CityFaceDirection.East;

    [Header("Coast")]
    [SerializeField, Range(-0.5f, 0.35f)]
    private float coastPosition = -0.08f;
    [SerializeField, Range(0f, 0.15f)]
    private float coastlineVariation = 0.045f;
    [SerializeField, Range(0.01f, 0.15f)]
    private float coastSoftness = 0.05f;
    [SerializeField, Range(0f, 0.04f)]
    private float seaFloorHeight = 0.004f;

    [Header("Land")]
    [SerializeField, Range(0.01f, 0.2f)]
    private float shoreHeight = 0.045f;
    [SerializeField, Range(0f, 0.08f)]
    private float meadowRelief = 0.018f;
    [SerializeField, Range(0f, 0.08f)]
    private float detailStrength = 0.025f;
    [SerializeField, Range(1f, 12f)]
    private float detailScale = 5.5f;

    [Header("Mountain")]
    [SerializeField, Range(0.1f, 0.8f)]
    private float primaryPeakHeight = 0.49f;
    [SerializeField, Range(-0.55f, 0.55f)]
    private float primaryPeakAcross = 0.2f;
    [SerializeField, Range(0.1f, 0.7f)]
    private float secondaryPeakHeight = 0.34f;
    [SerializeField, Range(-0.55f, 0.55f)]
    private float secondaryPeakAcross = -0.3f;
    [SerializeField, Range(-0.65f, 0f)]
    private float mountainDepthPosition = -0.29f;
    [SerializeField, Range(0.2f, 0.9f)]
    private float mountainWidth = 0.58f;
    [SerializeField, Range(0.2f, 0.9f)]
    private float mountainDepth = 0.48f;
    [SerializeField, Range(-0.1f, 0.65f)]
    private float cityFacePosition = 0.16f;
    [SerializeField, Range(0.01f, 0.18f)]
    private float cityFaceSteepness = 0.045f;
    [SerializeField, Range(0.05f, 0.4f)]
    private float cityFacadeHalfWidth = 0.15f;
    [SerializeField, Range(0f, 0.2f)]
    private float mountainRoughness = 0.055f;
    [SerializeField, Range(0f, 0.75f)]
    private float terraceStrength = 0.24f;
    [SerializeField, Range(0.02f, 0.14f)]
    private float terraceHeight = 0.065f;

    [Header("Approach")]
    [SerializeField, Range(0f, 0.06f)]
    private float pathDepth = 0.014f;
    [SerializeField, Range(0.01f, 0.16f)]
    private float pathHalfWidth = 0.045f;
    [SerializeField, Range(0f, 0.35f)]
    private float pathCurve = 0.13f;

    [MenuItem("Delvekin/Dwarf Island/Terrain Blockout")]
    private static void Open()
    {
        GetWindow<DwarfIslandTerrainBlockoutWindow>(
            "Island Blockout");
    }

    [MenuItem("GameObject/Delvekin/Use Terrain For Island Blockout", false, 10)]
    private static void OpenForSelectedTerrain()
    {
        Terrain selected = Selection.activeGameObject != null
            ? Selection.activeGameObject.GetComponent<Terrain>()
            : null;

        DwarfIslandTerrainBlockoutWindow window =
            GetWindow<DwarfIslandTerrainBlockoutWindow>(
                "Island Blockout");

        if (selected != null)
        {
            window.targetTerrain = selected;
        }

        window.Show();
    }

    private void OnEnable()
    {
        if (targetTerrain == null &&
            Selection.activeGameObject != null)
        {
            targetTerrain =
                Selection.activeGameObject.GetComponent<Terrain>();
        }
    }

    private void OnGUI()
    {
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        EditorGUILayout.LabelField(
            "Dwarf Island Terrain Blockout",
            EditorStyles.boldLabel);

        EditorGUILayout.HelpBox(
            "Generates the peninsula composition for the Dwarf Island hub: " +
            "meadow in front, sea behind, and a two-peak mountain at the " +
            "coast. East matches a city camera on the positive X side. " +
            "Generation changes heights only; Terrain layers remain intact.",
            MessageType.Info);

        targetTerrain = (Terrain)EditorGUILayout.ObjectField(
            "Target Terrain",
            targetTerrain,
            typeof(Terrain),
            true);

        seed = EditorGUILayout.IntField("Seed", seed);
        cityFace = (CityFaceDirection)EditorGUILayout.EnumPopup(
            "City Face Direction",
            cityFace);

        DrawCoastSettings();
        DrawLandSettings();
        DrawMountainSettings();
        DrawApproachSettings();

        EditorGUILayout.Space();

        if (GUILayout.Button("Reset Parameters To Sketch Defaults"))
        {
            ResetToSketchDefaults();
        }

        using (new EditorGUI.DisabledScope(
                   targetTerrain == null ||
                   targetTerrain.terrainData == null ||
                   Application.isPlaying))
        {
            if (GUILayout.Button("Generate Terrain Blockout", GUILayout.Height(32f)))
            {
                GenerateWithConfirmation();
            }
        }

        if (targetTerrain == null)
        {
            EditorGUILayout.HelpBox(
                "Assign a Terrain or select one before opening this window.",
                MessageType.Warning);
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawCoastSettings()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Coast", EditorStyles.boldLabel);
        coastPosition = EditorGUILayout.Slider(
            "Coast Position", coastPosition, -0.5f, 0.35f);
        coastlineVariation = EditorGUILayout.Slider(
            "Coast Variation", coastlineVariation, 0f, 0.15f);
        coastSoftness = EditorGUILayout.Slider(
            "Coast Softness", coastSoftness, 0.01f, 0.15f);
        seaFloorHeight = EditorGUILayout.Slider(
            "Sea Floor Height", seaFloorHeight, 0f, 0.04f);
    }

    private void DrawLandSettings()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Land", EditorStyles.boldLabel);
        shoreHeight = EditorGUILayout.Slider(
            "Shore Height", shoreHeight, 0.01f, 0.2f);
        meadowRelief = EditorGUILayout.Slider(
            "Meadow Relief", meadowRelief, 0f, 0.08f);
        detailStrength = EditorGUILayout.Slider(
            "Detail Strength", detailStrength, 0f, 0.08f);
        detailScale = EditorGUILayout.Slider(
            "Detail Scale", detailScale, 1f, 12f);
    }

    private void DrawMountainSettings()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Mountain", EditorStyles.boldLabel);
        primaryPeakHeight = EditorGUILayout.Slider(
            "Primary Peak Height", primaryPeakHeight, 0.1f, 0.8f);
        primaryPeakAcross = EditorGUILayout.Slider(
            "Primary Peak Side", primaryPeakAcross, -0.55f, 0.55f);
        secondaryPeakHeight = EditorGUILayout.Slider(
            "Secondary Peak Height", secondaryPeakHeight, 0.1f, 0.7f);
        secondaryPeakAcross = EditorGUILayout.Slider(
            "Secondary Peak Side", secondaryPeakAcross, -0.55f, 0.55f);
        mountainDepthPosition = EditorGUILayout.Slider(
            "Depth Position", mountainDepthPosition, -0.65f, 0f);
        mountainWidth = EditorGUILayout.Slider(
            "Overall Width", mountainWidth, 0.2f, 0.9f);
        mountainDepth = EditorGUILayout.Slider(
            "Overall Depth", mountainDepth, 0.2f, 0.9f);
        cityFacePosition = EditorGUILayout.Slider(
            "City Face Position", cityFacePosition, -0.1f, 0.65f);
        cityFaceSteepness = EditorGUILayout.Slider(
            "City Face Steepness", cityFaceSteepness, 0.01f, 0.18f);
        cityFacadeHalfWidth = EditorGUILayout.Slider(
            "City Facade Half Width", cityFacadeHalfWidth, 0.05f, 0.4f);
        mountainRoughness = EditorGUILayout.Slider(
            "Roughness", mountainRoughness, 0f, 0.2f);
        terraceStrength = EditorGUILayout.Slider(
            "Terrace Strength", terraceStrength, 0f, 0.75f);
        terraceHeight = EditorGUILayout.Slider(
            "Terrace Height", terraceHeight, 0.02f, 0.14f);
    }

    private void DrawApproachSettings()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Approach Path", EditorStyles.boldLabel);
        pathDepth = EditorGUILayout.Slider(
            "Depth", pathDepth, 0f, 0.06f);
        pathHalfWidth = EditorGUILayout.Slider(
            "Half Width", pathHalfWidth, 0.01f, 0.16f);
        pathCurve = EditorGUILayout.Slider(
            "Curve", pathCurve, 0f, 0.35f);
    }

    private void GenerateWithConfirmation()
    {
        if (!EditorUtility.DisplayDialog(
                "Generate Dwarf Island Blockout?",
                $"This replaces the heightmap of " +
                $"'{targetTerrain.name}'. You can undo the operation.",
                "Generate",
                "Cancel"))
        {
            return;
        }

        TerrainData data = targetTerrain.terrainData;
        Undo.RegisterCompleteObjectUndo(data, UndoName);

        int resolution = data.heightmapResolution;
        float[,] heights = new float[resolution, resolution];
        Vector2 seedOffset = BuildSeedOffset(seed);

        for (int z = 0; z < resolution; z++)
        {
            float normalizedZ = z / (resolution - 1f);
            float localZ = normalizedZ * 2f - 1f;

            for (int x = 0; x < resolution; x++)
            {
                float normalizedX = x / (resolution - 1f);
                float localX = normalizedX * 2f - 1f;

                GetFaceCoordinates(
                    localX,
                    localZ,
                    out float towardViewer,
                    out float acrossMountain);

                heights[z, x] = CalculateHeight(
                    localX,
                    localZ,
                    towardViewer,
                    acrossMountain,
                    seedOffset);
            }
        }

        data.SetHeights(0, 0, heights);
        EditorUtility.SetDirty(data);
        targetTerrain.Flush();
        SceneView.RepaintAll();

        Debug.Log(
            $"Generated Dwarf Island blockout on '{targetTerrain.name}' " +
            $"with seed {seed} and {cityFace} city face.",
            targetTerrain);
    }

    private float CalculateHeight(
        float x,
        float z,
        float towardViewer,
        float acrossMountain,
        Vector2 seedOffset)
    {
        float coastNoise = FractalNoise(
            acrossMountain * 1.8f + seedOffset.x,
            seedOffset.y,
            3);
        float localCoast = coastPosition +
            (coastNoise - 0.5f) * coastlineVariation * 2f;
        float mainlandMask = SmoothStep(
            localCoast - coastSoftness,
            localCoast + coastSoftness,
            towardViewer);

        float landNoise = FractalNoise(
            x * detailScale + seedOffset.x,
            z * detailScale + seedOffset.y,
            4);

        float meadow = seaFloorHeight + mainlandMask *
            (shoreHeight - seaFloorHeight +
             meadowRelief * mainlandMask +
             (landNoise - 0.5f) * detailStrength);

        float primaryDepth =
            (towardViewer - mountainDepthPosition) /
            Mathf.Max(0.01f, mountainDepth * 0.82f);
        float primarySide =
            (acrossMountain - primaryPeakAcross) /
            Mathf.Max(0.01f, mountainWidth * 0.55f);
        float primaryPeak = Mathf.Exp(
            -(Square(primaryDepth) * 1.15f +
              Square(primarySide) * 1.3f)) *
            primaryPeakHeight;

        float secondaryDepth =
            (towardViewer - (mountainDepthPosition + 0.07f)) /
            Mathf.Max(0.01f, mountainDepth * 0.72f);
        float secondarySide =
            (acrossMountain - secondaryPeakAcross) /
            Mathf.Max(0.01f, mountainWidth * 0.48f);
        float secondaryPeak = Mathf.Exp(
            -(Square(secondaryDepth) * 1.25f +
              Square(secondarySide) * 1.45f)) *
            secondaryPeakHeight;

        float shoulderDepth =
            (towardViewer - mountainDepthPosition) /
            Mathf.Max(0.01f, mountainDepth);
        float shoulderSide =
            acrossMountain /
            Mathf.Max(0.01f, mountainWidth);
        float mountainShoulder = Mathf.Exp(
            -(Square(shoulderDepth) + Square(shoulderSide)) * 1.3f) *
            Mathf.Min(primaryPeakHeight, secondaryPeakHeight) * 0.58f;

        float mountainShape = Mathf.Max(
            mountainShoulder,
            Mathf.Max(primaryPeak, secondaryPeak));

        float cityCliff = 1f - SmoothStep(
            cityFacePosition - cityFaceSteepness,
            cityFacePosition + cityFaceSteepness,
            towardViewer);

        float facadeAcrossMask = 1f - SmoothStep(
            cityFacadeHalfWidth,
            cityFacadeHalfWidth + 0.14f,
            Mathf.Abs(acrossMountain));
        float facadeDepthMask = 1f - SmoothStep(
            cityFaceSteepness,
            cityFaceSteepness * 5f,
            Mathf.Abs(towardViewer - cityFacePosition));
        float facadeMask = facadeAcrossMask * facadeDepthMask;

        float mountainNoise = FractalNoise(
            x * 3.25f + seedOffset.x + 17.3f,
            z * 3.25f + seedOffset.y - 9.7f,
            4);
        float roughness = (mountainNoise - 0.45f) *
            mountainRoughness *
            (1f - facadeMask * 0.95f);

        float mountain = Mathf.Max(
            0f,
            (mountainShape +
             roughness * SmoothStep(0.015f, 0.12f, mountainShape)) *
            cityCliff);

        float safeTerraceHeight = Mathf.Max(0.001f, terraceHeight);
        float terraceBase = Mathf.Floor(
            mountain / safeTerraceHeight) * safeTerraceHeight;
        float terraceFraction = Mathf.Repeat(
            mountain,
            safeTerraceHeight) / safeTerraceHeight;
        float terracedMountain = terraceBase +
            SmoothStep(0.72f, 1f, terraceFraction) *
            safeTerraceHeight;
        float localTerraceStrength = terraceStrength *
            (1f - facadeMask);
        mountain = Mathf.Lerp(
            mountain,
            terracedMountain,
            localTerraceStrength);

        float mountainFootprint = SmoothStep(
            0.008f,
            0.085f,
            mountainShape * cityCliff);
        float peninsulaBase = Mathf.Lerp(
            seaFloorHeight,
            shoreHeight,
            mountainFootprint);
        float baseHeight = Mathf.Max(meadow, peninsulaBase);

        float pathCentre = Mathf.Sin(
            Mathf.InverseLerp(cityFacePosition, 1f, towardViewer) *
            Mathf.PI * 1.25f) * pathCurve;
        float pathDistance = Mathf.Abs(
            acrossMountain - pathCentre);
        float pathMask = 1f - SmoothStep(
            pathHalfWidth,
            pathHalfWidth * 2f,
            pathDistance);
        float approachMask = SmoothStep(
            cityFacePosition - 0.03f,
            cityFacePosition + 0.09f,
            towardViewer) *
            (1f - SmoothStep(0.92f, 1.02f, towardViewer));

        float height = baseHeight + mountain -
            pathDepth * pathMask * approachMask;

        return Mathf.Clamp01(Mathf.Max(seaFloorHeight, height));
    }

    private void GetFaceCoordinates(
        float x,
        float z,
        out float towardViewer,
        out float acrossMountain)
    {
        switch (cityFace)
        {
            case CityFaceDirection.West:
                towardViewer = -x;
                acrossMountain = -z;
                break;

            case CityFaceDirection.North:
                towardViewer = z;
                acrossMountain = x;
                break;

            case CityFaceDirection.South:
                towardViewer = -z;
                acrossMountain = -x;
                break;

            default:
                towardViewer = x;
                acrossMountain = z;
                break;
        }
    }

    private void ResetToSketchDefaults()
    {
        seed = 2718;
        cityFace = CityFaceDirection.East;

        coastPosition = -0.08f;
        coastlineVariation = 0.045f;
        coastSoftness = 0.05f;
        seaFloorHeight = 0.004f;

        shoreHeight = 0.045f;
        meadowRelief = 0.018f;
        detailStrength = 0.025f;
        detailScale = 5.5f;

        primaryPeakHeight = 0.49f;
        primaryPeakAcross = 0.2f;
        secondaryPeakHeight = 0.34f;
        secondaryPeakAcross = -0.3f;
        mountainDepthPosition = -0.29f;
        mountainWidth = 0.58f;
        mountainDepth = 0.48f;
        cityFacePosition = 0.16f;
        cityFaceSteepness = 0.045f;
        cityFacadeHalfWidth = 0.15f;
        mountainRoughness = 0.055f;
        terraceStrength = 0.24f;
        terraceHeight = 0.065f;

        pathDepth = 0.014f;
        pathHalfWidth = 0.045f;
        pathCurve = 0.13f;

        Repaint();
    }

    private static Vector2 BuildSeedOffset(int value)
    {
        System.Random random = new(value);

        return new Vector2(
            (float)random.NextDouble() * 1000f,
            (float)random.NextDouble() * 1000f);
    }

    private static float FractalNoise(
        float x,
        float y,
        int octaves)
    {
        float value = 0f;
        float amplitude = 0.5f;
        float frequency = 1f;
        float amplitudeSum = 0f;

        for (int octave = 0; octave < octaves; octave++)
        {
            value += Mathf.PerlinNoise(
                x * frequency,
                y * frequency) * amplitude;
            amplitudeSum += amplitude;
            amplitude *= 0.5f;
            frequency *= 2f;
        }

        return amplitudeSum > 0f
            ? value / amplitudeSum
            : 0f;
    }

    private static float SmoothStep(float minimum, float maximum, float value)
    {
        return Mathf.SmoothStep(
            0f,
            1f,
            Mathf.InverseLerp(minimum, maximum, value));
    }

    private static float Square(float value)
    {
        return value * value;
    }
}
