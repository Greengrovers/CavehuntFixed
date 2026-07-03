using System.Collections.Generic;
using UnityEngine;

public class EnemyPickupDropper : MonoBehaviour
{
    [SerializeField, Range(0f, 1f)] private float dropChance = 0.25f;
    [SerializeField] private GameObject[] pickupPrefabs;
    [SerializeField] private float spawnLift = 0f;
    [SerializeField] private float horizontalJitter = 0.2f;
    [SerializeField] private float groundProbeStartHeight = 2f;
    [SerializeField] private float groundProbeDistance = 50f;

    [Header("Boundary Drop")]
    [SerializeField] private bool dropInsidePlayerBoundary = true;
    [SerializeField] private string geodeBoundaryPrefix = "Geode Boundary";
    [SerializeField] private float boundaryRadius = 100f;
    [SerializeField] private float boundaryPadding = 8f;
    [SerializeField] private float minimumPlayerDropDistance = 10f;
    [SerializeField] private int dropPositionAttempts = 12;

    [Header("Pickup Marker")]
    [SerializeField] private bool showPickupArrow = true;
    [SerializeField] private Color pickupArrowColor = new Color(0.45f, 0.9f, 1f, 1f);
    [SerializeField] private float pickupArrowHeight = 2.4f;
    [SerializeField] private float pickupArrowScale = 0.9f;

    [Header("Trap")]
    [SerializeField] private bool spawnTraps = false;
    [SerializeField, Range(0f, 1f)] private float trapSpawnChance = 0.25f;
    [SerializeField] private GameObject spikeTrapTemplate;
    [SerializeField] private string spikeTrapSceneObjectName = "Trap";
    [SerializeField] private float trapPlayerSpacing = 6f;
    [SerializeField] private float trapPickupSpacing = 4f;
    [SerializeField] private float trapSpawnLift = 0f;
    [SerializeField] private float trapDamage = 1f;

    [Header("Healing Geode")]
    [SerializeField, Range(0f, 1f)] private float healingGeodeSpawnChance = 0.25f;
    [SerializeField] private GameObject healingGeodeTemplate;
    [SerializeField] private string healingGeodeResourcePath = "Geodes/HealingGeode";
    [SerializeField] private float healingGeodePlayerSpacing = 4f;
    [SerializeField] private float healingGeodePickupSpacing = 3f;
    [SerializeField] private float healingGeodeSpawnLift = 0f;

    private Transform cachedPlayerTarget;
    private GameObject cachedSpikeTrapTemplate;
    private GameObject cachedHealingGeodeTemplate;
    private bool hasCachedArenaBoundary;
    private Vector3 cachedArenaCenter;
    private float cachedArenaRadius;

    private void Awake()
    {
        EnsurePickupPrefabs();
        ResolveSpikeTrapTemplate();
        ResolveHealingGeodeTemplate();
    }

    private void OnValidate()
    {
        dropChance = Mathf.Clamp01(dropChance);
        trapSpawnChance = Mathf.Clamp01(trapSpawnChance);
        healingGeodeSpawnChance = Mathf.Clamp01(healingGeodeSpawnChance);
    }

    public void Configure(float chance, GameObject[] prefabs = null)
    {
        dropChance = Mathf.Clamp01(chance);

        if (prefabs != null && prefabs.Length > 0)
        {
            pickupPrefabs = prefabs;
        }

        EnsurePickupPrefabs();
    }

    public float HealingGeodeSpawnChance
    {
        get => healingGeodeSpawnChance;
        set => healingGeodeSpawnChance = Mathf.Clamp01(value);
    }

    public float HealingGeodeSpawnChancePercent
    {
        get => healingGeodeSpawnChance * 100f;
        set => HealingGeodeSpawnChance = value / 100f;
    }


    public GameObject TryDrop(Vector3 enemyPosition)
    {
        EnsurePickupPrefabs();

        if (pickupPrefabs == null || pickupPrefabs.Length == 0) return null;
        if (Random.value > dropChance) return null;

        List<GameObject> validPrefabs = new List<GameObject>();
        for (int i = 0; i < pickupPrefabs.Length; i++)
        {
            if (pickupPrefabs[i] != null)
            {
                validPrefabs.Add(pickupPrefabs[i]);
            }
        }

        if (validPrefabs.Count == 0) return null;

        GameObject prefab = validPrefabs[Random.Range(0, validPrefabs.Count)];
        Vector3 spawnPosition = ResolveDropPosition(enemyPosition);
        GameObject pickup = Instantiate(prefab, spawnPosition, Quaternion.identity);
        CavehuntRuntimeCleanup.Mark(pickup, CavehuntRuntimeCleanup.CleanupKind.Pickup);

        PickupDropAnimation dropAnimation = pickup.GetComponent<PickupDropAnimation>();
        if (dropAnimation != null)
        {
            dropAnimation.PlayDrop();
        }

        AttachPickupArrow(pickup);
        ProceduralGameAudio.PlayPickupDrop(spawnPosition);
        TrySpawnTrapBetweenPlayerAndPickup(spawnPosition);
        TrySpawnHealingGeodeBetweenPlayerAndPickup(spawnPosition);

        return pickup;
    }

    private Vector3 ResolveDropPosition(Vector3 enemyPosition)
    {
        if (!dropInsidePlayerBoundary)
        {
            Vector2 jitter = Random.insideUnitCircle * horizontalJitter;
            return ResolveGroundDropPosition(enemyPosition, jitter);
        }

        Transform playerTarget = ResolvePlayerTarget();
        if (!TryResolveDropBoundary(playerTarget, out Vector3 center, out float maxRadius))
        {
            Vector2 jitter = Random.insideUnitCircle * horizontalJitter;
            return ResolveGroundDropPosition(enemyPosition, jitter);
        }

        float minRadius = Mathf.Clamp(minimumPlayerDropDistance, 0f, maxRadius - 0.5f);
        int attempts = Mathf.Max(1, dropPositionAttempts);

        for (int i = 0; i < attempts; i++)
        {
            Vector2 offset = Random.insideUnitCircle;
            if (offset.sqrMagnitude < 0.0001f)
            {
                float angle = Random.value * Mathf.PI * 2f;
                offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            }

            float distance = Mathf.Lerp(minRadius, maxRadius, Mathf.Sqrt(Random.value));
            offset = offset.normalized * distance;
            Vector3 horizontalPosition = new Vector3(center.x + offset.x, enemyPosition.y, center.z + offset.y);
            Vector3 resolvedPosition = ResolveGroundDropPositionInsideBoundary(horizontalPosition, Vector2.zero, spawnLift, center, maxRadius);

            if (IsInsideBoundary(resolvedPosition, center, maxRadius))
            {
                return resolvedPosition;
            }
        }

        Vector3 fallbackOffset = (enemyPosition - center);
        fallbackOffset.y = 0f;
        if (fallbackOffset.sqrMagnitude < minRadius * minRadius)
        {
            fallbackOffset = Random.insideUnitSphere;
            fallbackOffset.y = 0f;
        }

        fallbackOffset = Vector3.ClampMagnitude(fallbackOffset, maxRadius);
        if (fallbackOffset.magnitude < minRadius)
        {
            fallbackOffset = fallbackOffset.normalized * minRadius;
        }

        Vector3 fallbackPosition = new Vector3(center.x + fallbackOffset.x, enemyPosition.y, center.z + fallbackOffset.z);
        return ResolveGroundDropPositionInsideBoundary(fallbackPosition, Vector2.zero, spawnLift, center, maxRadius);
    }


    private Vector3 ResolveGroundDropPositionInsideBoundary(Vector3 horizontalPosition, Vector2 jitter, float lift, Vector3 center, float maxRadius)
    {
        Vector3 resolvedPosition = ResolveGroundDropPosition(horizontalPosition, jitter, lift);
        if (IsInsideBoundary(resolvedPosition, center, maxRadius))
        {
            return resolvedPosition;
        }

        Vector3 clampedHorizontalPosition = ClampHorizontalToBoundary(resolvedPosition, center, maxRadius);
        Vector3 clampedResolvedPosition = ResolveGroundDropPosition(clampedHorizontalPosition, Vector2.zero, lift);
        if (IsInsideBoundary(clampedResolvedPosition, center, maxRadius))
        {
            return clampedResolvedPosition;
        }

        return ClampHorizontalToBoundary(clampedResolvedPosition, center, maxRadius);
    }

    private static Vector3 ClampHorizontalToBoundary(Vector3 position, Vector3 center, float maxRadius)
    {
        Vector3 offset = position - center;
        offset.y = 0f;

        float safeRadius = Mathf.Max(0.01f, maxRadius);
        if (offset.sqrMagnitude <= safeRadius * safeRadius)
        {
            return position;
        }

        Vector3 clampedOffset = offset.normalized * safeRadius;
        return new Vector3(center.x + clampedOffset.x, position.y, center.z + clampedOffset.z);
    }
    private bool TryResolveDropBoundary(Transform playerTarget, out Vector3 center, out float maxRadius)
    {
        if (TryResolveGeodeArenaBoundary(out center, out maxRadius))
        {
            return true;
        }

        if (playerTarget != null)
        {
            center = playerTarget.position;
            maxRadius = Mathf.Max(1f, boundaryRadius - Mathf.Max(0f, boundaryPadding));
            return true;
        }

        center = Vector3.zero;
        maxRadius = 0f;
        return false;
    }

    private bool TryResolveGeodeArenaBoundary(out Vector3 center, out float maxRadius)
    {
        if (!hasCachedArenaBoundary)
        {
            RecalculateGeodeArenaBoundary();
        }

        center = cachedArenaCenter;
        maxRadius = cachedArenaRadius;
        return cachedArenaRadius > 1f;
    }

    private void RecalculateGeodeArenaBoundary()
    {
        hasCachedArenaBoundary = true;
        cachedArenaCenter = Vector3.zero;
        cachedArenaRadius = 0f;

        if (string.IsNullOrWhiteSpace(geodeBoundaryPrefix)) return;

        Transform[] transforms = FindObjectsByType<Transform>(FindObjectsInactive.Include);
        Vector3 sum = Vector3.zero;
        int count = 0;

        for (int i = 0; i < transforms.Length; i++)
        {
            Transform candidate = transforms[i];
            if (candidate == null) continue;
            if (!candidate.name.StartsWith(geodeBoundaryPrefix, System.StringComparison.OrdinalIgnoreCase)) continue;

            sum += candidate.position;
            count++;
        }

        if (count < 3) return;

        Vector3 arenaCenter = sum / count;
        float nearestBoundaryDistance = float.PositiveInfinity;

        for (int i = 0; i < transforms.Length; i++)
        {
            Transform candidate = transforms[i];
            if (candidate == null) continue;
            if (!candidate.name.StartsWith(geodeBoundaryPrefix, System.StringComparison.OrdinalIgnoreCase)) continue;

            Vector2 delta = new Vector2(candidate.position.x - arenaCenter.x, candidate.position.z - arenaCenter.z);
            float distance = delta.magnitude;
            if (distance > 0.1f && distance < nearestBoundaryDistance)
            {
                nearestBoundaryDistance = distance;
            }
        }

        if (float.IsInfinity(nearestBoundaryDistance)) return;

        cachedArenaCenter = arenaCenter;
        cachedArenaRadius = Mathf.Max(1f, nearestBoundaryDistance - Mathf.Max(0f, boundaryPadding));
    }

    private Vector3 ResolveGroundDropPosition(Vector3 horizontalPosition, Vector2 jitter)
    {
        return ResolveGroundDropPosition(horizontalPosition, jitter, spawnLift);
    }

    private Vector3 ResolveGroundDropPosition(Vector3 horizontalPosition, Vector2 jitter, float lift)
    {
        float probeBaseY = ResolveGroundProbeBaseY(horizontalPosition.y);
        Vector3 horizontalDropPosition = new Vector3(horizontalPosition.x + jitter.x, probeBaseY, horizontalPosition.z + jitter.y);
        Vector3 probeStart = horizontalDropPosition + Vector3.up * Mathf.Max(0.1f, groundProbeStartHeight);
        float probeDistance = Mathf.Max(0.1f, groundProbeStartHeight + groundProbeDistance);

        RaycastHit[] hits = Physics.RaycastAll(
            probeStart,
            Vector3.down,
            probeDistance,
            ~0,
            QueryTriggerInteraction.Ignore
        );

        float nearestDistance = float.PositiveInfinity;
        RaycastHit nearestGroundHit = default;
        bool foundGround = false;

        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit hit = hits[i];
            if (!IsGroundDropSurface(hit.collider)) continue;

            if (hit.distance < nearestDistance)
            {
                nearestDistance = hit.distance;
                nearestGroundHit = hit;
                foundGround = true;
            }
        }

        if (foundGround)
        {
            return nearestGroundHit.point + nearestGroundHit.normal * Mathf.Max(0f, lift);
        }

        return horizontalDropPosition + Vector3.up * Mathf.Max(0f, lift);
    }

    private float ResolveGroundProbeBaseY(float fallbackY)
    {
        Transform playerTarget = ResolvePlayerTarget();
        if (playerTarget != null)
        {
            return playerTarget.position.y;
        }

        return Mathf.Min(fallbackY, 0f);
    }

    private bool IsGroundDropSurface(Collider candidate)
    {
        if (candidate == null) return false;
        if (candidate.transform.IsChildOf(transform)) return false;
        if (candidate.GetComponentInParent<BatEnemy>() != null) return false;
        if (candidate.GetComponentInParent<Damageable>() != null) return false;
        if (candidate.GetComponentInParent<PlayerHealth>() != null) return false;
        if (candidate.GetComponentInParent<AmmoPickup>() != null) return false;
        if (candidate.GetComponentInParent<SpikeTrap>() != null) return false;
        if (candidate.GetComponentInParent<HealingGeode>() != null) return false;

        return true;
    }

    private void AttachPickupArrow(GameObject pickup)
    {
        if (!showPickupArrow || pickup == null) return;

        PickupLocationArrow.Attach(
            pickup.transform,
            pickupArrowColor,
            pickupArrowHeight,
            pickupArrowScale
        );
    }

    private void TrySpawnTrapBetweenPlayerAndPickup(Vector3 pickupPosition)
    {
        if (!spawnTraps) return;
        if (trapSpawnChance <= 0f || Random.value > trapSpawnChance) return;

        Transform playerTarget = ResolvePlayerTarget();
        GameObject template = ResolveSpikeTrapTemplate();
        if (playerTarget == null || template == null) return;

        Vector3 playerPosition = playerTarget.position;
        Vector3 toPickup = pickupPosition - playerPosition;
        toPickup.y = 0f;

        float distance = toPickup.magnitude;
        if (distance < 1f) return;

        Vector3 direction = toPickup / distance;
        float lower = Mathf.Clamp01(trapPlayerSpacing / distance);
        float upper = Mathf.Clamp01(1f - trapPickupSpacing / distance);
        float t = lower < upper ? Random.Range(lower, upper) : 0.5f;

        Vector3 trapHorizontalPosition = playerPosition + direction * (distance * t);
        trapHorizontalPosition.y = pickupPosition.y;
        Vector3 trapPosition = ResolveGroundDropPosition(trapHorizontalPosition, Vector2.zero, 0f) + Vector3.up * Mathf.Max(0f, trapSpawnLift);
        Quaternion trapRotation = template.transform.rotation;

        GameObject trapObject = Instantiate(template, trapPosition, trapRotation);
        trapObject.name = "Spike Trap";
        CavehuntRuntimeCleanup.Mark(trapObject, CavehuntRuntimeCleanup.CleanupKind.Trap);
        trapObject.SetActive(true);

        SpikeTrap trap = trapObject.GetComponent<SpikeTrap>();
        if (trap == null)
        {
            trap = trapObject.AddComponent<SpikeTrap>();
        }

        trap.Configure(trapDamage);
    }

    private void TrySpawnHealingGeodeBetweenPlayerAndPickup(Vector3 pickupPosition)
    {
        if (healingGeodeSpawnChance <= 0f || Random.value > healingGeodeSpawnChance) return;

        Transform playerTarget = ResolvePlayerTarget();
        GameObject template = ResolveHealingGeodeTemplate();
        if (playerTarget == null || template == null) return;

        Vector3 playerPosition = playerTarget.position;
        Vector3 toPickup = pickupPosition - playerPosition;
        toPickup.y = 0f;

        float distance = toPickup.magnitude;
        if (distance < 1f) return;

        Vector3 direction = toPickup / distance;
        float lower = Mathf.Clamp01(healingGeodePlayerSpacing / distance);
        float upper = Mathf.Clamp01(1f - healingGeodePickupSpacing / distance);
        float t = lower < upper ? Random.Range(lower, upper) : 0.5f;

        Vector3 geodeHorizontalPosition = playerPosition + direction * (distance * t);
        geodeHorizontalPosition.y = pickupPosition.y;
        Vector3 geodePosition = ResolveGroundDropPosition(geodeHorizontalPosition, Vector2.zero, 0f) + Vector3.up * Mathf.Max(0f, healingGeodeSpawnLift);
        Quaternion geodeRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

        GameObject geodeObject = Instantiate(template, geodePosition, geodeRotation);
        geodeObject.name = "Geode Boundary Healing";
        CavehuntRuntimeCleanup.Mark(geodeObject, CavehuntRuntimeCleanup.CleanupKind.Pickup);
        geodeObject.SetActive(true);

        HealingGeode healingGeode = geodeObject.GetComponent<HealingGeode>();
        if (healingGeode == null)
        {
            Debug.LogWarning("Healing geode template has no HealingGeode component. Inspector healing values were not applied.", geodeObject);
        }
    }

    private GameObject ResolveSpikeTrapTemplate()
    {
        if (spikeTrapTemplate != null) return spikeTrapTemplate;
        if (cachedSpikeTrapTemplate != null) return cachedSpikeTrapTemplate;

        GameObject trapParent = GameObject.Find("Trap");
        if (trapParent != null)
        {
            cachedSpikeTrapTemplate = CreateSpikeTrapTemplate(trapParent);
            return cachedSpikeTrapTemplate;
        }

        if (!string.IsNullOrEmpty(spikeTrapSceneObjectName))
        {
            cachedSpikeTrapTemplate = GameObject.Find(spikeTrapSceneObjectName);
            if (cachedSpikeTrapTemplate != null && cachedSpikeTrapTemplate.transform.parent != null)
            {
                cachedSpikeTrapTemplate = cachedSpikeTrapTemplate.transform.parent.gameObject;
            }

            if (cachedSpikeTrapTemplate != null)
            {
                cachedSpikeTrapTemplate = CreateSpikeTrapTemplate(cachedSpikeTrapTemplate);
            }
        }

        return cachedSpikeTrapTemplate;
    }


    private GameObject ResolveHealingGeodeTemplate()
    {
        if (healingGeodeTemplate != null) return healingGeodeTemplate;
        if (cachedHealingGeodeTemplate != null) return cachedHealingGeodeTemplate;

        if (!string.IsNullOrEmpty(healingGeodeResourcePath))
        {
            cachedHealingGeodeTemplate = Resources.Load<GameObject>(healingGeodeResourcePath);
        }

        return cachedHealingGeodeTemplate;
    }
    private GameObject CreateSpikeTrapTemplate(GameObject source)
    {
        GameObject template = Instantiate(source);
        template.name = "Spike Trap Template";
        template.SetActive(false);
        return template;
    }

    private Transform ResolvePlayerTarget()
    {
        if (cachedPlayerTarget != null) return cachedPlayerTarget;

        PlayerHealth playerHealth = FindAnyObjectByType<PlayerHealth>();
        if (playerHealth != null)
        {
            cachedPlayerTarget = playerHealth.transform;
            return cachedPlayerTarget;
        }

        GameObject xrOrigin = GameObject.Find("XR Origin (XR Rig)") ?? GameObject.Find("XR Origin (VR)");
        if (xrOrigin != null)
        {
            cachedPlayerTarget = xrOrigin.transform;
            return cachedPlayerTarget;
        }

        Camera mainCamera = Camera.main;
        if (mainCamera != null)
        {
            cachedPlayerTarget = mainCamera.transform;
        }

        return cachedPlayerTarget;
    }

    private static bool IsInsideBoundary(Vector3 position, Vector3 center, float maxRadius)
    {
        Vector2 delta = new Vector2(position.x - center.x, position.z - center.z);
        return delta.sqrMagnitude <= maxRadius * maxRadius;
    }

    private void EnsurePickupPrefabs()
    {
        if (pickupPrefabs != null && pickupPrefabs.Length > 0) return;

        pickupPrefabs = Resources.LoadAll<GameObject>("Pickups");
    }
}
