using System.Collections.Generic;
using UnityEngine;

public class BatOuterRingSpawner : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject batPrefab;
    [SerializeField] private Transform spawnPointParent;
    [SerializeField] private string spawnPointParentName = "Bat Outer Ring Spawn Points";
    [SerializeField] private Material bulletMaterial;

    [Header("Spawning")]
    [SerializeField] private bool spawnAutomatically = false;
    [SerializeField, Min(0.1f)] private float spawnInterval = 3f;
    [SerializeField, Min(0)] private int maxActiveBats;
    [SerializeField] private string spawnedBatNamePrefix = "Bat Outer Ring";

    private readonly List<Transform> spawnPoints = new List<Transform>();
    private Transform playerTarget;
    private PlayerHealth playerHealth;
    private Material runtimeBulletMaterial;
    private int spawnedCount;
    private bool warnedMissingPrefab;
    private bool warnedMissingSpawnPoints;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        if (spawnAutomatically)
        {
            StartSpawning();
        }
    }

    private void OnDisable()
    {
        CancelInvoke(nameof(SpawnBat));
    }

    public void StartSpawning()
    {
        ResolveReferences();
        CancelInvoke(nameof(SpawnBat));
        SpawnBat();
        InvokeRepeating(nameof(SpawnBat), Mathf.Max(0.1f, spawnInterval), Mathf.Max(0.1f, spawnInterval));
    }

    public void StopSpawning()
    {
        CancelInvoke(nameof(SpawnBat));
    }

    public void ResetForBowPickup()
    {
        StopSpawning();

        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if (child != null && child.GetComponent<BatEnemy>() != null)
            {
                Destroy(child.gameObject);
            }
        }

        spawnedCount = 0;
        gameObject.SetActive(false);
    }
    public void SpawnBat()
    {
        ResolveReferences();

        if (batPrefab == null)
        {
            WarnOnce(ref warnedMissingPrefab, "BatOuterRingSpawner has no bat prefab assigned.");
            return;
        }

        if (spawnPoints.Count == 0)
        {
            WarnOnce(ref warnedMissingSpawnPoints, "BatOuterRingSpawner found no outer-ring spawn points.");
            return;
        }

        if (maxActiveBats > 0 && CountActiveSpawnedBats() >= maxActiveBats)
        {
            return;
        }

        int spawnIndex = Random.Range(0, spawnPoints.Count);
        Transform spawnPoint = spawnPoints[spawnIndex];
        if (spawnPoint == null)
        {
            RefreshSpawnPoints();
            return;
        }

        GameObject bat = Instantiate(batPrefab, spawnPoint.position, spawnPoint.rotation, transform);
        spawnedCount++;
        bat.name = $"{spawnedBatNamePrefix} {spawnedCount:00}";
        bat.SetActive(true);

        ConfigureSpawnedBat(bat, spawnPoint);
    }

    private void ConfigureSpawnedBat(GameObject bat, Transform spawnPoint)
    {
        if (bat == null) return;

        BatEnemy batEnemy = bat.GetComponent<BatEnemy>();
        if (batEnemy == null) return;

        ResolvePlayerReferences();
        Material resolvedBulletMaterial = ResolveBulletMaterial();
        Transform[] resolvedSpawnPoints = spawnPoint != null
            ? new[] { spawnPoint }
            : spawnPoints.ToArray();

        batEnemy.SetPreferredSpawnIndex(0);
        batEnemy.Configure(playerTarget, batEnemy.BulletSpawnPoint, resolvedSpawnPoints, resolvedBulletMaterial, playerHealth);
        batEnemy.BeginEncounter();
    }

    private void ResolveReferences()
    {
        ResolveBatPrefab();
        ResolveSpawnPointParent();
        RefreshSpawnPoints();
        ResolvePlayerReferences();
    }

    private void ResolveBatPrefab()
    {
        if (batPrefab != null) return;

#if UNITY_EDITOR
        batPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/cavehunt/Prefab/Bat Outer ring.prefab");
#endif
    }

    private void ResolveSpawnPointParent()
    {
        if (spawnPointParent != null) return;
        if (string.IsNullOrWhiteSpace(spawnPointParentName)) return;

        GameObject parentObject = GameObject.Find(spawnPointParentName);
        if (parentObject != null)
        {
            spawnPointParent = parentObject.transform;
        }
    }

    private void RefreshSpawnPoints()
    {
        spawnPoints.Clear();
        if (spawnPointParent == null) return;

        for (int i = 0; i < spawnPointParent.childCount; i++)
        {
            Transform child = spawnPointParent.GetChild(i);
            if (child != null && child.gameObject.activeInHierarchy)
            {
                spawnPoints.Add(child);
            }
        }
    }

    private void ResolvePlayerReferences()
    {
        if (playerTarget == null)
        {
            Camera camera = VrCameraResolver.GetCamera();
            playerTarget = camera != null ? camera.transform : ResolveXrOrigin();
        }

        if (playerHealth == null)
        {
            Transform healthOwner = ResolvePlayerHealthOwner(playerTarget);
            playerHealth = healthOwner != null ? healthOwner.GetComponent<PlayerHealth>() : null;
        }
    }

    private Transform ResolveXrOrigin()
    {
        GameObject xrOrigin = GameObject.Find("XR Origin (XR Rig)") ?? GameObject.Find("XR Origin (VR)");
        return xrOrigin != null ? xrOrigin.transform : null;
    }

    private Transform ResolvePlayerHealthOwner(Transform target)
    {
        Transform xrOrigin = ResolveXrOrigin();
        if (xrOrigin != null && target != null && target.IsChildOf(xrOrigin))
        {
            return xrOrigin;
        }

        return target;
    }

    private Material ResolveBulletMaterial()
    {
        if (bulletMaterial != null) return bulletMaterial;
        if (runtimeBulletMaterial != null) return runtimeBulletMaterial;

        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        if (shader == null) return null;

        runtimeBulletMaterial = new Material(shader)
        {
            name = "Bat_Outer_Ring_Bullet_Red_Runtime"
        };

        if (runtimeBulletMaterial.HasProperty("_BaseColor")) runtimeBulletMaterial.SetColor("_BaseColor", Color.red);
        if (runtimeBulletMaterial.HasProperty("_Color")) runtimeBulletMaterial.SetColor("_Color", Color.red);
        if (runtimeBulletMaterial.HasProperty("_EmissionColor")) runtimeBulletMaterial.SetColor("_EmissionColor", Color.red * 1.5f);
        if (runtimeBulletMaterial.HasProperty("_Smoothness")) runtimeBulletMaterial.SetFloat("_Smoothness", 0.2f);

        return runtimeBulletMaterial;
    }

    private int CountActiveSpawnedBats()
    {
        int activeCount = 0;
        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            if (child != null && child.gameObject.activeInHierarchy && child.GetComponent<BatEnemy>() != null)
            {
                activeCount++;
            }
        }

        return activeCount;
    }

    private void WarnOnce(ref bool warned, string message)
    {
        if (warned) return;

        warned = true;
        Debug.LogWarning(message, this);
    }
}
