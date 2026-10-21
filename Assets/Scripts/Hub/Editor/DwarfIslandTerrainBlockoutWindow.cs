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
    private float primaryPeakHeight = 0.55f;
    [SerializeField, Range(-0.55f, 0.55f)]
    private float primaryPeakAcross = 0.17f;
    [SerializeField, Range(0.1f, 0.7f)]
    private float secondaryPeakHeight = 0.43f;
    [SerializeField, Range(-0.55f, 0.55f)]
    private float secondaryPeakAcross = -0.16f;
    [SerializeField, Range(-0.65f, 0f)]
    private float mountainDepthPosition = -0.29f;
    [SerializeField, Range(0.2f, 0.9f)]
    private float mountainWidth = 0.58f;
    [SerializeField, Range(0.2f, 0.9f)]
    private float mountainDepth = 0.48f;
    [SerializeField, Range(0f, 0.45f)]
    private float summitFlatness = 0.16f;
    [SerializeField, Range(0.45f, 2f)]
    private float slopeCurve = 0.82f;
    [SerializeField, Range(0f, 0.22f)]
    private float largeShapeVariation = 0.08f;
    [SerializeField, Range(-0.1f, 0.65f)]
    private float cityFacePosition = 0.16f;
    [SerializeField, Range(0.01f, 0.18f)]
    private float cityFaceSteepness = 0.045f;
    [SerializeField, Range(0.05f, 0.4f)]
    private float cityFacadeHalfWidth = 0.15f;
    [SerializeField, Range(0.04f, 0.35f)]
    private float cityFacadeHeight = 0.17f;
    [SerializeField, Range(0.05f, 0.4f)]
    private float cityFacadeDepth = 0.17f;
    [SerializeField, Range(0f, 0.2f)]
    private float mountainRoughness = 0.055f;
    [SerializeField, Range(0f, 0.75f)]
    private float terraceStrength = 0.24f;
    [SerializeField, Range(0.02f, 0.14f)]
    private float terraceHeight = 0.065f;

    [Header("C-Shaped Mountain")]
    [SerializeField, Range(0.12f, 0.55f)]
    private float armOffset = 0.34f;
    [SerializeField, Range(-0.1f, 0.5f)]
    private float armForwardPosition = 0.12f;
    [SerializeField, Range(0.12f, 0.5f)]
    private float armHeight = 0.2f;
    [SerializeField, Range(0.15f, 0.65f)]
    private float armLength = 0.46f;
    [SerializeField, Range(0.1f, 0.38f)]
    private float armThickness = 0.2f;

    [Header("Surface Painting")]
    [SerializeField]
    private bool paintTerrainLayers = true;
    [SerializeField, Range(0f, 1f)]
    private float rockStartHeight = 0.19f;
    [SerializeField, Range(0f, 1f)]
    private float snowStartHeight = 0.4f;
    [SerializeField, Range(0.01f, 0.25f)]
    private float surfaceBlend = 0.07f;

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
            "meadow in front, sea behind, and a C-shaped two-peak mountain " +
            "around the recessed city facade. East matches a city camera " +
            "on the positive X side. Optional surface painting uses Terrain " +
            "Layers 0-2 as Grass, Rock, and Snow.",
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
        DrawSurfaceSettings();
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
            "Right Peak Height", primaryPeakHeight, 0.1f, 0.8f);
        primaryPeakAcross = EditorGUILayout.Slider(
            "Right Peak Side", primaryPeakAcross, -0.55f, 0.55f);
        secondaryPeakHeight = EditorGUILayout.Slider(
            "Left Peak Height", secondaryPeakHeight, 0.1f, 0.7f);
        secondaryPeakAcross = EditorGUILayout.Slider(
            "Left Peak Side", secondaryPeakAcross, -0.55f, 0.55f);
        mountainDepthPosition = EditorGUILayout.Slider(
            "Depth Position", mountainDepthPosition, -0.65f, 0f);
        mountainWidth = EditorGUILayout.Slider(
            "Overall Width", mountainWidth, 0.2f, 0.9f);
        mountainDepth = EditorGUILayout.Slider(
            "Overall Depth", mountainDepth, 0.2f, 0.9f);
        summitFlatness = EditorGUILayout.Slider(
            "Summit Flatness", summitFlatness, 0f, 0.45f);
        slopeCurve = EditorGUILayout.Slider(
            "Slope Curve", slopeCurve, 0.45f, 2f);
        largeShapeVariation = EditorGUILayout.Slider(
            "Large Shape Variation", largeShapeVariation, 0f, 0.22f);
        cityFacePosition = EditorGUILayout.Slider(
            "City Face Position", cityFacePosition, -0.1f, 0.65f);
        cityFaceSteepness = EditorGUILayout.Slider(
            "City Face Steepness", cityFaceSteepness, 0.01f, 0.18f);
        cityFacadeHalfWidth = EditorGUILayout.Slider(
            "City Facade Half Width", cityFacadeHalfWidth, 0.05f, 0.4f);
        cityFacadeHeight = EditorGUILayout.Slider(
            "City Facade Height", cityFacadeHeight, 0.04f, 0.35f);
        cityFacadeDepth = EditorGUILayout.Slider(
            "City Facade Depth", cityFacadeDepth, 0.05f, 0.4f);
        mountainRoughness = EditorGUILayout.Slider(
            "Roughness", mountainRoughness, 0f, 0.2f);
        terraceStrength = EditorGUILayout.Slider(
            "Terrace Strength", terraceStrength, 0f, 0.75f);
        terraceHeight = EditorGUILayout.Slider(
            "Terrace Height", terraceHeight, 0.02f, 0.14f);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("C Shape", EditorStyles.boldLabel);
        armOffset = EditorGUILayout.Slider(
            "Arm Side Offset", armOffset, 0.12f, 0.55f);
        armForwardPosition = EditorGUILayout.Slider(
            "Arm Forward Position", armForwardPosition, -0.1f, 0.5f);
        armHeight = EditorGUILayout.Slider(
            "Arm Height", armHeight, 0.12f, 0.5f);
        armLength = EditorGUILayout.Slider(
            "Arm Length", armLength, 0.15f, 0.65f);
        armThickness = EditorGUILayout.Slider(
            "Arm Thickness", armThickness, 0.1f, 0.38f);
    }

    private void DrawSurfaceSettings()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField(
            "Surface Painting",
            EditorStyles.boldLabel);
        paintTerrainLayers = EditorGUILayout.Toggle(
            "Paint By Height",
            paintTerrainLayers);

        using (new EditorGUI.DisabledScope(!paintTerrainLayers))
        {
            rockStartHeight = EditorGUILayout.Slider(
                "Rock Start Height", rockStartHeight, 0f, 1f);
            snowStartHeight = EditorGUILayout.Slider(
                "Snow Start Height", snowStartHeight, 0f, 1f);
            surfaceBlend = EditorGUILayout.Slider(
                "Surface Blend", surfaceBlend, 0.01f, 0.25f);
        }

        if (paintTerrainLayers &&
            targetTerrain != null &&
            targetTerrain.terrainData != null &&
            targetTerrain.terrainData.terrainLayers.Length < 3)
        {
            EditorGUILayout.HelpBox(
                "Assign at least three Terrain Layers in this order: " +
                "Grass, Rock, Snow. Heights can still be generated, but " +
                "surface painting is skipped until those layers exist.",
                MessageType.Info);
        }
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

        if (paintTerrainLayers && data.terrainLayers.Length >= 3)
        {
            PaintSurfaceLayers(data);
        }

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

        float largeShapeNoise = FractalNoise(
            x * 1.65f + seedOffset.x - 23.1f,
            z * 1.65f + seedOffset.y + 11.4f,
            3);
        float shapeWarp =
            (largeShapeNoise - 0.5f) * largeShapeVariation;

        float primaryPeak = CalculateMountainMass(
            towardViewer,
            acrossMountain,
            mountainDepthPosition - 0.035f,
            primaryPeakAcross,
            mountainDepth * 0.72f,
            mountainWidth * 0.48f,
            primaryPeakHeight,
            summitFlatness * 0.65f,
            slopeCurve * 0.92f,
            shapeWarp);

        float secondaryPeak = CalculateMountainMass(
            towardViewer,
            acrossMountain,
            mountainDepthPosition + 0.015f,
            secondaryPeakAcross,
            mountainDepth * 0.68f,
            mountainWidth * 0.46f,
            secondaryPeakHeight,
            summitFlatness * 0.82f,
            slopeCurve * 0.88f,
            -shapeWarp * 0.8f);

        float rearMountainBody = CalculateMountainMass(
            towardViewer,
            acrossMountain,
            mountainDepthPosition + 0.02f,
            0f,
            mountainDepth,
            mountainWidth,
            Mathf.Min(primaryPeakHeight, secondaryPeakHeight) * 0.57f,
            0.13f,
            0.72f,
            shapeWarp * 0.45f);

        float rearMountain = Mathf.Max(
            rearMountainBody,
            Mathf.Max(primaryPeak, secondaryPeak));

        float cityCliff = 1f - SmoothStep(
            cityFacePosition - cityFaceSteepness,
            cityFacePosition + cityFaceSteepness,
            towardViewer);

        float facadeAcrossMask = 1f - SmoothStep(
            cityFacadeHalfWidth,
            cityFacadeHalfWidth + 0.14f,
            Mathf.Abs(acrossMountain));
        float facadeCutMask = 1f - SmoothStep(
            cityFacadeHalfWidth + 0.08f,
            cityFacadeHalfWidth + 0.25f,
            Mathf.Abs(acrossMountain));
        rearMountain *= Mathf.Lerp(1f, cityCliff, facadeCutMask);

        float leftArm = CalculateMountainMass(
            towardViewer,
            acrossMountain,
            armForwardPosition,
            -armOffset,
            armLength,
            armThickness,
            armHeight * 0.92f,
            0.14f,
            0.74f,
            shapeWarp * 0.35f);

        float rightArm = CalculateMountainMass(
            towardViewer,
            acrossMountain,
            armForwardPosition - 0.035f,
            armOffset,
            armLength * 1.04f,
            armThickness,
            armHeight,
            0.12f,
            0.72f,
            -shapeWarp * 0.3f);

        float facadeRearMask = SmoothStep(
            cityFacePosition - cityFacadeDepth,
            cityFacePosition - cityFacadeDepth + 0.055f,
            towardViewer);
        float facadeMass = cityFacadeHeight *
            facadeAcrossMask *
            facadeRearMask *
            cityCliff;

        float mountainShape = Mathf.Max(
            facadeMass,
            Mathf.Max(rearMountain, Mathf.Max(leftArm, rightArm)));

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
            mountainShape +
            roughness * SmoothStep(0.015f, 0.12f, mountainShape));

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

        float mountainIntegrationMask = SmoothStep(
            0.006f,
            0.07f,
            Mathf.Max(mountainShape, facadeMass));
        float integratedMountainBase = shoreHeight +
            (landNoise - 0.5f) * detailStrength * 0.25f;
        float baseHeight = Mathf.Lerp(
            meadow,
            integratedMountainBase,
            mountainIntegrationMask);

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

    private void PaintSurfaceLayers(TerrainData data)
    {
        int width = data.alphamapWidth;
        int height = data.alphamapHeight;
        int layerCount = data.terrainLayers.Length;
        float[,,] weights = new float[height, width, layerCount];
        float safeTerrainHeight = Mathf.Max(0.01f, data.size.y);

        for (int y = 0; y < height; y++)
        {
            float normalizedY = y / (height - 1f);

            for (int x = 0; x < width; x++)
            {
                float normalizedX = x / (width - 1f);
                float normalizedHeight = data.GetInterpolatedHeight(
                    normalizedX,
                    normalizedY) / safeTerrainHeight;
                float steepness = data.GetSteepness(
                    normalizedX,
                    normalizedY);

                float exposedRock = Mathf.Max(
                    SmoothStep(
                        rockStartHeight,
                        rockStartHeight + surfaceBlend,
                        normalizedHeight),
                    SmoothStep(28f, 54f, steepness));
                float snowByHeight = SmoothStep(
                    snowStartHeight,
                    snowStartHeight + surfaceBlend,
                    normalizedHeight);
                float snowOnSlope = 1f - SmoothStep(
                    48f,
                    72f,
                    steepness);
                float snow = snowByHeight * Mathf.Lerp(
                    0.38f,
                    1f,
                    snowOnSlope);
                float rock = exposedRock * (1f - snow);
                float grass = Mathf.Max(0f, 1f - exposedRock) *
                    (1f - snow);
                float total = Mathf.Max(0.0001f, grass + rock + snow);

                weights[y, x, 0] = grass / total;
                weights[y, x, 1] = rock / total;
                weights[y, x, 2] = snow / total;
            }
        }

        data.SetAlphamaps(0, 0, weights);
    }

    private static float CalculateMountainMass(
        float depthPosition,
        float acrossPosition,
        float centreDepth,
        float centreAcross,
        float depthRadius,
        float acrossRadius,
        float height,
        float flatness,
        float curve,
        float shapeWarp)
    {
        float depth =
            (depthPosition - centreDepth) /
            Mathf.Max(0.01f, depthRadius);
        float across =
            (acrossPosition - centreAcross) /
            Mathf.Max(0.01f, acrossRadius);
        float distance = Mathf.Sqrt(
            Square(depth) + Square(across));

        float warpedDistance = Mathf.Max(
            0f,
            distance + shapeWarp *
            Mathf.Lerp(0.35f, 1f, Mathf.Clamp01(distance)));
        float profile = 1f - SmoothStep(
            Mathf.Clamp01(flatness),
            1f,
            warpedDistance);

        return Mathf.Pow(
            Mathf.Clamp01(profile),
            Mathf.Max(0.05f, curve)) * height;
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

        primaryPeakHeight = 0.55f;
        primaryPeakAcross = 0.17f;
        secondaryPeakHeight = 0.43f;
        secondaryPeakAcross = -0.16f;
        mountainDepthPosition = -0.29f;
        mountainWidth = 0.58f;
        mountainDepth = 0.48f;
        summitFlatness = 0.16f;
        slopeCurve = 0.82f;
        largeShapeVariation = 0.08f;
        cityFacePosition = 0.16f;
        cityFaceSteepness = 0.045f;
        cityFacadeHalfWidth = 0.15f;
        cityFacadeHeight = 0.17f;
        cityFacadeDepth = 0.17f;
        mountainRoughness = 0.055f;
        terraceStrength = 0.24f;
        terraceHeight = 0.065f;

        armOffset = 0.34f;
        armForwardPosition = 0.12f;
        armHeight = 0.2f;
        armLength = 0.46f;
        armThickness = 0.2f;

        paintTerrainLayers = true;
        rockStartHeight = 0.19f;
        snowStartHeight = 0.4f;
        surfaceBlend = 0.07f;

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
