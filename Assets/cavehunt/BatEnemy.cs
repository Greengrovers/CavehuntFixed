using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Damageable))]
public class BatEnemy : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform playerTarget;
    [SerializeField] private Transform bulletSpawnPoint;
    [SerializeField] private Transform[] eyeBulletSpawnPoints;
    [SerializeField] private Transform[] ceilingSpawnPoints;
    [SerializeField] private Material bulletMaterial;

    [Header("Movement")]
    [SerializeField] private float descendSpeed = 1.5f;
    [SerializeField] private float groundY = 0.05f;
    [SerializeField] private float groundHitDamage = 1f;
    [SerializeField] private float respawnDelay = 2f;
    [SerializeField] private bool descendTowardGround = true;
    [SerializeField] private bool respawnOnDeath = true;

    [Header("Near Ground Sound")]
    [SerializeField] private bool playNearGroundSound = true;
    [SerializeField] private string nearGroundSoundResourcePath = "Audio/BatCaveSound";
    [SerializeField] private float nearGroundSoundHeight = 16f;
    [SerializeField, Range(0f, 2f)] private float nearGroundSoundVolume = 1.8f;
    [SerializeField] private float nearGroundSoundMinDistance = 2f;
    [SerializeField] private float nearGroundSoundMaxDistance = 80f;

    [Header("Spawn")]
    [SerializeField] private float minPlayerSpawnDistance = 1.5f;
    [SerializeField] private float spawnPositionJitter = 0.35f;
    [SerializeField] private float spawnHeightJitter = 0.1f;
    [SerializeField] private bool facePlayerOnSpawn = true;
    [SerializeField, Range(-180f, 180f)] private float facePlayerYawOffsetDegrees = 0f;
    [SerializeField] private Vector2 firstShotDelayMultiplierRange = new Vector2(0.35f, 0.75f);
    [SerializeField] private bool waitForBowPickup = true;

    [Header("Shooting")]
    [SerializeField] private float shootInterval = 1.5f;
    [SerializeField] private float bulletSpeed = 7f;
    [SerializeField] private float bulletDamage = 1f;
    [SerializeField] private float bulletSize = 0.18f;
    [SerializeField] private float bulletTargetHitRadius = 0.85f;
    [SerializeField] private float bulletLifetimePadding = 2f;
    [SerializeField] private float centeredSpawnPointTolerance = 0.35f;
    [SerializeField] private float muzzleSurfacePadding = 0.18f;

    private Damageable damageable;
    private float shootTimer;
    private bool waitingForRespawn;
    private int lastSpawnIndex = -1;
    private int preferredSpawnIndex = -1;
    private Renderer[] cachedRenderers;
    private Collider[] cachedColliders;
    private bool[] initialRendererStates;
    private bool[] initialColliderStates;
    private PlayerHealth playerHealth;
    private EnemyPickupDropper pickupDropper;
    private int nextEyeSpawnIndex;
    private bool encounterStarted;
    private bool presentationStateCached;
    private AudioSource nearGroundAudioSource;
    private bool nearGroundSoundPlayed;
    private static AudioClip cachedNearGroundClip;

    public bool IsPresenting => (!waitForBowPickup || encounterStarted) && !waitingForRespawn;
    public Transform BulletSpawnPoint => bulletSpawnPoint;
    public bool HasEyeBulletSpawnPoints => HasConfiguredEyeSpawnPoints();
    public bool EncounterStarted => encounterStarted;

    public void Configure(Transform target, Transform spawnPoint, Transform[] spawnPoints, Material redBulletMaterial, PlayerHealth targetHealth)
    {
        PrepareDamageable();
        playerTarget = target;
        playerHealth = targetHealth;
        CollectEyeBulletSpawnPointsIfNeeded();

        if (bulletSpawnPoint == null && spawnPoint != null)
        {
            bulletSpawnPoint = spawnPoint;
        }

        if (bulletSpawnPoint == null && HasConfiguredEyeSpawnPoints())
        {
            bulletSpawnPoint = eyeBulletSpawnPoints[0];
        }

        if (bulletSpawnPoint == null)
        {
            bulletSpawnPoint = transform;
        }

        ceilingSpawnPoints = spawnPoints;
        bulletMaterial = redBulletMaterial;
        CachePresentationComponents();
        CachePickupDropper();
    }

    public void ApplyEncounterTuning(float newShootInterval, float newBulletSpeed, float newBulletDamage)
    {
        shootInterval = Mathf.Max(0.1f, newShootInterval);
        bulletSpeed = Mathf.Max(0.1f, newBulletSpeed);
        bulletDamage = Mathf.Max(0f, newBulletDamage);
    }

    public void ApplyEncounterTuning(float newShootInterval, float newBulletSpeed, float newBulletDamage, float newBulletSize)
    {
        ApplyEncounterTuning(newShootInterval, newBulletSpeed, newBulletDamage);
        bulletSize = Mathf.Max(0.01f, newBulletSize);
    }

    public void SetPreferredSpawnIndex(int spawnIndex)
    {
        preferredSpawnIndex = spawnIndex;
    }

    public void SetDescendTowardGround(bool enabled)
    {
        descendTowardGround = enabled;
    }

    public void SetDescendSpeed(float speed)
    {
        descendSpeed = Mathf.Max(0.05f, speed);
    }

    public void SetRespawnOnDeath(bool enabled)
    {
        respawnOnDeath = enabled;
    }

    public void BeginEncounter()
    {
        if (encounterStarted) return;

        encounterStarted = true;
        waitingForRespawn = false;
        CancelInvoke(nameof(Respawn));
        CachePresentationComponents();
        CollectEyeBulletSpawnPointsIfNeeded();

        if (playerTarget == null)
        {
            Camera camera = VrCameraResolver.GetCamera();
            if (camera != null)
            {
                playerTarget = camera.transform;
            }
        }

        if (bulletSpawnPoint == null)
        {
            bulletSpawnPoint = HasConfiguredEyeSpawnPoints() ? eyeBulletSpawnPoints[0] : transform;
        }

        if (playerHealth == null)
        {
            playerHealth = ResolvePlayerHealth();
        }

        if (damageable != null)
        {
            damageable.ResetHealth();
        }

        SpawnAtCeilingPoint();
        SetPresentationActive(true);
    }

    public void ResetForBowPickup()
    {
        encounterStarted = false;
        waitingForRespawn = false;
        CancelInvoke(nameof(Respawn));
        PrepareDamageable();

        if (damageable != null)
        {
            damageable.ResetHealth();
        }

        gameObject.SetActive(true);
        CachePresentationComponents();
        SetPresentationActive(false);
        nearGroundSoundPlayed = false;
    }
    private void Awake()
    {
        PrepareDamageable();
        CollectEyeBulletSpawnPointsIfNeeded();
        CachePresentationComponents();
        CachePickupDropper();
    }

    private void OnEnable()
    {
        PrepareDamageable();
    }

    private void OnDisable()
    {
        if (damageable != null)
        {
            damageable.Died -= HandleDeath;
        }
    }

    private void Start()
    {
        CachePresentationComponents();
        CollectEyeBulletSpawnPointsIfNeeded();

        if (playerTarget == null)
        {
            Camera camera = VrCameraResolver.GetCamera();
            if (camera != null)
            {
                playerTarget = camera.transform;
            }
        }

        if (bulletSpawnPoint == null)
        {
            bulletSpawnPoint = transform;
        }

        if (playerHealth == null)
        {
            playerHealth = ResolvePlayerHealth();
        }

        if (waitForBowPickup && !encounterStarted)
        {
            SetPresentationActive(false);
            return;
        }

        if (!encounterStarted)
        {
            BeginEncounter();
        }
    }

    private void Update()
    {
        if ((waitForBowPickup && !encounterStarted) || waitingForRespawn || !gameObject.activeInHierarchy) return;

        RotateTowardPlayer();
        if (descendTowardGround)
        {
            MoveTowardGround();
        }
        PlayNearGroundSoundIfNeeded();
        ShootAtPlayer();
    }

    private void RotateTowardPlayer()
    {
        if (!facePlayerOnSpawn || playerTarget == null) return;

        if (TryGetLookRotation(transform.position, playerTarget.position, out Quaternion lookRotation))
        {
            transform.rotation = ApplyFacePlayerRotationOffset(lookRotation);
        }
    }

    private void MoveTowardGround()
    {
        Vector3 position = transform.position;
        position.y = Mathf.MoveTowards(position.y, groundY, descendSpeed * Time.deltaTime);
        transform.position = position;

        if (position.y <= groundY + 0.001f)
        {
            DamagePlayer(groundHitDamage);
            StartRespawnCooldown(false);
        }
    }

    private void ShootAtPlayer()
    {
        if (playerTarget == null) return;

        shootTimer -= Time.deltaTime;
        if (shootTimer > 0f) return;

        shootTimer = shootInterval;

        Vector3 aimPoint = GetPlayerAimPoint();
        Transform shotSpawnPoint = ResolveNextBulletSpawnPoint();
        AimSpawnPointAtTarget(shotSpawnPoint, aimPoint);
        Vector3 muzzlePosition = ResolveMuzzlePosition(aimPoint, shotSpawnPoint);
        Vector3 direction = aimPoint - muzzlePosition;
        if (direction.sqrMagnitude <= 0.0001f) return;
        direction.Normalize();

        GameObject bullet = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        bullet.name = "Bat Bullet";
        bullet.transform.position = muzzlePosition;
        bullet.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
        bullet.transform.localScale = Vector3.one * bulletSize;

        Collider bulletCollider = bullet.GetComponent<Collider>();
        bulletCollider.isTrigger = true;
        IgnoreOwnColliders(bulletCollider);

        Rigidbody rb = bullet.AddComponent<Rigidbody>();
        rb.useGravity = false;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.linearVelocity = direction * bulletSpeed;

        BatProjectile projectile = bullet.AddComponent<BatProjectile>();
        float distanceToPlayer = Vector3.Distance(muzzlePosition, aimPoint);
        float projectileLifetime = Mathf.Clamp(distanceToPlayer / bulletSpeed + bulletLifetimePadding, 4f, 20f);
        projectile.Initialize(bulletDamage, projectileLifetime, playerTarget, bulletTargetHitRadius, GetPlayerHealth());

        Renderer renderer = bullet.GetComponent<Renderer>();
        if (renderer != null && bulletMaterial != null)
        {
            renderer.sharedMaterial = bulletMaterial;
        }
    }

    private void Respawn()
    {
        waitingForRespawn = false;
        if (damageable != null)
        {
            damageable.ResetHealth();
        }

        SpawnAtCeilingPoint();
        SetPresentationActive(true);
    }

    private void SpawnAtCeilingPoint()
    {
        if (!TryPickSpawnPoint(out Transform spawnPoint, out int spawnIndex)) return;

        Vector3 spawnPosition = ResolveSpawnPosition(spawnPoint);
        transform.position = spawnPosition;
        transform.rotation = ResolveSpawnRotation(spawnPoint, spawnPosition);
        lastSpawnIndex = spawnIndex;
        nearGroundSoundPlayed = false;

        float minFirstShotDelay = Mathf.Max(0f, Mathf.Min(firstShotDelayMultiplierRange.x, firstShotDelayMultiplierRange.y));
        float maxFirstShotDelay = Mathf.Max(minFirstShotDelay, Mathf.Max(firstShotDelayMultiplierRange.x, firstShotDelayMultiplierRange.y));
        shootTimer = shootInterval * Random.Range(minFirstShotDelay, maxFirstShotDelay);
    }

    private void DamagePlayer(float amount)
    {
        if (playerTarget == null) return;

        PlayerHealth health = GetPlayerHealth();
        if (health != null)
        {
            health.TakeDamage(amount);
        }
    }

    private void PlayNearGroundSoundIfNeeded()
    {
        if (!playNearGroundSound || nearGroundSoundPlayed) return;
        if (transform.position.y > groundY + Mathf.Max(0f, nearGroundSoundHeight)) return;

        AudioClip clip = ResolveNearGroundClip();
        if (clip == null) return;

        AudioSource audioSource = ResolveNearGroundAudioSource();
        if (audioSource == null) return;

        nearGroundSoundPlayed = true;
        audioSource.PlayOneShot(clip, nearGroundSoundVolume);
    }

    private AudioSource ResolveNearGroundAudioSource()
    {
        if (nearGroundAudioSource != null) return nearGroundAudioSource;

        nearGroundAudioSource = GetComponent<AudioSource>();
        if (nearGroundAudioSource == null)
        {
            nearGroundAudioSource = gameObject.AddComponent<AudioSource>();
        }

        nearGroundAudioSource.playOnAwake = false;
        nearGroundAudioSource.spatialBlend = 1f;
        nearGroundAudioSource.rolloffMode = AudioRolloffMode.Logarithmic;
        nearGroundAudioSource.minDistance = Mathf.Max(0.1f, nearGroundSoundMinDistance);
        nearGroundAudioSource.maxDistance = Mathf.Max(nearGroundAudioSource.minDistance + 0.1f, nearGroundSoundMaxDistance);
        nearGroundAudioSource.dopplerLevel = 0.35f;
        return nearGroundAudioSource;
    }

    private AudioClip ResolveNearGroundClip()
    {
        if (cachedNearGroundClip != null) return cachedNearGroundClip;
        if (string.IsNullOrWhiteSpace(nearGroundSoundResourcePath)) return null;

        cachedNearGroundClip = Resources.Load<AudioClip>(nearGroundSoundResourcePath);
        if (cachedNearGroundClip == null)
        {
            Debug.LogWarning($"Bat near-ground sound not found at Resources/{nearGroundSoundResourcePath}.", this);
        }

        return cachedNearGroundClip;
    }

    private void HandleDeath()
    {
        if (respawnOnDeath)
        {
            StartRespawnCooldown(true);
            return;
        }

        waitingForRespawn = true;
        TryDropPickup();
        CancelInvoke(nameof(Respawn));
        SetPresentationActive(false);
    }

    private void StartRespawnCooldown(bool dropPickup)
    {
        if (waitingForRespawn) return;

        waitingForRespawn = true;
        if (dropPickup)
        {
            TryDropPickup();
        }

        CancelInvoke(nameof(Respawn));
        SetPresentationActive(false);
        Invoke(nameof(Respawn), Mathf.Max(0.05f, respawnDelay));
    }

    private void PrepareDamageable()
    {
        if (damageable == null)
        {
            damageable = GetComponent<Damageable>();
        }

        if (damageable == null) return;

        damageable.DeactivateOnDeath = false;
        damageable.Died -= HandleDeath;
        damageable.Died += HandleDeath;
    }

    private void CachePickupDropper()
    {
        if (pickupDropper == null)
        {
            pickupDropper = GetComponent<EnemyPickupDropper>();
        }
    }

    private void TryDropPickup()
    {
        CachePickupDropper();
        if (pickupDropper == null) return;

        pickupDropper.TryDrop(transform.position);
    }

    private void CachePresentationComponents()
    {
        if (presentationStateCached && cachedRenderers != null && cachedColliders != null) return;

        cachedRenderers = GetComponentsInChildren<Renderer>(true);
        cachedColliders = GetComponentsInChildren<Collider>(true);
        initialRendererStates = new bool[cachedRenderers.Length];
        initialColliderStates = new bool[cachedColliders.Length];

        for (int i = 0; i < cachedRenderers.Length; i++)
        {
            initialRendererStates[i] = cachedRenderers[i].enabled;
        }

        for (int i = 0; i < cachedColliders.Length; i++)
        {
            initialColliderStates[i] = cachedColliders[i].enabled;
        }

        presentationStateCached = true;
    }

    private void SetPresentationActive(bool active)
    {
        if (cachedRenderers == null || cachedColliders == null)
        {
            CachePresentationComponents();
        }

        for (int i = 0; i < cachedRenderers.Length; i++)
        {
            if (cachedRenderers[i] != null)
            {
                cachedRenderers[i].enabled = active && initialRendererStates[i];
            }
        }

        for (int i = 0; i < cachedColliders.Length; i++)
        {
            if (cachedColliders[i] != null)
            {
                cachedColliders[i].enabled = active && initialColliderStates[i];
            }
        }
    }

    private bool TryPickSpawnPoint(out Transform spawnPoint, out int spawnIndex)
    {
        spawnPoint = null;
        spawnIndex = -1;

        if (ceilingSpawnPoints == null || ceilingSpawnPoints.Length == 0) return false;

        int validPointCount = 0;
        for (int i = 0; i < ceilingSpawnPoints.Length; i++)
        {
            if (ceilingSpawnPoints[i] != null)
            {
                validPointCount++;
            }
        }

        if (validPointCount == 0) return false;

        if (lastSpawnIndex < 0 && preferredSpawnIndex >= 0)
        {
            int wrappedIndex = preferredSpawnIndex % ceilingSpawnPoints.Length;
            if (ceilingSpawnPoints[wrappedIndex] != null)
            {
                spawnPoint = ceilingSpawnPoints[wrappedIndex];
                spawnIndex = wrappedIndex;
                return true;
            }
        }

        float bestScore = float.NegativeInfinity;
        for (int i = 0; i < ceilingSpawnPoints.Length; i++)
        {
            Transform candidate = ceilingSpawnPoints[i];
            if (candidate == null) continue;

            float score = Random.value * 0.25f;
            if (playerTarget != null)
            {
                Vector3 playerOffset = candidate.position - playerTarget.position;
                playerOffset.y = 0f;
                float horizontalDistance = playerOffset.magnitude;
                score += horizontalDistance;

                if (horizontalDistance < minPlayerSpawnDistance)
                {
                    score -= (minPlayerSpawnDistance - horizontalDistance) * 10f;
                }
            }

            if (validPointCount > 1 && i == lastSpawnIndex)
            {
                score -= 100f;
            }

            if (score > bestScore)
            {
                bestScore = score;
                spawnPoint = candidate;
                spawnIndex = i;
            }
        }

        return spawnPoint != null;
    }

    private Vector3 ResolveSpawnPosition(Transform spawnPoint)
    {
        Vector3 position = spawnPoint.position;

        if (spawnPositionJitter > 0f)
        {
            Vector2 jitter = Random.insideUnitCircle * spawnPositionJitter;
            position.x += jitter.x;
            position.z += jitter.y;
        }

        if (spawnHeightJitter > 0f)
        {
            position.y -= Random.Range(0f, spawnHeightJitter);
        }

        if (playerTarget == null || minPlayerSpawnDistance <= 0f) return position;

        Vector3 fromPlayer = position - playerTarget.position;
        fromPlayer.y = 0f;
        float distanceFromPlayer = fromPlayer.magnitude;
        if (distanceFromPlayer >= minPlayerSpawnDistance) return position;

        if (distanceFromPlayer <= 0.001f)
        {
            fromPlayer = spawnPoint.forward;
            fromPlayer.y = 0f;

            if (fromPlayer.sqrMagnitude <= 0.001f)
            {
                fromPlayer = Vector3.forward;
            }
        }

        Vector3 push = fromPlayer.normalized * (minPlayerSpawnDistance - distanceFromPlayer);
        position.x += push.x;
        position.z += push.z;
        return position;
    }

    private Vector3 GetPlayerAimPoint()
    {
        if (playerTarget == null) return transform.position;

        return playerTarget.position;
    }

    private Transform ResolveNextBulletSpawnPoint()
    {
        CollectEyeBulletSpawnPointsIfNeeded();

        if (HasConfiguredEyeSpawnPoints())
        {
            for (int i = 0; i < eyeBulletSpawnPoints.Length; i++)
            {
                int index = nextEyeSpawnIndex % eyeBulletSpawnPoints.Length;
                nextEyeSpawnIndex = (nextEyeSpawnIndex + 1) % eyeBulletSpawnPoints.Length;

                Transform candidate = eyeBulletSpawnPoints[index];
                if (candidate != null)
                {
                    return candidate;
                }
            }
        }

        return bulletSpawnPoint != null ? bulletSpawnPoint : transform;
    }

    private Vector3 ResolveMuzzlePosition(Vector3 aimPoint, Transform spawnPoint)
    {
        Transform resolvedSpawnPoint = spawnPoint != null ? spawnPoint : bulletSpawnPoint;
        Vector3 spawnPosition = resolvedSpawnPoint != null ? resolvedSpawnPoint.position : transform.position;
        if (IsConfiguredEyeSpawnPoint(resolvedSpawnPoint)) return spawnPosition;

        if (!TryGetVisibleBodyBounds(out Bounds bounds)) return spawnPosition;

        float centeredTolerance = Mathf.Max(0.01f, bounds.extents.magnitude * centeredSpawnPointTolerance);
        if (Vector3.Distance(spawnPosition, bounds.center) > centeredTolerance)
        {
            return spawnPosition;
        }

        Vector3 fireDirection = aimPoint - bounds.center;
        if (fireDirection.sqrMagnitude <= 0.0001f)
        {
            fireDirection = transform.forward;
        }

        fireDirection.Normalize();
        float projectedBodyRadius =
            Mathf.Abs(fireDirection.x) * bounds.extents.x +
            Mathf.Abs(fireDirection.y) * bounds.extents.y +
            Mathf.Abs(fireDirection.z) * bounds.extents.z;

        return bounds.center + fireDirection * (projectedBodyRadius + bulletSize + muzzleSurfacePadding);
    }

    private void CollectEyeBulletSpawnPointsIfNeeded()
    {
        if (HasConfiguredEyeSpawnPoints()) return;

        List<Transform> candidates = new List<Transform>();
        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            if (IsLikelyEyeSpawnMarker(child))
            {
                candidates.Add(child);
            }
        }

        if (candidates.Count == 0)
        {
            Transform visualEyes = FindChildRecursive(transform, "Eyes_Bullet_Spawn");
            if (visualEyes != null)
            {
                candidates.Add(visualEyes);
            }
        }

        candidates.Sort((left, right) => left.localPosition.x.CompareTo(right.localPosition.x));
        eyeBulletSpawnPoints = candidates.ToArray();
    }

    private bool HasConfiguredEyeSpawnPoints()
    {
        if (eyeBulletSpawnPoints == null || eyeBulletSpawnPoints.Length == 0) return false;

        for (int i = 0; i < eyeBulletSpawnPoints.Length; i++)
        {
            if (eyeBulletSpawnPoints[i] != null && eyeBulletSpawnPoints[i].IsChildOf(transform))
            {
                return true;
            }
        }

        return false;
    }

    private bool IsConfiguredEyeSpawnPoint(Transform candidate)
    {
        if (candidate == null || eyeBulletSpawnPoints == null || !candidate.IsChildOf(transform)) return false;

        for (int i = 0; i < eyeBulletSpawnPoints.Length; i++)
        {
            if (eyeBulletSpawnPoints[i] == candidate)
            {
                return true;
            }
        }

        return false;
    }

    private bool IsLikelyEyeSpawnMarker(Transform candidate)
    {
        if (candidate == null || candidate == transform) return false;
        if (candidate.GetComponent<Renderer>() != null) return false;
        if (candidate.GetComponent<Collider>() != null) return false;
        if (candidate.GetComponent<BatEnemy>() != null) return false;
        if (candidate.GetComponent<Damageable>() != null) return false;
        if (candidate.childCount > 0) return false;

        string lowerName = candidate.name.ToLowerInvariant();
        if (lowerName == "eyes_bullet_spawn") return false;
        if (lowerName.Contains("health bar") || lowerName.Contains("bullet_muzzle")) return false;

        return lowerName.StartsWith("gameobject") ||
            lowerName.Contains("eye") ||
            lowerName.Contains("spawn");
    }

    private Transform FindChildRecursive(Transform parent, string name)
    {
        if (parent == null) return null;
        if (parent.name == name) return parent;

        foreach (Transform child in parent)
        {
            Transform match = FindChildRecursive(child, name);
            if (match != null) return match;
        }

        return null;
    }

    private bool TryGetVisibleBodyBounds(out Bounds bounds)
    {
        bounds = default;

        Renderer[] renderers = cachedRenderers != null && cachedRenderers.Length > 0
            ? cachedRenderers
            : GetComponentsInChildren<Renderer>(true);

        bool hasBounds = false;
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || !renderer.enabled || IsRuntimeHelper(renderer.transform)) continue;

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return hasBounds;
    }

    private bool IsRuntimeHelper(Transform candidate)
    {
        Transform current = candidate;
        while (current != null && current != transform)
        {
            string lowerName = current.name.ToLowerInvariant();
            if (lowerName.Contains("health bar") || lowerName.Contains("bullet_muzzle"))
            {
                return true;
            }

            current = current.parent;
        }

        return false;
    }

    private PlayerHealth GetPlayerHealth()
    {
        if (playerHealth == null)
        {
            playerHealth = ResolvePlayerHealth();
        }

        return playerHealth;
    }

    private PlayerHealth ResolvePlayerHealth()
    {
        if (playerTarget == null) return null;

        GameObject xrOrigin = GameObject.Find("XR Origin (XR Rig)") ?? GameObject.Find("XR Origin (VR)");
        if (xrOrigin != null && playerTarget.IsChildOf(xrOrigin.transform))
        {
            return xrOrigin.GetComponent<PlayerHealth>();
        }

        return playerTarget.GetComponentInParent<PlayerHealth>();
    }

    private void IgnoreOwnColliders(Collider bulletCollider)
    {
        if (bulletCollider == null) return;

        if (cachedColliders == null)
        {
            CachePresentationComponents();
        }

        for (int i = 0; i < cachedColliders.Length; i++)
        {
            Collider ownCollider = cachedColliders[i];
            if (ownCollider != null)
            {
                Physics.IgnoreCollision(bulletCollider, ownCollider, true);
            }
        }
    }

    private Quaternion ResolveSpawnRotation(Transform spawnPoint, Vector3 spawnPosition)
    {
        if (!facePlayerOnSpawn || playerTarget == null)
        {
            return spawnPoint.rotation;
        }

        if (TryGetLookRotation(spawnPosition, playerTarget.position, out Quaternion lookRotation))
        {
            return ApplyFacePlayerRotationOffset(lookRotation);
        }

        return spawnPoint.rotation;
    }

    private void AimSpawnPointAtTarget(Transform spawnPoint, Vector3 targetPosition)
    {
        if (spawnPoint == null) return;

        if (TryGetLookRotation(spawnPoint.position, targetPosition, out Quaternion lookRotation))
        {
            spawnPoint.rotation = lookRotation;
        }
    }

    private bool TryGetLookRotation(Vector3 origin, Vector3 targetPosition, out Quaternion lookRotation)
    {
        Vector3 lookDirection = targetPosition - origin;
        if (lookDirection.sqrMagnitude <= 0.0001f)
        {
            lookRotation = Quaternion.identity;
            return false;
        }

        lookRotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
        return true;
    }

    private Quaternion ApplyFacePlayerRotationOffset(Quaternion lookRotation)
    {
        return lookRotation * Quaternion.Euler(0f, facePlayerYawOffsetDegrees, 0f);
    }

}
