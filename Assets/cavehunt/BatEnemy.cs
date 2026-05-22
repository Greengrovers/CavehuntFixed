using UnityEngine;

[RequireComponent(typeof(Damageable))]
public class BatEnemy : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform playerTarget;
    [SerializeField] private Transform bulletSpawnPoint;
    [SerializeField] private Transform[] ceilingSpawnPoints;
    [SerializeField] private Material bulletMaterial;

    [Header("Movement")]
    [SerializeField] private float descendSpeed = 1.5f;
    [SerializeField] private float groundY = 0.05f;
    [SerializeField] private float groundHitDamage = 1f;
    [SerializeField] private float respawnDelay = 2f;

    [Header("Spawn")]
    [SerializeField] private float minPlayerSpawnDistance = 1.5f;
    [SerializeField] private float spawnPositionJitter = 0.35f;
    [SerializeField] private float spawnHeightJitter = 0.1f;
    [SerializeField] private bool facePlayerOnSpawn = true;
    [SerializeField] private Vector2 firstShotDelayMultiplierRange = new Vector2(0.35f, 0.75f);

    [Header("Shooting")]
    [SerializeField] private float shootInterval = 1.5f;
    [SerializeField] private float bulletSpeed = 7f;
    [SerializeField] private float bulletDamage = 1f;
    [SerializeField] private float bulletSize = 0.18f;
    [SerializeField] private float bulletTargetHitRadius = 0.85f;
    [SerializeField] private float bulletLifetimePadding = 2f;

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

    public bool IsPresenting => !waitingForRespawn;

    public void Configure(Transform target, Transform spawnPoint, Transform[] spawnPoints, Material redBulletMaterial, PlayerHealth targetHealth)
    {
        PrepareDamageable();
        playerTarget = target;
        playerHealth = targetHealth;
        bulletSpawnPoint = spawnPoint != null ? spawnPoint : transform;
        ceilingSpawnPoints = spawnPoints;
        bulletMaterial = redBulletMaterial;
        CachePresentationComponents();
        CachePickupDropper();
    }

    public void ApplyEncounterTuning(float newDescendSpeed, float newShootInterval, float newBulletSpeed, float newBulletDamage)
    {
        descendSpeed = Mathf.Max(0.1f, newDescendSpeed);
        shootInterval = Mathf.Max(0.1f, newShootInterval);
        bulletSpeed = Mathf.Max(0.1f, newBulletSpeed);
        bulletDamage = Mathf.Max(0f, newBulletDamage);
    }

    public void SetPreferredSpawnIndex(int spawnIndex)
    {
        preferredSpawnIndex = spawnIndex;
    }

    private void Awake()
    {
        PrepareDamageable();
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

        if (playerTarget == null && Camera.main != null)
        {
            playerTarget = Camera.main.transform;
        }

        if (bulletSpawnPoint == null)
        {
            bulletSpawnPoint = transform;
        }

        if (playerHealth == null)
        {
            playerHealth = ResolvePlayerHealth();
        }

        SpawnAtCeilingPoint();
        SetPresentationActive(true);
    }

    private void Update()
    {
        if (waitingForRespawn || !gameObject.activeInHierarchy) return;

        RotateTowardPlayer();
        MoveTowardGround();
        ShootAtPlayer();
    }

    private void RotateTowardPlayer()
    {
        if (!facePlayerOnSpawn || playerTarget == null) return;

        Vector3 lookDirection = playerTarget.position - transform.position;
        lookDirection.y = 0f;
        if (lookDirection.sqrMagnitude <= 0.0001f) return;

        transform.rotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
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

        Vector3 muzzlePosition = bulletSpawnPoint != null ? bulletSpawnPoint.position : transform.position;
        Vector3 aimPoint = GetPlayerAimPoint();
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

    private void HandleDeath()
    {
        StartRespawnCooldown(true);
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

        Vector3 lookDirection = playerTarget.position - spawnPosition;
        lookDirection.y = 0f;
        if (lookDirection.sqrMagnitude <= 0.0001f)
        {
            return spawnPoint.rotation;
        }

        return Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
    }

}
