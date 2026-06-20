using UnityEngine;

[RequireComponent(typeof(Damageable))]
public class EnemyHealthBar : MonoBehaviour
{
    [SerializeField] private Vector2 size = new Vector2(2.4f, 0.24f);
    [SerializeField] private float heightPadding = 0.85f;
    [SerializeField] private float distanceScale = 0.04f;
    [SerializeField] private Vector2 distanceScaleRange = new Vector2(1f, 4f);
    [SerializeField] private Color backgroundColor = new Color(0.05f, 0.02f, 0.02f, 1f);
    [SerializeField] private Color fillColor = new Color(0.1f, 1f, 0.2f, 1f);

    private Damageable damageable;
    private Transform barRoot;
    private Transform fillBar;
    private Renderer backgroundRenderer;
    private Renderer fillRenderer;
    private Material backgroundMaterial;
    private Material fillMaterial;
    private BatEnemy batEnemy;
    private Transform viewerTarget;

    private void Awake()
    {
        damageable = GetComponent<Damageable>();
        batEnemy = GetComponent<BatEnemy>();
        EnsureBar();
    }

    public void Configure(Transform target)
    {
        viewerTarget = target;
    }

    private void LateUpdate()
    {
        if (damageable == null)
        {
            damageable = GetComponent<Damageable>();
        }

        if (batEnemy == null)
        {
            batEnemy = GetComponent<BatEnemy>();
        }

        EnsureBar();
        UpdateVisibilityAndFill();
        PositionAboveEnemy();
        Transform cameraTransform = ResolveViewerTarget();
        FaceCamera(cameraTransform);
        ScaleForDistance(cameraTransform);
    }

    private void EnsureBar()
    {
        if (barRoot != null) return;

        barRoot = new GameObject("Health Bar").transform;
        barRoot.localScale = Vector3.one;

        Transform background = CreateBarSegment("Health Bar Background", backgroundColor, out backgroundRenderer);
        background.SetParent(barRoot);
        background.localPosition = Vector3.zero;
        background.localRotation = Quaternion.identity;
        background.localScale = new Vector3(size.x, size.y, 0.04f);

        fillBar = CreateBarSegment("Health Bar Fill", fillColor, out fillRenderer);
        fillBar.SetParent(barRoot);
        fillBar.localRotation = Quaternion.identity;

        backgroundMaterial = backgroundRenderer.sharedMaterial;
        fillMaterial = fillRenderer.sharedMaterial;

        if (backgroundRenderer != null) backgroundRenderer.enabled = false;
        if (fillRenderer != null) fillRenderer.enabled = false;
    }

    private Transform CreateBarSegment(string segmentName, Color color, out Renderer segmentRenderer)
    {
        GameObject segment = GameObject.CreatePrimitive(PrimitiveType.Cube);
        segment.name = segmentName;

        Collider segmentCollider = segment.GetComponent<Collider>();
        if (segmentCollider != null)
        {
            Destroy(segmentCollider);
        }

        segmentRenderer = segment.GetComponent<Renderer>();
        if (segmentRenderer != null)
        {
            Material material = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Standard"))
            {
                color = color
            };

            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", color * 1.8f);
            material.EnableKeyword("_EMISSION");
            material.renderQueue = 4000;

            segmentRenderer.sharedMaterial = material;
        }

        return segment.transform;
    }

    private void UpdateVisibilityAndFill()
    {
        float healthFraction = damageable != null && damageable.MaxHealth > 0f
            ? Mathf.Clamp01(damageable.CurrentHealth / damageable.MaxHealth)
            : 0f;

        bool visible = healthFraction > 0f && (batEnemy == null || batEnemy.IsPresenting);
        if (backgroundRenderer != null) backgroundRenderer.enabled = visible;
        if (fillRenderer != null) fillRenderer.enabled = visible;

        if (!visible || fillBar == null) return;

        fillBar.localScale = new Vector3(size.x * healthFraction, size.y * 0.72f, 0.055f);
        fillBar.localPosition = new Vector3((healthFraction - 1f) * size.x * 0.5f, 0f, 0.035f);

        if (fillMaterial != null)
        {
            Color healthColor = Color.Lerp(Color.red, fillColor, healthFraction);
            if (fillMaterial.HasProperty("_BaseColor")) fillMaterial.SetColor("_BaseColor", healthColor);
            if (fillMaterial.HasProperty("_Color")) fillMaterial.SetColor("_Color", healthColor);
        }
    }

    private void PositionAboveEnemy()
    {
        if (barRoot == null) return;

        if (TryGetBodyBounds(out Bounds bounds))
        {
            Vector3 worldPosition = bounds.center + Vector3.up * (bounds.extents.y + heightPadding);
            barRoot.position = worldPosition;
        }
        else
        {
            barRoot.localPosition = Vector3.up * 1.2f;
        }
    }

    private void FaceCamera(Transform cameraTransform)
    {
        if (barRoot == null) return;
        if (cameraTransform == null) return;

        Vector3 direction = cameraTransform.position - barRoot.position;
        if (direction.sqrMagnitude <= 0.0001f) return;

        barRoot.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
    }

    private void ScaleForDistance(Transform cameraTransform)
    {
        if (barRoot == null || cameraTransform == null) return;

        float scale = Mathf.Clamp(
            Vector3.Distance(barRoot.position, cameraTransform.position) * distanceScale,
            distanceScaleRange.x,
            distanceScaleRange.y);

        barRoot.localScale = Vector3.one * scale;
    }

    private Transform ResolveViewerTarget()
    {
        if (viewerTarget != null) return viewerTarget;
        if (Camera.main != null) return Camera.main.transform;

        GameObject mainCamera = GameObject.Find("Main Camera");
        if (mainCamera != null) return mainCamera.transform;

        Camera[] cameras = Camera.allCameras;
        return cameras.Length > 0 ? cameras[0].transform : null;
    }

    private bool TryGetBodyBounds(out Bounds bounds)
    {
        bounds = default;
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        bool hasBounds = false;

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer bodyRenderer = renderers[i];
            if (bodyRenderer == null || bodyRenderer.transform.IsChildOf(barRoot)) continue;

            if (!hasBounds)
            {
                bounds = bodyRenderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(bodyRenderer.bounds);
            }
        }

        return hasBounds;
    }

    private void OnDestroy()
    {
        if (barRoot != null) Destroy(barRoot.gameObject);
        if (backgroundMaterial != null) Destroy(backgroundMaterial);
        if (fillMaterial != null) Destroy(fillMaterial);
    }
}
