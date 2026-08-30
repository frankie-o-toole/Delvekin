using System.Collections.Generic;
using UnityEngine;

public class DwarfHighlight : MonoBehaviour
{
    [Header("References")]
    [SerializeField]
    private Transform visualRoot;

    [Header("Job Colours")]
    [SerializeField]
    [Range(0f, 1f)]
    private float jobColourStrength = 0.65f;

    [SerializeField]
    private Color directionAltererColour =
        new Color(0.55f, 0.2f, 0.85f, 1f);

    [SerializeField]
    private Color tunnellerColour =
        new Color(0.18f, 0.25f, 0.32f, 1f);

    [SerializeField]
    private Color diggerColour =
        new Color(0.1f, 0.38f, 0.16f, 1f);

    [SerializeField]
    private Color stairBuilderColour =
        new Color(0.48f, 0.24f, 0.07f, 1f);

    [SerializeField]
    private Color ladderBuilderColour =
        new Color(0.12f, 0.32f, 0.72f, 1f);

    [Header("Hover")]
    [SerializeField]
    private Color hoverColour =
        new Color(0f, 0.9f, 1f, 1f);

    [SerializeField]
    [Range(0f, 1f)]
    private float hoverStrength = 0.55f;

    [Header("Selected")]
    [SerializeField]
    private Color selectedColour =
        new Color(1f, 0.8f, 0.1f, 1f);

    [SerializeField]
    [Range(0f, 1f)]
    private float selectedStrength = 0.7f;

    [Header("Valid Job Target")]
    [SerializeField]
    private Color validTargetColour =
        new Color(0.15f, 1f, 0.25f, 1f);

    [SerializeField]
    [Range(0f, 1f)]
    private float validTargetStrength = 0.7f;

    [Header("Invalid Job Target")]
    [SerializeField]
    private Color invalidTargetColour =
        new Color(1f, 0.1f, 0.1f, 1f);

    [SerializeField]
    [Range(0f, 1f)]
    private float invalidTargetStrength = 0.75f;

    private static readonly int BaseColourProperty =
        Shader.PropertyToID("_BaseColor");

    private static readonly int ColourProperty =
        Shader.PropertyToID("_Color");

    private static readonly int ShaderGraphColourProperty =
        Shader.PropertyToID("_Base_Color");

    private readonly List<MaterialData> materials =
        new();

    private DwarfJobController jobController;

    private bool isHovered;
    private bool isSelected;

    private bool hasJobTargetState;
    private bool isValidJobTarget;

    private void Awake()
    {
        if (visualRoot == null)
        {
            visualRoot =
                transform.Find("VisualRoot");
        }

        if (visualRoot == null)
        {
            Debug.LogError(
                $"{name} has no VisualRoot.",
                this);

            return;
        }

        CacheRuntimeMaterials();
        RefreshHighlight();
    }

    private void OnEnable()
    {
        ResolveJobController();

        if (jobController != null)
        {
            jobController.StateChanged -=
                HandleJobStateChanged;

            jobController.StateChanged +=
                HandleJobStateChanged;
        }

        RefreshHighlight();
    }

    private void OnDisable()
    {
        if (jobController != null)
        {
            jobController.StateChanged -=
                HandleJobStateChanged;
        }

        isHovered = false;
        isSelected = false;

        hasJobTargetState = false;
        isValidJobTarget = false;

        RestoreOriginalColours();
    }

    private void ResolveJobController()
    {
        if (jobController != null)
        {
            return;
        }

        jobController =
            GetComponent<DwarfJobController>();

        if (jobController == null)
        {
            jobController =
                GetComponentInParent<DwarfJobController>();
        }
    }

    private void HandleJobStateChanged(
        DwarfJobController controller)
    {
        RefreshHighlight();
    }

    public void SetHovered(bool hovered)
    {
        isHovered = hovered;
        RefreshHighlight();
    }

    public void SetSelected(bool selected)
    {
        isSelected = selected;
        RefreshHighlight();
    }

    public void SetJobTargetState(
        bool active,
        bool valid)
    {
        hasJobTargetState = active;
        isValidJobTarget = valid;

        RefreshHighlight();
    }

    private void RefreshHighlight()
    {
        if (hasJobTargetState &&
            isHovered)
        {
            if (isValidJobTarget)
            {
                ApplyTint(
                    validTargetColour,
                    validTargetStrength);
            }
            else
            {
                ApplyTint(
                    invalidTargetColour,
                    invalidTargetStrength);
            }

            return;
        }

        if (isSelected)
        {
            ApplyTint(
                selectedColour,
                selectedStrength);

            return;
        }

        if (isHovered)
        {
            ApplyTint(
                hoverColour,
                hoverStrength);

            return;
        }

        ApplyJobBaseColours();
    }

    private void CacheRuntimeMaterials()
    {
        materials.Clear();

        Renderer[] renderers =
            visualRoot.GetComponentsInChildren<Renderer>(
                includeInactive: true);

        foreach (Renderer renderer in renderers)
        {
            Material[] runtimeMaterials =
                renderer.materials;

            foreach (Material material in runtimeMaterials)
            {
                if (material == null)
                {
                    continue;
                }

                int property =
                    FindColourProperty(material);

                if (property == -1)
                {
                    continue;
                }

                materials.Add(
                    new MaterialData(
                        material,
                        property,
                        material.GetColor(property)));
            }
        }
    }

    private static int FindColourProperty(
        Material material)
    {
        if (material.HasProperty(
                BaseColourProperty))
        {
            return BaseColourProperty;
        }

        if (material.HasProperty(
                ColourProperty))
        {
            return ColourProperty;
        }

        if (material.HasProperty(
                ShaderGraphColourProperty))
        {
            return ShaderGraphColourProperty;
        }

        return -1;
    }

    private void ApplyTint(
        Color tint,
        float strength)
    {
        foreach (MaterialData data in materials)
        {
            if (data.Material == null)
                continue;

            Color result =
                Color.Lerp(
                    GetBaseColour(data),
                    tint,
                    strength);

            result.a =
                data.OriginalColour.a;

            data.Material.SetColor(
                data.ColourProperty,
                result);
        }
    }

    private void ApplyJobBaseColours()
    {
        foreach (MaterialData data in materials)
        {
            if (data.Material == null)
                continue;

            data.Material.SetColor(
                data.ColourProperty,
                GetBaseColour(data));
        }
    }

    private Color GetBaseColour(
        MaterialData data)
    {
        if (!TryGetCurrentJobColour(
                out Color jobColour))
        {
            return data.OriginalColour;
        }

        Color result =
            Color.Lerp(
                data.OriginalColour,
                jobColour,
                jobColourStrength);

        result.a =
            data.OriginalColour.a;

        return result;
    }

    private bool TryGetCurrentJobColour(
        out Color colour)
    {
        ResolveJobController();

        DwarfJobType jobType =
            DwarfJobType.None;

        if (jobController != null)
        {
            jobType =
                jobController.HasActiveJob
                    ? jobController.ActiveJobType
                    : jobController.PendingJobType;
        }

        switch (jobType)
        {
            case DwarfJobType.DirectionAlter:
                colour = directionAltererColour;
                return true;

            case DwarfJobType.Tunneller:
                colour = tunnellerColour;
                return true;

            case DwarfJobType.Digger:
                colour = diggerColour;
                return true;

            case DwarfJobType.StairBuilder:
                colour = stairBuilderColour;
                return true;

            case DwarfJobType.LadderBuilder:
                colour = ladderBuilderColour;
                return true;

            default:
                colour = default;
                return false;
        }
    }

    private void RestoreOriginalColours()
    {
        foreach (MaterialData data in materials)
        {
            if (data.Material == null)
                continue;

            data.Material.SetColor(
                data.ColourProperty,
                data.OriginalColour);
        }
    }

    private sealed class MaterialData
    {
        public Material Material { get; }
        public int ColourProperty { get; }
        public Color OriginalColour { get; }

        public MaterialData(
            Material material,
            int colourProperty,
            Color originalColour)
        {
            Material = material;
            ColourProperty = colourProperty;
            OriginalColour = originalColour;
        }
    }
}
