using UnityEngine;
using UnityEngine.Rendering;

public class BowArrowSpawner : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform arrowSpawnPoint;
    [SerializeField] private GameObject arrowPrefab;
    [SerializeField] private PlayerAmmoInventory ammoInventory;

    [Header("Shoot Settings")]
    [SerializeField] private float shootForce = 20f;
    [SerializeField] private float airForceMultiplier = 3f;
    [SerializeField] private float airShotOffset = 0.16f;
    [SerializeField] private bool clampNockedArrowMovement = true;
    [SerializeField] private float maxNockedArrowPullDistance = 0.41f;

    private GameObject currentArrowInstance;
    private Arrow currentArrow;
    private Collider[] bowColliders;
    private Collider[] playerColliders;
    private Vector3 arrowStartLocalPosition;
    private float stringPullPointStartLocalX;
    private bool hasPullStartX;
    private AmmoType currentArrowVisualAmmo = AmmoType.Normal;

    public Transform ArrowSpawnPoint => arrowSpawnPoint;
    public bool HasNockedArrow => currentArrow != null;
    public AmmoType CurrentAmmoType => ResolveCurrentAmmoType();
    public float CurrentGrenadeExplosionRadius => currentArrow != null ? currentArrow.GrenadeExplosionRadius : 2.25f;

    private void Awake()
    {
        EnsureAimPreview();
        RefreshBowColliders();
        CachePlayerColliders();
        IgnoreBowCollisionsWithPlayer();
    }

    private void Start()
    {
        ResolveAmmoInventory();
        RefreshBowColliders();
        CachePlayerColliders();
        IgnoreBowCollisionsWithPlayer();
        SpawnArrow();
    }

    private void LateUpdate()
    {
        UpdateNockedArrowAmmoVisual();
    }

    public void SpawnArrow()
    {
        if (currentArrowInstance != null) return;
        if (arrowPrefab == null || arrowSpawnPoint == null) return;

        ResolveAmmoInventory();
        CachePlayerColliders();

        currentArrowInstance = Instantiate(
            arrowPrefab,
            arrowSpawnPoint.position,
            arrowSpawnPoint.rotation,
            transform
        );

        currentArrow = currentArrowInstance.GetComponent<Arrow>();
        arrowStartLocalPosition = currentArrowInstance.transform.localPosition;
        stringPullPointStartLocalX = 0f;
        hasPullStartX = false;

        if (currentArrow == null)
        {
            Debug.LogWarning("Arrow Prefab hat kein Arrow-Script.");
            return;
        }

        currentArrow.PrepareForNockedArrow();
        currentArrowVisualAmmo = ResolveCurrentAmmoType();
        currentArrow.SetAmmoType(currentArrowVisualAmmo);

        Collider[] arrowColliders = currentArrowInstance.GetComponentsInChildren<Collider>();
        currentArrow.IgnoreCollisionsWith(bowColliders);
        currentArrow.IgnoreCollisionsWith(playerColliders);

        foreach (Collider bowCol in bowColliders)
        {
            foreach (Collider arrowCol in arrowColliders)
            {
                Physics.IgnoreCollision(bowCol, arrowCol, true);
            }
        }

        IgnorePlayerCollisions(arrowColliders);
    }

    public void MoveCurrentArrowToString(Transform stringPullPoint)
    {
        if (currentArrowInstance == null || stringPullPoint == null || arrowSpawnPoint == null) return;

        Vector3 pullPointLocalPosition = transform.InverseTransformPoint(stringPullPoint.position);

        if (!hasPullStartX)
        {
            stringPullPointStartLocalX = pullPointLocalPosition.x;
            hasPullStartX = true;
        }

        float pullDeltaX = pullPointLocalPosition.x - stringPullPointStartLocalX;
        if (clampNockedArrowMovement)
        {
            pullDeltaX = Mathf.Clamp(pullDeltaX, 0f, Mathf.Max(0f, maxNockedArrowPullDistance));
        }

        Vector3 arrowLocalPosition = arrowStartLocalPosition;
        arrowLocalPosition.x += pullDeltaX;

        currentArrowInstance.transform.localPosition = arrowLocalPosition;
        currentArrowInstance.transform.rotation = arrowSpawnPoint.rotation;
    }

    public void ShootCurrentArrow()
    {
        ShootCurrentArrow(shootForce);
    }

    public void ShootCurrentArrow(float force)
    {
        if (currentArrow == null) return;

        AmmoType shotAmmo = ResolveShotAmmoType();
        float adjustedForce = force;

        currentArrow.SetAmmoType(shotAmmo);

        ProceduralGameAudio.PlayArrowShot(arrowSpawnPoint != null ? arrowSpawnPoint.position : transform.position);

        if (shotAmmo == AmmoType.Air)
        {
            adjustedForce *= Mathf.Max(1f, airForceMultiplier);
            currentArrow.transform.position += arrowSpawnPoint.right * airShotOffset;
        }

        currentArrow.Shoot(arrowSpawnPoint.forward, Mathf.Max(0.01f, adjustedForce));

        currentArrow = null;
        currentArrowInstance = null;
        currentArrowVisualAmmo = AmmoType.Normal;
        hasPullStartX = false;
        stringPullPointStartLocalX = 0f;

        Invoke(nameof(SpawnArrow), 0.3f);
    }

    private AmmoType ResolveCurrentAmmoType()
    {
        ResolveAmmoInventory();
        return ammoInventory != null ? ammoInventory.CurrentAmmo : AmmoType.Normal;
    }

    private void UpdateNockedArrowAmmoVisual()
    {
        if (currentArrow == null) return;

        AmmoType selectedAmmo = ResolveCurrentAmmoType();
        if (selectedAmmo == currentArrowVisualAmmo) return;

        currentArrowVisualAmmo = selectedAmmo;
        currentArrow.SetAmmoType(selectedAmmo);
    }

    private AmmoType ResolveShotAmmoType()
    {
        ResolveAmmoInventory();
        return ammoInventory != null ? ammoInventory.ConsumeCurrentShot() : AmmoType.Normal;
    }

    private void ResolveAmmoInventory()
    {
        if (ammoInventory != null) return;

        ammoInventory = FindAnyObjectByType<PlayerAmmoInventory>();
    }

    private void CachePlayerColliders()
    {
        Transform playerRoot = ResolvePlayerRoot();
        playerColliders = playerRoot != null
            ? playerRoot.GetComponentsInChildren<Collider>(true)
            : new Collider[0];
    }

    private void IgnoreBowCollisionsWithPlayer()
    {
        if (bowColliders == null || bowColliders.Length == 0) return;
        if (playerColliders == null || playerColliders.Length == 0) return;

        for (int i = 0; i < bowColliders.Length; i++)
        {
            Collider bowCollider = bowColliders[i];
            if (bowCollider == null) continue;

            for (int j = 0; j < playerColliders.Length; j++)
            {
                Collider playerCollider = playerColliders[j];
                if (playerCollider != null && playerCollider != bowCollider)
                {
                    Physics.IgnoreCollision(bowCollider, playerCollider, true);
                }
            }
        }
    }

    private void RefreshBowColliders()
    {
        Collider[] allColliders = GetComponentsInChildren<Collider>(true);
        int validCount = 0;
        for (int i = 0; i < allColliders.Length; i++)
        {
            if (IsBowCollider(allColliders[i]))
            {
                validCount++;
            }
        }

        bowColliders = new Collider[validCount];
        int writeIndex = 0;
        for (int i = 0; i < allColliders.Length; i++)
        {
            Collider collider = allColliders[i];
            if (!IsBowCollider(collider)) continue;

            bowColliders[writeIndex] = collider;
            writeIndex++;
        }
    }

    private bool IsBowCollider(Collider collider)
    {
        if (collider == null) return false;
        if (currentArrowInstance != null && collider.transform.IsChildOf(currentArrowInstance.transform)) return false;
        if (collider.GetComponentInParent<Arrow>() != null) return false;
        if (collider.GetComponentInParent<PlayerHealth>() != null) return false;

        return true;
    }

    private Transform ResolvePlayerRoot()
    {
        if (ammoInventory != null)
        {
            return ammoInventory.transform;
        }

        PlayerHealth playerHealth = FindAnyObjectByType<PlayerHealth>();
        if (playerHealth != null)
        {
            return playerHealth.transform;
        }

        GameObject xrOrigin = GameObject.Find("XR Origin (XR Rig)") ?? GameObject.Find("XR Origin (VR)");
        if (xrOrigin != null)
        {
            return xrOrigin.transform;
        }

        return Camera.main != null ? Camera.main.transform.root : null;
    }

    private void IgnorePlayerCollisions(Collider[] arrowColliders)
    {
        if (arrowColliders == null || arrowColliders.Length == 0) return;

        CachePlayerColliders();

        for (int i = 0; i < arrowColliders.Length; i++)
        {
            Collider arrowCollider = arrowColliders[i];
            if (arrowCollider == null) continue;

            for (int j = 0; j < playerColliders.Length; j++)
            {
                Collider playerCollider = playerColliders[j];
                if (playerCollider != null)
                {
                    Physics.IgnoreCollision(arrowCollider, playerCollider, true);
                }
            }
        }
    }

    private void EnsureAimPreview()
    {
        if (GetComponent<BowAimPreview>() == null)
        {
            gameObject.AddComponent<BowAimPreview>();
        }
    }
}

[RequireComponent(typeof(BowArrowSpawner))]
public class BowAimPreview : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BowArrowSpawner arrowSpawner;

    [Header("Aim")]
    [SerializeField] private bool showAimPreview = true;
    [SerializeField] private LayerMask aimMask = ~0;
    [SerializeField] private float maxAimDistance = 30f;
    [SerializeField] private float rayStartOffset = 0.12f;

    [Header("Crosshair")]
    [SerializeField] private float crosshairRadius = 0.18f;
    [SerializeField] private float crosshairLineLength = 0.28f;
    [SerializeField] private float lineWidth = 0.015f;
    [SerializeField] private Color crosshairColor = new Color(0.35f, 1f, 1f, 0.92f);
    [SerializeField] private Color fireCrosshairColor = new Color(1f, 0.34f, 0.04f, 0.95f);
    [SerializeField] private Color airCrosshairColor = new Color(0.45f, 0.88f, 1f, 0.92f);
    [SerializeField] private Color grenadeCrosshairColor = new Color(0.78f, 1f, 0.18f, 0.95f);

    [Header("Grenade Radius")]
    [SerializeField] private int circleSegments = 72;
    [SerializeField] private Color grenadeRadiusColor = new Color(0.6f, 1f, 0.2f, 0.78f);

    private LineRenderer crosshairCircle;
    private LineRenderer crosshairHorizontal;
    private LineRenderer crosshairVertical;
    private LineRenderer grenadeRadiusCircle;
    private Material previewMaterial;

    private void Awake()
    {
        ResolveReferences();
        CreateRenderers();
    }

    private void OnEnable()
    {
        SetVisible(false);
    }

    private void LateUpdate()
    {
        ResolveReferences();

        if (!showAimPreview || arrowSpawner == null || !arrowSpawner.HasNockedArrow || arrowSpawner.ArrowSpawnPoint == null)
        {
            SetVisible(false);
            return;
        }

        AimPoint aimPoint = ResolveAimPoint(arrowSpawner.ArrowSpawnPoint);
        AmmoType ammoType = arrowSpawner.CurrentAmmoType;
        Color aimColor = ResolveAimColor(ammoType);
        SetVisible(true);
        DrawCrosshair(aimPoint.Position, aimPoint.Normal, aimColor);

        bool showGrenadeRadius = ammoType == AmmoType.Grenade;
        grenadeRadiusCircle.enabled = showGrenadeRadius;
        if (showGrenadeRadius)
        {
            DrawCircle(grenadeRadiusCircle, aimPoint.Position, aimPoint.Normal, arrowSpawner.CurrentGrenadeExplosionRadius);
            SetLineColor(grenadeRadiusCircle, grenadeRadiusColor);
        }
    }

    private void ResolveReferences()
    {
        if (arrowSpawner == null)
        {
            arrowSpawner = GetComponent<BowArrowSpawner>();
        }
    }

    private void CreateRenderers()
    {
        if (crosshairCircle != null) return;

        previewMaterial = CreatePreviewMaterial();
        crosshairCircle = CreateRenderer("Aim Crosshair Circle", circleSegments, true);
        crosshairHorizontal = CreateRenderer("Aim Crosshair Horizontal", 2, false);
        crosshairVertical = CreateRenderer("Aim Crosshair Vertical", 2, false);
        grenadeRadiusCircle = CreateRenderer("Grenade Explosion Radius", circleSegments, true);
    }

    private Material CreatePreviewMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
        {
            shader = Shader.Find("Sprites/Default");
        }

        return shader != null ? new Material(shader) : null;
    }

    private LineRenderer CreateRenderer(string objectName, int positions, bool loop)
    {
        GameObject lineObject = new GameObject(objectName);
        lineObject.transform.SetParent(transform, false);

        LineRenderer lineRenderer = lineObject.AddComponent<LineRenderer>();
        lineRenderer.useWorldSpace = true;
        lineRenderer.loop = loop;
        lineRenderer.positionCount = Mathf.Max(2, positions);
        lineRenderer.widthMultiplier = lineWidth;
        lineRenderer.material = previewMaterial;
        lineRenderer.shadowCastingMode = ShadowCastingMode.Off;
        lineRenderer.receiveShadows = false;
        SetLineColor(lineRenderer, crosshairColor);
        return lineRenderer;
    }

    private AimPoint ResolveAimPoint(Transform arrowSpawnPoint)
    {
        Vector3 direction = arrowSpawnPoint.forward.normalized;
        Vector3 origin = arrowSpawnPoint.position + direction * Mathf.Max(0f, rayStartOffset);

        RaycastHit[] hits = Physics.RaycastAll(
            origin,
            direction,
            Mathf.Max(0.1f, maxAimDistance),
            aimMask,
            QueryTriggerInteraction.Ignore
        );

        float nearestDistance = float.PositiveInfinity;
        RaycastHit nearestHit = default;
        bool hasHit = false;

        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit hit = hits[i];
            if (!IsValidAimHit(hit.collider)) continue;

            if (hit.distance < nearestDistance)
            {
                nearestDistance = hit.distance;
                nearestHit = hit;
                hasHit = true;
            }
        }

        if (hasHit)
        {
            return new AimPoint(nearestHit.point, nearestHit.normal);
        }

        return new AimPoint(origin + direction * Mathf.Max(0.1f, maxAimDistance), -direction);
    }

    private bool IsValidAimHit(Collider hitCollider)
    {
        if (hitCollider == null) return false;
        if (hitCollider.transform.IsChildOf(transform)) return false;
        if (hitCollider.GetComponentInParent<Arrow>() != null) return false;
        if (hitCollider.GetComponentInParent<PlayerHealth>() != null) return false;

        return true;
    }

    private void DrawCrosshair(Vector3 center, Vector3 normal, Color color)
    {
        DrawCircle(crosshairCircle, center, normal, crosshairRadius);

        GetPlaneAxes(normal, out Vector3 horizontalAxis, out Vector3 verticalAxis);
        float halfLength = Mathf.Max(0.01f, crosshairLineLength) * 0.5f;

        crosshairHorizontal.SetPosition(0, center - horizontalAxis * halfLength);
        crosshairHorizontal.SetPosition(1, center + horizontalAxis * halfLength);
        crosshairVertical.SetPosition(0, center - verticalAxis * halfLength);
        crosshairVertical.SetPosition(1, center + verticalAxis * halfLength);

        SetLineColor(crosshairCircle, color);
        SetLineColor(crosshairHorizontal, color);
        SetLineColor(crosshairVertical, color);
    }

    private Color ResolveAimColor(AmmoType ammoType)
    {
        switch (ammoType)
        {
            case AmmoType.Fire:
                return fireCrosshairColor;
            case AmmoType.Air:
                return airCrosshairColor;
            case AmmoType.Grenade:
                return grenadeCrosshairColor;
            default:
                return crosshairColor;
        }
    }

    private void DrawCircle(LineRenderer lineRenderer, Vector3 center, Vector3 normal, float radius)
    {
        if (lineRenderer == null) return;

        GetPlaneAxes(normal, out Vector3 horizontalAxis, out Vector3 verticalAxis);
        int segmentCount = Mathf.Max(12, circleSegments);
        lineRenderer.positionCount = segmentCount;

        float safeRadius = Mathf.Max(0.01f, radius);
        for (int i = 0; i < segmentCount; i++)
        {
            float angle = (i / (float)segmentCount) * Mathf.PI * 2f;
            Vector3 point = center
                + horizontalAxis * Mathf.Cos(angle) * safeRadius
                + verticalAxis * Mathf.Sin(angle) * safeRadius;
            lineRenderer.SetPosition(i, point);
        }
    }

    private void GetPlaneAxes(Vector3 normal, out Vector3 horizontalAxis, out Vector3 verticalAxis)
    {
        Vector3 safeNormal = normal.sqrMagnitude > 0.0001f ? normal.normalized : Vector3.up;
        horizontalAxis = Vector3.Cross(safeNormal, Vector3.up);
        if (horizontalAxis.sqrMagnitude < 0.0001f)
        {
            horizontalAxis = Vector3.Cross(safeNormal, Vector3.right);
        }

        horizontalAxis.Normalize();
        verticalAxis = Vector3.Cross(horizontalAxis, safeNormal).normalized;
    }

    private void SetVisible(bool visible)
    {
        if (crosshairCircle != null) crosshairCircle.enabled = visible;
        if (crosshairHorizontal != null) crosshairHorizontal.enabled = visible;
        if (crosshairVertical != null) crosshairVertical.enabled = visible;
        if (grenadeRadiusCircle != null) grenadeRadiusCircle.enabled = false;
    }

    private void SetLineColor(LineRenderer lineRenderer, Color color)
    {
        if (lineRenderer == null) return;

        lineRenderer.startColor = color;
        lineRenderer.endColor = color;
        if (lineRenderer.material != null)
        {
            lineRenderer.material.color = color;
        }
    }

    private readonly struct AimPoint
    {
        public AimPoint(Vector3 position, Vector3 normal)
        {
            Position = position;
            Normal = normal;
        }

        public Vector3 Position { get; }
        public Vector3 Normal { get; }
    }
}
