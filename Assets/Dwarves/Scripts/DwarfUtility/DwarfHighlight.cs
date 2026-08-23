using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Highlights every compatible material beneath VisualRoot.
///
/// Runtime material instances allow each pooled dwarf to have its own
/// hover and selected colours without changing the shared source materials.
/// </summary>
public class DwarfHighlight : MonoBehaviour
{
    [Header("References")]
    [SerializeField]
    private Transform visualRoot;

    [Header("Hover")]
    [SerializeField]
    private Color hoverColour =
        new Color(
            0f,
            0.9f,
            1f,
            1f);

    [SerializeField]
    [Range(0f, 1f)]
    private float hoverStrength = 0.55f;

    [Header("Selected")]
    [SerializeField]
    private Color selectedColour =
        new Color(
            1f,
            0.8f,
            0.1f,
            1f);

    [SerializeField]
    [Range(0f, 1f)]
    private float selectedStrength = 0.7f;

    private static readonly int BaseColourProperty =
        Shader.PropertyToID("_BaseColor");

    private static readonly int ColourProperty =
        Shader.PropertyToID("_Color");

    private static readonly int ShaderGraphBaseColourProperty =
        Shader.PropertyToID("_Base_Color");

    private readonly List<MaterialData> materials =
        new();

    private bool isHovered;
    private bool isSelected;

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
                $"{name} has no VisualRoot for highlighting.",
                this);

            return;
        }

        CacheRuntimeMaterials();
        RefreshHighlight();
    }

    private void OnDisable()
    {
        isHovered = false;
        isSelected = false;

        RestoreOriginalColours();
    }

    public void SetHovered(bool hovered)
    {
        if (isHovered == hovered)
        {
            return;
        }

        isHovered = hovered;
        RefreshHighlight();
    }

    public void SetSelected(bool selected)
    {
        if (isSelected == selected)
        {
            return;
        }

        isSelected = selected;
        RefreshHighlight();
    }

    private void CacheRuntimeMaterials()
    {
        materials.Clear();

        Renderer[] renderers =
            visualRoot.GetComponentsInChildren<Renderer>(
                includeInactive: true);

        foreach (Renderer renderer in renderers)
        {
            // Accessing renderer.materials creates instances belonging
            // specifically to this renderer and dwarf.
            Material[] runtimeMaterials =
                renderer.materials;

            foreach (Material material in runtimeMaterials)
            {
                if (material == null)
                {
                    continue;
                }

                int colourProperty =
                    FindColourProperty(material);

                if (colourProperty == -1)
                {
                    Debug.LogWarning(
                        $"Material '{material.name}' on {renderer.name} "
                        + $"uses shader '{material.shader.name}', but no "
                        + "supported colour property was found. "
                        + "Expected _BaseColor, _Color or _Base_Color.",
                        renderer);

                    continue;
                }

                Color originalColour =
                    material.GetColor(
                        colourProperty);

                materials.Add(
                    new MaterialData(
                        material,
                        colourProperty,
                        originalColour));
            }
        }

        if (materials.Count == 0)
        {
            Debug.LogError(
                $"{name} found no highlight-compatible materials "
                + $"beneath VisualRoot.",
                this);
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
                ShaderGraphBaseColourProperty))
        {
            return ShaderGraphBaseColourProperty;
        }

        return -1;
    }

    private void RefreshHighlight()
    {
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

        RestoreOriginalColours();
    }

    private void ApplyTint(
        Color tint,
        float strength)
    {
        foreach (MaterialData data in materials)
        {
            if (data.Material == null)
            {
                continue;
            }

            Color highlightedColour =
                Color.Lerp(
                    data.OriginalColour,
                    tint,
                    strength);

            highlightedColour.a =
                data.OriginalColour.a;

            data.Material.SetColor(
                data.ColourProperty,
                highlightedColour);
        }
    }

    private void RestoreOriginalColours()
    {
        foreach (MaterialData data in materials)
        {
            if (data.Material == null)
            {
                continue;
            }

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

    [ContextMenu("Debug/Apply Hover Highlight")]
    private void DebugApplyHoverHighlight()
    {
        Debug.Log(
            $"{name}: manually applying hover to "
            + $"{materials.Count} cached material(s).",
            this);

        isHovered = true;
        isSelected = false;

        RefreshHighlight();
    }

    [ContextMenu("Debug/Apply Selected Highlight")]
    private void DebugApplySelectedHighlight()
    {
        Debug.Log(
            $"{name}: manually applying selection to "
            + $"{materials.Count} cached material(s).",
            this);

        isHovered = false;
        isSelected = true;

        RefreshHighlight();
    }

    [ContextMenu("Debug/Clear Highlight")]
    private void DebugClearHighlight()
    {
        isHovered = false;
        isSelected = false;

        RefreshHighlight();
    }
}