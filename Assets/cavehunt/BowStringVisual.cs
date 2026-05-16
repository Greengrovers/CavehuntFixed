using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(LineRenderer))]
public class BowStringVisual : MonoBehaviour
{
    [SerializeField] private Transform stringTop;
    [SerializeField] private Transform stringMiddle;
    [SerializeField] private Transform stringBottom;
    [SerializeField] private float stringWidth = 0.008f;

    private LineRenderer lineRenderer;
    private MeshRenderer staticMeshRenderer;

    private void Awake()
    {
        Initialize();
    }

    private void OnEnable()
    {
        Initialize();
        UpdateString();
    }

    private void OnValidate()
    {
        Initialize();
        UpdateString();
    }

    private void LateUpdate()
    {
        UpdateString();
    }

    private void Initialize()
    {
        if (lineRenderer == null)
        {
            lineRenderer = GetComponent<LineRenderer>();
        }

        if (staticMeshRenderer == null)
        {
            staticMeshRenderer = GetComponent<MeshRenderer>();
        }

        if (staticMeshRenderer != null)
        {
            staticMeshRenderer.enabled = false;
        }

        if (lineRenderer == null) return;

        lineRenderer.enabled = true;
        lineRenderer.positionCount = 3;
        lineRenderer.useWorldSpace = true;
        lineRenderer.startWidth = stringWidth;
        lineRenderer.endWidth = stringWidth;
        lineRenderer.startColor = Color.white;
        lineRenderer.endColor = Color.white;

        if (lineRenderer.sharedMaterial == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }

            if (shader != null)
            {
                lineRenderer.material = new Material(shader) { color = Color.white };
            }
        }
    }

    private void UpdateString()
    {
        if (lineRenderer == null) return;
        if (stringTop == null || stringMiddle == null || stringBottom == null) return;

        lineRenderer.SetPosition(0, stringTop.position);
        lineRenderer.SetPosition(1, stringMiddle.position);
        lineRenderer.SetPosition(2, stringBottom.position);
    }
}