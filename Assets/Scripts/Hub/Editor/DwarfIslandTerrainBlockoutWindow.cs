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

    [Header("Island")]
    [SerializeField, Range(0.55f, 0.95f)]
    private float islandRadius = 0.82f;
    [SerializeField, Range(0.65f, 1.35f)]
    private float islandAspect = 1f;
    [SerializeField, Range(0.01f, 0.2f)]
    private float coastlineVariation = 0.09f;
    [SerializeField, Range(0.01f, 0.15f)]
    private float coastSoftness = 0.055f;

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
    private float mountainHeight = 0.46f;
    [SerializeField, Range(0.15f, 0.75f)]
    private float mountainWidth = 0.48f;
    [SerializeField, Range(0.15f, 0.8f)]
    private float mountainDepth = 0.5f;
    [SerializeField, Range(-0.35f, 0.35f)]
    private float mountainOffset = -0.06f;
    [SerializeField, Range(-0.1f, 0.65f)]
    private float cityFacePosition = 0.26f;
    [SerializeField, Range(0.01f, 0.18f)]
    private float cityFaceSteepness = 0.055f;
    [SerializeField, Range(0f, 0.2f)]
    private float mountainRoughness = 0.085f;

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
            "Generates an editable heightmap for the hub island. " +
            "East matches a city camera placed on the positive X side. " +
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

        DrawIslandSettings();
        DrawLandSettings();
        DrawMountainSettings();
        DrawApproachSettings();

        EditorGUILayout.Space();

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

    private void DrawIslandSettings()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Island", EditorStyles.boldLabel);
        islandRadius = EditorGUILayout.Slider(
            "Radius", islandRadius, 0.55f, 0.95f);
        islandAspect = EditorGUILayout.Slider(
            "Aspect", islandAspect, 0.65f, 1.35f);
        coastlineVariation = EditorGUILayout.Slider(
            "Coast Variation", coastlineVariation, 0.01f, 0.2f);
        coastSoftness = EditorGUILayout.Slider(
            "Coast Softness", coastSoftness, 0.01f, 0.15f);
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
        mountainHeight = EditorGUILayout.Slider(
            "Height", mountainHeight, 0.1f, 0.8f);
        mountainWidth = EditorGUILayout.Slider(
            "Width", mountainWidth, 0.15f, 0.75f);
        mountainDepth = EditorGUILayout.Slider(
            "Depth", mountainDepth, 0.15f, 0.8f);
        mountainOffset = EditorGUILayout.Slider(
            "Offset From Centre", mountainOffset, -0.35f, 0.35f);
        cityFacePosition = EditorGUILayout.Slider(
            "City Face Position", cityFacePosition, -0.1f, 0.65f);
        cityFaceSteepness = EditorGUILayout.Slider(
            "City Face Steepness", cityFaceSteepness, 0.01f, 0.18f);
        mountainRoughness = EditorGUILayout.Slider(
            "Roughness", mountainRoughness, 0f, 0.2f);
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
                    out float towardCity,
                    out float acrossCity);

                heights[z, x] = CalculateHeight(
                    localX,
                    localZ,
                    towardCity,
                    acrossCity,
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
        float towardCity,
        float acrossCity,
        Vector2 seedOffset)
    {
        float aspectX = islandAspect >= 1f
            ? islandAspect
            : 1f;
        float aspectZ = islandAspect < 1f
            ? 1f / islandAspect
            : 1f;

        float ellipticalDistance = Mathf.Sqrt(
            Square(x / aspectX) + Square(z / aspectZ));
        float angle = Mathf.Atan2(z, x);

        float coastNoise = FractalNoise(
            Mathf.Cos(angle) * 0.85f + seedOffset.x,
            Mathf.Sin(angle) * 0.85f + seedOffset.y,
            3);

        float localRadius = islandRadius +
            (coastNoise - 0.5f) * coastlineVariation * 2f;
        float islandMask = 1f - SmoothStep(
            localRadius - coastSoftness,
            localRadius + coastSoftness,
            ellipticalDistance);

        if (islandMask <= 0f)
        {
            return 0f;
        }

        float landNoise = FractalNoise(
            x * detailScale + seedOffset.x,
            z * detailScale + seedOffset.y,
            4);

        float meadow = shoreHeight +
            meadowRelief * Mathf.SmoothStep(0f, 1f, islandMask) +
            (landNoise - 0.5f) * detailStrength;

        float mountainU =
            (towardCity - mountainOffset) / Mathf.Max(0.01f, mountainDepth);
        float mountainV =
            acrossCity / Mathf.Max(0.01f, mountainWidth);
        float mountainDome = Mathf.Exp(
            -(mountainU * mountainU + mountainV * mountainV) * 1.35f);

        float cityCliff = 1f - SmoothStep(
            cityFacePosition - cityFaceSteepness,
            cityFacePosition + cityFaceSteepness,
            towardCity);

        float mountainNoise = FractalNoise(
            x * 3.25f + seedOffset.x + 17.3f,
            z * 3.25f + seedOffset.y - 9.7f,
            4);
        float facadeCalm = SmoothStep(
            cityFaceSteepness,
            cityFaceSteepness * 4f,
            Mathf.Abs(towardCity - cityFacePosition));
        float roughness =
            (mountainNoise - 0.42f) * mountainRoughness * facadeCalm;

        float mountain = Mathf.Max(
            0f,
            mountainDome * cityCliff * (mountainHeight + roughness));

        float pathCentre = Mathf.Sin(
            Mathf.InverseLerp(-islandRadius, cityFacePosition, towardCity) *
            Mathf.PI * 1.35f) * pathCurve;
        float pathDistance = Mathf.Abs(acrossCity - pathCentre);
        float pathMask = 1f - SmoothStep(
            pathHalfWidth,
            pathHalfWidth * 2f,
            pathDistance);
        float approachMask = SmoothStep(
            -islandRadius,
            -islandRadius * 0.35f,
            towardCity) *
            (1f - SmoothStep(
                cityFacePosition - 0.1f,
                cityFacePosition + 0.04f,
                towardCity));

        float height = meadow + mountain -
            pathDepth * pathMask * approachMask;

        return Mathf.Clamp01(height * islandMask);
    }

    private void GetFaceCoordinates(
        float x,
        float z,
        out float towardCity,
        out float acrossCity)
    {
        switch (cityFace)
        {
            case CityFaceDirection.West:
                towardCity = -x;
                acrossCity = -z;
                break;

            case CityFaceDirection.North:
                towardCity = z;
                acrossCity = x;
                break;

            case CityFaceDirection.South:
                towardCity = -z;
                acrossCity = -x;
                break;

            default:
                towardCity = x;
                acrossCity = z;
                break;
        }
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
