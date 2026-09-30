using UnityEngine;

public sealed class DirectionAltererPreview : MonoBehaviour
{
    [SerializeField]
    private float heightAboveDwarf = 6f;

    [SerializeField]
    private float arrowLength = 3f;

    [SerializeField]
    private float arrowHeadLength = 0.9f;

    [SerializeField]
    private float arrowHeadWidth = 0.65f;

    [SerializeField]
    private float lineWidth = 0.18f;

    [SerializeField]
    private Color colour =
        new(1f, 0.8f, 0.05f, 1f);

    private DwarfAgent target;
    private DirectionAltererTurn turn;

    private GameObject previewRoot;
    private LineRenderer shaft;
    private LineRenderer leftWing;
    private LineRenderer rightWing;
    private Material lineMaterial;

    public void Show(
        DwarfAgent dwarf,
        DirectionAltererTurn previewTurn)
    {
        if (dwarf == null ||
            !dwarf.IsActive)
        {
            Hide();
            return;
        }

        EnsureCreated();

        target = dwarf;
        turn = previewTurn;

        previewRoot.SetActive(true);
        RefreshGeometry();
    }

    public void Hide()
    {
        target = null;

        if (previewRoot != null)
        {
            previewRoot.SetActive(false);
        }
    }

    private void LateUpdate()
    {
        if (target == null ||
            !target.IsActive)
        {
            Hide();
            return;
        }

        RefreshGeometry();
    }

    private void EnsureCreated()
    {
        if (previewRoot != null)
        {
            return;
        }

        previewRoot =
            new GameObject("Direction Alterer Preview");

        previewRoot.transform.SetParent(
            transform,
            false);

        Shader shader =
            Shader.Find("Sprites/Default");

        lineMaterial =
            new Material(shader)
            {
                name = "Direction Alterer Preview Material",
                hideFlags = HideFlags.HideAndDontSave
            };

        shaft = CreateLine("Shaft");
        leftWing = CreateLine("Left Wing");
        rightWing = CreateLine("Right Wing");
    }

    private LineRenderer CreateLine(
        string lineName)
    {
        GameObject lineObject =
            new(lineName);

        lineObject.transform.SetParent(
            previewRoot.transform,
            false);

        LineRenderer line =
            lineObject.AddComponent<LineRenderer>();

        line.useWorldSpace = true;
        line.positionCount = 2;
        line.startWidth = lineWidth;
        line.endWidth = lineWidth;
        line.startColor = colour;
        line.endColor = colour;
        line.material = lineMaterial;
        line.numCapVertices = 4;
        line.shadowCastingMode =
            UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;

        return line;
    }

    private void RefreshGeometry()
    {
        PuzzleSide outputSide =
            DirectionUtility.ApplyTurn(
                target.Facing,
                turn);

        Vector3 direction =
            (Vector3)DirectionUtility.ToVector(
                outputSide);

        if (direction.sqrMagnitude <= 0.0001f)
        {
            Hide();
            return;
        }

        direction.Normalize();

        Vector3 origin =
            target.transform.position +
            Vector3.up * heightAboveDwarf;

        Vector3 tip =
            origin +
            direction * arrowLength;

        Vector3 sideways =
            new(-direction.z, 0f, direction.x);

        Vector3 headBase =
            tip -
            direction * arrowHeadLength;

        shaft.SetPosition(0, origin);
        shaft.SetPosition(1, tip);

        leftWing.SetPosition(0, tip);
        leftWing.SetPosition(
            1,
            headBase +
            sideways * arrowHeadWidth);

        rightWing.SetPosition(0, tip);
        rightWing.SetPosition(
            1,
            headBase -
            sideways * arrowHeadWidth);
    }

    private void OnDestroy()
    {
        if (lineMaterial != null)
        {
            Destroy(lineMaterial);
        }
    }
}
