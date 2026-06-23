using UnityEngine;
using UnityEngine.SceneManagement;

public static class BatEncounterBootstrap
{
    private const int EnemyCount = 2;
    private const float BatShootInterval = 1.1f;
    private const float BatBulletSpeed = 12f;
    private const float BatBulletDamage = 1f;
    private const float BossBulletDamageMultiplier = 2f;
    private const float BossBulletSize = 1.8f;
    private const float BossTornadoEndRadius = 1.5f;
    private const int BossTornadoHelperCount = 24;
    private const float FallbackCeilingHeightAbovePlayer = 7f;
    private const float MinimumCeilingHeightAbovePlayer = 5.5f;
    private const float MaximumCeilingHeightAbovePlayer = 9f;
    private const float CeilingProbeStartOffset = 0.25f;
    private const float CeilingProbeDistance = 90f;
    private const float CeilingInset = 0.45f;
    private const float FallbackEyeHeight = 0.35f;
    private const float FallbackEyeForward = 0.55f;

    private static bool loggedCaveHeight;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void SetupBatEncounter()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.name != "03-Interactions") return;

        GameObject templateBat = GameObject.Find("Bat");
        if (templateBat == null) return;

        CavehuntDifficultySettings difficultySettings = CavehuntDifficultySettings.Resolve();
        CavehuntEncounterDirector encounterDirector = CavehuntEncounterDirector.Resolve();
        Transform playerTarget = FindPlayerTarget();
        PlayerHealth playerHealth = EnsurePlayerHealth(playerTarget);
        EnsurePlayerAmmoInventory(playerHealth, playerTarget);
        RemoveNonPlayerHealthComponents(playerHealth);

        Transform[] spawnPoints = EnsureCeilingSpawnPoints(templateBat.transform, playerTarget);
        Material bulletMaterial = CreateRedBulletMaterial();
        GameObject[] bats = EnsureBatEnemies(templateBat, EnemyCount);
        Vector3 baseScale = templateBat.transform.localScale;

        for (int i = 0; i < bats.Length; i++)
        {
            ConfigureBat(bats[i], playerTarget, playerHealth, spawnPoints, bulletMaterial, baseScale, i, difficultySettings, encounterDirector);
        }

        EnsureBossEnemy(templateBat, playerTarget, playerHealth, spawnPoints, bulletMaterial, baseScale, difficultySettings, encounterDirector);
        encounterDirector.ResetForBowPickup();
    }

    private static GameObject[] EnsureBatEnemies(GameObject templateBat, int enemyCount)
    {
        GameObject[] bats = new GameObject[enemyCount];
        bats[0] = templateBat;

        for (int i = 1; i < enemyCount; i++)
        {
            string batName = $"Bat {i + 1}";
            GameObject bat = GameObject.Find(batName);
            if (bat == null)
            {
                bat = UnityEngine.Object.Instantiate(templateBat);
                bat.name = batName;
            }

            bats[i] = bat;
        }

        return bats;
    }

    private static void ConfigureBat(GameObject bat, Transform playerTarget, PlayerHealth playerHealth, Transform[] spawnPoints, Material bulletMaterial, Vector3 baseScale, int spawnOffset, CavehuntDifficultySettings difficultySettings, CavehuntEncounterDirector encounterDirector)
    {
        bat.SetActive(true);
        bat.transform.localScale = baseScale * difficultySettings.BatScaleMultiplier;

        Damageable damageable = bat.GetComponent<Damageable>();
        if (damageable == null)
        {
            damageable = bat.AddComponent<Damageable>();
            difficultySettings.ApplyHealthTo(damageable, false);
        }
        else
        {
            difficultySettings.ApplyHealthTo(damageable, false);
        }

        if (bat.GetComponentInChildren<Collider>() == null)
        {
            SphereCollider collider = bat.AddComponent<SphereCollider>();
            collider.radius = 0.55f;
        }
        else
        {
            SphereCollider collider = bat.GetComponent<SphereCollider>();
            if (collider != null)
            {
                collider.radius = Mathf.Max(collider.radius, 0.65f);
            }
        }

        BatEnemy batEnemy = bat.GetComponent<BatEnemy>();
        if (batEnemy == null)
        {
            batEnemy = bat.AddComponent<BatEnemy>();
        }

        EnemyHealthBar healthBar = bat.GetComponent<EnemyHealthBar>();
        if (healthBar == null)
        {
            healthBar = bat.AddComponent<EnemyHealthBar>();
        }
        healthBar.Configure(playerTarget);

        Transform bulletSpawn = batEnemy.BulletSpawnPoint != null
            ? batEnemy.BulletSpawnPoint
            : EnsureBulletSpawnPoint(bat.transform);
        batEnemy.SetPreferredSpawnIndex(spawnOffset);
        batEnemy.SetRespawnOnDeath(false);
        batEnemy.SetDescendSpeed(difficultySettings.BatDescendSpeed);
        batEnemy.ApplyEncounterTuning(BatShootInterval, BatBulletSpeed, BatBulletDamage);
        batEnemy.Configure(playerTarget, bulletSpawn, spawnPoints, bulletMaterial, playerHealth);

        EnemyPickupDropper pickupDropper = bat.GetComponent<EnemyPickupDropper>();
        if (pickupDropper == null)
        {
            pickupDropper = bat.AddComponent<EnemyPickupDropper>();
            pickupDropper.Configure(0.25f);
        }

        CavehuntEnemyKillTracker killTracker = bat.GetComponent<CavehuntEnemyKillTracker>();
        if (killTracker == null)
        {
            killTracker = bat.AddComponent<CavehuntEnemyKillTracker>();
        }
        killTracker.Configure(CavehuntEnemyRole.Tutorial, encounterDirector);
    }


    private static void EnsureBossEnemy(GameObject templateBat, Transform playerTarget, PlayerHealth playerHealth, Transform[] spawnPoints, Material bulletMaterial, Vector3 baseScale, CavehuntDifficultySettings difficultySettings, CavehuntEncounterDirector encounterDirector)
    {
        GameObject boss = GameObject.Find("Boss Bat");
        if (boss == null)
        {
            boss = UnityEngine.Object.Instantiate(templateBat);
            boss.name = "Boss Bat";
        }

        boss.SetActive(true);
        boss.transform.localScale = baseScale * difficultySettings.BossScaleMultiplier;

        Damageable damageable = boss.GetComponent<Damageable>();
        if (damageable == null)
        {
            damageable = boss.AddComponent<Damageable>();
        }
        difficultySettings.ApplyHealthTo(damageable, true);

        SphereCollider collider = boss.GetComponent<SphereCollider>();
        if (collider == null && boss.GetComponentInChildren<Collider>() == null)
        {
            collider = boss.AddComponent<SphereCollider>();
        }
        if (collider != null)
        {
            collider.radius = Mathf.Max(collider.radius, 0.9f);
        }

        BatEnemy batEnemy = boss.GetComponent<BatEnemy>();
        if (batEnemy == null)
        {
            batEnemy = boss.AddComponent<BatEnemy>();
        }

        BossEnemy bossEnemy = boss.GetComponent<BossEnemy>();
        if (bossEnemy == null)
        {
            bossEnemy = boss.AddComponent<BossEnemy>();
        }
        bossEnemy.ConfigureTornadoPath(BossTornadoEndRadius, BossTornadoHelperCount);
        bossEnemy.SetDescendSpeed(difficultySettings.BossDescendSpeed);
        bossEnemy.ApplyDifficulty(difficultySettings);

        EnemyHealthBar healthBar = boss.GetComponent<EnemyHealthBar>();
        if (healthBar == null)
        {
            healthBar = boss.AddComponent<EnemyHealthBar>();
        }
        healthBar.Configure(playerTarget);

        Transform bulletSpawn = batEnemy.BulletSpawnPoint != null
            ? batEnemy.BulletSpawnPoint
            : EnsureBulletSpawnPoint(boss.transform);
        batEnemy.SetPreferredSpawnIndex(spawnPoints != null && spawnPoints.Length > 0 ? spawnPoints.Length - 1 : 0);
        batEnemy.SetDescendTowardGround(false);
        batEnemy.SetRespawnOnDeath(false);
        batEnemy.ApplyEncounterTuning(BatShootInterval, BatBulletSpeed, BatBulletDamage * BossBulletDamageMultiplier, BossBulletSize);
        batEnemy.Configure(playerTarget, bulletSpawn, spawnPoints, bulletMaterial, playerHealth);

        EnemyPickupDropper pickupDropper = boss.GetComponent<EnemyPickupDropper>();
        if (pickupDropper != null)
        {
            UnityEngine.Object.Destroy(pickupDropper);
        }

        CavehuntEnemyKillTracker killTracker = boss.GetComponent<CavehuntEnemyKillTracker>();
        if (killTracker == null)
        {
            killTracker = boss.AddComponent<CavehuntEnemyKillTracker>();
        }
        killTracker.Configure(CavehuntEnemyRole.Boss, encounterDirector);
    }
    private static Transform FindPlayerTarget()
    {
        Camera camera = VrCameraResolver.GetCamera();
        if (camera != null) return camera.transform;

        GameObject xrOrigin = GameObject.Find("XR Origin (XR Rig)") ?? GameObject.Find("XR Origin (VR)");
        if (xrOrigin != null) return xrOrigin.transform;

        GameObject cameraObject = GameObject.Find("Main Camera");
        return cameraObject != null ? cameraObject.transform : null;
    }

    private static PlayerHealth EnsurePlayerHealth(Transform playerTarget)
    {
        if (playerTarget == null) return null;

        Transform healthOwner = FindPlayerHealthOwner(playerTarget);
        if (healthOwner == null) return null;

        PlayerHealth playerHealth = healthOwner.GetComponent<PlayerHealth>();
        if (playerHealth == null)
        {
            playerHealth = healthOwner.gameObject.AddComponent<PlayerHealth>();
        }

        return playerHealth;
    }

    private static void RemoveNonPlayerHealthComponents(PlayerHealth playerHealth)
    {
        if (playerHealth == null) return;

        PlayerHealth[] healthComponents = UnityEngine.Object.FindObjectsByType<PlayerHealth>(FindObjectsInactive.Include);
        for (int i = 0; i < healthComponents.Length; i++)
        {
            PlayerHealth component = healthComponents[i];
            if (component == null || component == playerHealth) continue;

            UnityEngine.Object.Destroy(component);
        }
    }

    private static void EnsurePlayerAmmoInventory(PlayerHealth playerHealth, Transform playerTarget)
    {
        Transform inventoryOwner = playerHealth != null ? playerHealth.transform : FindPlayerHealthOwner(playerTarget);
        if (inventoryOwner == null) return;

        if (inventoryOwner.GetComponent<PlayerAmmoInventory>() == null)
        {
            inventoryOwner.gameObject.AddComponent<PlayerAmmoInventory>();
        }
    }

    private static Transform FindPlayerHealthOwner(Transform playerTarget)
    {
        GameObject xrOrigin = GameObject.Find("XR Origin (XR Rig)") ?? GameObject.Find("XR Origin (VR)");
        if (xrOrigin != null && playerTarget.IsChildOf(xrOrigin.transform))
        {
            return xrOrigin.transform;
        }

        return playerTarget;
    }

    private static Transform[] EnsureCeilingSpawnPoints(Transform bat, Transform playerTarget)
    {
        GameObject parent = GameObject.Find("Bat Ceiling Spawn Points");
        if (parent == null)
        {
            parent = new GameObject("Bat Ceiling Spawn Points");
        }

        parent.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        parent.transform.localScale = Vector3.one;

        Vector3 center = playerTarget != null ? playerTarget.position : bat.position;
        Vector3[] offsets =
        {
            new Vector3(-2.4f, 0f, 2.4f),
            new Vector3(0f, 0f, 3.2f),
            new Vector3(2.4f, 0f, 2.4f),
            new Vector3(-1.8f, 0f, -2.2f),
            new Vector3(1.8f, 0f, -2.2f),
            new Vector3(0f, 0f, -3.1f),
        };

        Transform[] spawnPoints = new Transform[offsets.Length];
        for (int i = 0; i < offsets.Length; i++)
        {
            spawnPoints[i] = CreateCeilingSpawnPoint(parent.transform, i, center, offsets[i]);
        }

        return spawnPoints;
    }

    private static Transform CreateCeilingSpawnPoint(Transform parent, int index, Vector3 center, Vector3 offset)
    {
        string name = $"Bat Ceiling Spawn {index + 1}";
        Transform point = parent.Find(name);
        if (point == null)
        {
            point = new GameObject(name).transform;
            point.SetParent(parent);
        }

        point.position = ResolveCeilingSpawnPosition(center + offset, center);
        FaceToward(point, center);
        return point;
    }

    private static Transform EnsureBulletSpawnPoint(Transform bat)
    {
        Transform visualEyes = FindChildRecursive(bat, "Eyes_Bullet_Spawn");
        if (visualEyes != null)
        {
            return visualEyes;
        }

        Transform bulletSpawn = bat.Find("Bat_Bullet_Muzzle");
        if (bulletSpawn == null)
        {
            bulletSpawn = new GameObject("Bat_Bullet_Muzzle").transform;
            bulletSpawn.SetParent(bat, false);
        }

        PositionFallbackBulletSpawn(bat, bulletSpawn);
        bulletSpawn.localScale = Vector3.one;
        return bulletSpawn;
    }

    private static void PositionFallbackBulletSpawn(Transform bat, Transform bulletSpawn)
    {
        if (TryGetBodyBounds(bat, out Bounds bounds))
        {
            Vector3 eyePosition = bounds.center
                + Vector3.up * (bounds.extents.y * 0.12f)
                + bat.forward * (Mathf.Max(bounds.extents.x, bounds.extents.z) * 0.55f);

            bulletSpawn.position = eyePosition;
            bulletSpawn.rotation = bat.rotation;
            bulletSpawn.SetParent(bat, true);
            return;
        }

        bulletSpawn.SetParent(bat, false);
        bulletSpawn.localPosition = new Vector3(0f, FallbackEyeHeight, FallbackEyeForward);
        bulletSpawn.localRotation = Quaternion.identity;
    }

    private static bool IsReasonableEyePosition(Transform bat, Vector3 eyePosition)
    {
        if (!TryGetBodyBounds(bat, out Bounds bounds)) return true;

        float allowedDistance = Mathf.Max(0.75f, bounds.extents.magnitude * 1.25f);
        return Vector3.Distance(bounds.center, eyePosition) <= allowedDistance;
    }

    private static bool TryGetBodyBounds(Transform root, out Bounds bounds)
    {
        bounds = default;
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        bool hasBounds = false;

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || IsRuntimeHelper(renderer.transform, root)) continue;

            EncapsulateBounds(ref bounds, renderer.bounds, ref hasBounds);
        }

        return hasBounds;
    }

    private static bool IsRuntimeHelper(Transform candidate, Transform root)
    {
        Transform current = candidate;
        while (current != null && current != root)
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

    private static void EncapsulateBounds(ref Bounds aggregate, Bounds nextBounds, ref bool hasBounds)
    {
        if (nextBounds.size.sqrMagnitude <= 0.0001f) return;

        if (!hasBounds)
        {
            aggregate = nextBounds;
            hasBounds = true;
        }
        else
        {
            aggregate.Encapsulate(nextBounds);
        }
    }

    private static Vector3 ResolveCeilingSpawnPosition(Vector3 desiredPosition, Vector3 center)
    {
        float minimumY = center.y + MinimumCeilingHeightAbovePlayer;
        float maximumY = Mathf.Max(minimumY, center.y + MaximumCeilingHeightAbovePlayer);
        float spawnY = Mathf.Clamp(center.y + FallbackCeilingHeightAbovePlayer, minimumY, maximumY);

        Vector3 probeOrigin = new Vector3(desiredPosition.x, center.y + CeilingProbeStartOffset, desiredPosition.z);
        if (TryFindCaveSurface(probeOrigin, Vector3.up, CeilingProbeDistance, out RaycastHit ceilingHit))
        {
            spawnY = Mathf.Clamp(ceilingHit.point.y - CeilingInset, minimumY, maximumY);
        }

        LogLocalCaveHeight(center, spawnY);
        return new Vector3(desiredPosition.x, spawnY, desiredPosition.z);
    }

    private static bool TryFindCaveSurface(Vector3 origin, Vector3 direction, float distance, out RaycastHit bestHit)
    {
        bestHit = default;
        bool previousQueriesHitBackfaces = Physics.queriesHitBackfaces;
        Physics.queriesHitBackfaces = true;
        RaycastHit[] hits;
        try
        {
            hits = Physics.RaycastAll(origin, direction, distance, ~0, QueryTriggerInteraction.Ignore);
        }
        finally
        {
            Physics.queriesHitBackfaces = previousQueriesHitBackfaces;
        }

        float bestDistance = float.PositiveInfinity;
        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit hit = hits[i];
            if (hit.collider == null || !IsCaveCandidate(hit.collider.transform)) continue;

            if (hit.distance < bestDistance)
            {
                bestDistance = hit.distance;
                bestHit = hit;
            }
        }

        return bestHit.collider != null;
    }

    private static bool IsCaveCandidate(Transform candidate)
    {
        Transform current = candidate;
        while (current != null)
        {
            string lowerName = current.name.ToLowerInvariant();
            if (lowerName.Contains("cave") || lowerName.Contains("ceiling"))
            {
                return true;
            }

            current = current.parent;
        }

        return false;
    }

    private static void LogLocalCaveHeight(Vector3 center, float spawnY)
    {
        if (loggedCaveHeight) return;

        loggedCaveHeight = true;
        Vector3 origin = center + Vector3.up * CeilingProbeStartOffset;
        bool foundFloor = TryFindCaveSurface(origin, Vector3.down, CeilingProbeDistance, out RaycastHit floorHit);
        bool foundCeiling = TryFindCaveSurface(origin, Vector3.up, CeilingProbeDistance, out RaycastHit ceilingHit);

        if (foundFloor && foundCeiling)
        {
            float caveHeight = ceilingHit.point.y - floorHit.point.y;
            Debug.Log($"Cavehunt bat spawn: local floor Y {floorHit.point.y:F2}, local ceiling Y {ceilingHit.point.y:F2}, local height {caveHeight:F2}. Bat spawn Y {spawnY:F2}.");
            return;
        }

        Debug.LogWarning($"Cavehunt bat spawn: local cave height could not be fully measured. Bat spawn Y {spawnY:F2}.");
    }

    private static void FaceToward(Transform transformToRotate, Vector3 targetPosition)
    {
        Vector3 direction = targetPosition - transformToRotate.position;
        direction.y = 0f;

        if (direction.sqrMagnitude <= 0.0001f) return;

        transformToRotate.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
    }

    private static Transform FindChildRecursive(Transform parent, string name)
    {
        if (parent.name == name) return parent;

        foreach (Transform child in parent)
        {
            Transform match = FindChildRecursive(child, name);
            if (match != null) return match;
        }

        return null;
    }

    private static Material CreateRedBulletMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        Material material = new Material(shader)
        {
            name = "Bat_Bullet_Red_Runtime"
        };

        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.red);
        if (material.HasProperty("_Color")) material.SetColor("_Color", Color.red);
        if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", Color.red * 1.5f);
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.2f);

        return material;
    }
}
