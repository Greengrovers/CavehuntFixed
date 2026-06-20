using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(BatEnemy))]
[RequireComponent(typeof(Damageable))]
public class BossEnemy : MonoBehaviour
{
    private const float DefaultTornadoRadius = 4.5f;
    private const int DefaultTornadoHelperCount = 16;

    [SerializeField] private bool applyDifficultyHealthOnEnable = true;
    [SerializeField] private string spawnPointName = "BossbatSpawn";
    [SerializeField] private float tornadoRadius = 4.5f;
    [SerializeField] private float tornadoTurns = 5f;
    [SerializeField] private float tornadoMoveSpeed = 4f;
    [SerializeField] private float tornadoMaxDuration = 120f;
    [SerializeField, Min(8)] private int tornadoHelperCount = 16;
    [SerializeField] private float groundY = 0.05f;

    private BatEnemy batEnemy;
    private Damageable damageable;
    private Transform spawnPoint;
    private Vector3 tornadoCenter;
    private Vector3 tornadoStart;
    private float tornadoHeight;
    private float tornadoDuration;
    private float tornadoElapsed;
    private bool tornadoActive;
    private Transform tornadoHelperRoot;
    private int tornadoHelperIndex;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateSceneTornadoHelpers()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.name != "03-Interactions") return;

        GameObject spawnObject = GameObject.Find("BossbatSpawn") ?? GameObject.Find("Boss Bat Spawn");
        if (spawnObject == null) return;

        CreateHelperRing(spawnObject.transform, DefaultTornadoRadius, DefaultTornadoHelperCount);
    }

    public void ApplyDifficulty(CavehuntDifficultySettings settings, bool resetCurrentHealth = true)
    {
        if (settings == null)
        {
            settings = CavehuntDifficultySettings.Resolve();
        }

        settings.ApplyHealthTo(GetComponent<Damageable>(), true, resetCurrentHealth);
    }

    public void PrepareForEncounter(CavehuntDifficultySettings settings)
    {
        EnsureReferences();
        ApplyDifficulty(settings);

        if (batEnemy != null)
        {
            batEnemy.SetDescendTowardGround(false);
            batEnemy.SetRespawnOnDeath(false);
        }

        tornadoActive = false;
        gameObject.SetActive(false);
    }

    public void BeginBossEncounter(Transform fallbackSpawnPoint, CavehuntDifficultySettings settings)
    {
        EnsureReferences();
        ApplyDifficulty(settings);

        if (batEnemy != null)
        {
            batEnemy.SetDescendTowardGround(false);
            batEnemy.SetRespawnOnDeath(false);
        }

        spawnPoint = ResolveSpawnPoint(fallbackSpawnPoint);
        Vector3 startPosition = spawnPoint != null ? spawnPoint.position : transform.position;
        Quaternion startRotation = spawnPoint != null ? spawnPoint.rotation : transform.rotation;

        transform.SetPositionAndRotation(startPosition, startRotation);
        gameObject.SetActive(true);

        ConfigureTornado(startPosition, startRotation);

        if (batEnemy != null && !batEnemy.EncounterStarted)
        {
            batEnemy.BeginEncounter();
        }

        transform.position = tornadoStart;
        tornadoActive = true;
    }

    private void OnEnable()
    {
        EnsureReferences();

        if (applyDifficultyHealthOnEnable)
        {
            ApplyDifficulty(CavehuntDifficultySettings.Resolve(), false);
        }

        if (batEnemy != null)
        {
            batEnemy.SetDescendTowardGround(false);
            batEnemy.SetRespawnOnDeath(false);
        }
    }

    private void LateUpdate()
    {
        if (!tornadoActive) return;

        if (batEnemy != null)
        {
            batEnemy.SetDescendTowardGround(false);
            batEnemy.SetRespawnOnDeath(false);
        }

        if (damageable != null && damageable.CurrentHealth <= 0f)
        {
            tornadoActive = false;
            return;
        }

        tornadoElapsed += Time.deltaTime;
        float t = tornadoDuration <= 0f ? 1f : Mathf.Clamp01(tornadoElapsed / tornadoDuration);
        float y = Mathf.Lerp(tornadoStart.y, groundY, t);
        Vector3 targetPosition = ResolveCurrentHelperTarget(y);
        transform.position = Vector3.MoveTowards(transform.position, targetPosition, Mathf.Max(0.1f, tornadoMoveSpeed) * Time.deltaTime);

        if (Vector3.Distance(transform.position, targetPosition) <= 0.15f)
        {
            AdvanceTornadoHelper();
        }

        FacePlayer();

        if (t >= 1f)
        {
            tornadoActive = false;
        }
    }

    private void EnsureReferences()
    {
        if (batEnemy == null)
        {
            batEnemy = GetComponent<BatEnemy>();
        }

        if (damageable == null)
        {
            damageable = GetComponent<Damageable>();
        }
    }

    private Transform ResolveSpawnPoint(Transform fallbackSpawnPoint)
    {
        if (fallbackSpawnPoint != null) return fallbackSpawnPoint;

        if (!string.IsNullOrWhiteSpace(spawnPointName))
        {
            GameObject namedSpawn = GameObject.Find(spawnPointName) ?? GameObject.Find("Boss Bat Spawn");
            if (namedSpawn != null) return namedSpawn.transform;
        }

        return null;
    }

    private void ConfigureTornado(Vector3 centerPosition, Quaternion startRotation)
    {
        Vector3 right = startRotation * Vector3.right;
        if (right.sqrMagnitude <= 0.0001f)
        {
            right = Vector3.right;
        }

        tornadoCenter = centerPosition;
        tornadoStart = centerPosition + right.normalized * Mathf.Max(0f, tornadoRadius);
        tornadoHeight = Mathf.Max(0f, tornadoStart.y - groundY);
        tornadoElapsed = 0f;
        tornadoHelperIndex = 0;
        EnsureTornadoHelpers(startRotation);

        float circumferenceDistance = Mathf.Max(0f, tornadoTurns) * 2f * Mathf.PI * Mathf.Max(0f, tornadoRadius);
        float pathLength = Mathf.Sqrt(tornadoHeight * tornadoHeight + circumferenceDistance * circumferenceDistance);
        float requestedDuration = pathLength / Mathf.Max(0.1f, tornadoMoveSpeed);
        tornadoDuration = Mathf.Min(Mathf.Max(0.1f, requestedDuration), Mathf.Max(0.1f, tornadoMaxDuration));

        Debug.Log($"Boss tornado path is about {pathLength:F1}m and will take {tornadoDuration:F1}s.");
    }

    private void EnsureTornadoHelpers(Quaternion orientation)
    {
        if (tornadoHelperRoot == null)
        {
            tornadoHelperRoot = CreateHelperRing(spawnPoint, Mathf.Max(0f, tornadoRadius), Mathf.Max(8, tornadoHelperCount));
        }

        tornadoHelperRoot.position = tornadoCenter;
        tornadoHelperRoot.rotation = orientation;
        if (spawnPoint != null)
        {
            tornadoHelperRoot.SetParent(spawnPoint, true);
        }

        int helperCount = Mathf.Max(8, tornadoHelperCount);
        while (tornadoHelperRoot.childCount < helperCount)
        {
            GameObject helper = new GameObject($"Boss Tornado Helper {tornadoHelperRoot.childCount + 1:00}");
            helper.transform.SetParent(tornadoHelperRoot, false);
        }

        for (int i = 0; i < tornadoHelperRoot.childCount; i++)
        {
            Transform helper = tornadoHelperRoot.GetChild(i);
            bool active = i < helperCount;
            helper.gameObject.SetActive(active);
            if (!active) continue;

            float angle = i / (float)helperCount * Mathf.PI * 2f;
            Vector3 localPosition = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * Mathf.Max(0f, tornadoRadius);
            helper.localPosition = localPosition;
            helper.localRotation = Quaternion.identity;
        }
    }

    private static Transform CreateHelperRing(Transform center, float radius, int count)
    {
        GameObject root = GameObject.Find("Boss Tornado Path Helpers");
        if (root == null)
        {
            root = new GameObject("Boss Tornado Path Helpers");
        }

        Transform rootTransform = root.transform;
        if (center != null)
        {
            rootTransform.SetParent(center, false);
        }

        rootTransform.localPosition = Vector3.zero;
        rootTransform.localRotation = Quaternion.identity;
        rootTransform.localScale = Vector3.one;

        int helperCount = Mathf.Max(8, count);
        while (rootTransform.childCount < helperCount)
        {
            GameObject helper = new GameObject($"Boss Tornado Helper {rootTransform.childCount + 1:00}");
            helper.transform.SetParent(rootTransform, false);
        }

        for (int i = 0; i < rootTransform.childCount; i++)
        {
            Transform helper = rootTransform.GetChild(i);
            bool active = i < helperCount;
            helper.gameObject.SetActive(active);
            if (!active) continue;

            float angle = i / (float)helperCount * Mathf.PI * 2f;
            helper.localPosition = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * Mathf.Max(0f, radius);
            helper.localRotation = Quaternion.identity;
            helper.localScale = Vector3.one;
        }

        return rootTransform;
    }

    private Vector3 ResolveCurrentHelperTarget(float y)
    {
        if (tornadoHelperRoot == null || tornadoHelperRoot.childCount == 0)
        {
            float fallbackAngle = tornadoElapsed * tornadoMoveSpeed / Mathf.Max(0.1f, tornadoRadius);
            Vector3 fallbackOffset = new Vector3(Mathf.Cos(fallbackAngle), 0f, Mathf.Sin(fallbackAngle)) * Mathf.Max(0f, tornadoRadius);
            Vector3 fallbackPosition = tornadoCenter + fallbackOffset;
            fallbackPosition.y = y;
            return fallbackPosition;
        }

        int helperCount = Mathf.Max(1, Mathf.Min(tornadoHelperCount, tornadoHelperRoot.childCount));
        int wrappedIndex = ((tornadoHelperIndex % helperCount) + helperCount) % helperCount;
        Vector3 targetPosition = tornadoHelperRoot.GetChild(wrappedIndex).position;
        targetPosition.y = y;
        return targetPosition;
    }

    private void AdvanceTornadoHelper()
    {
        int helperCount = tornadoHelperRoot != null ? Mathf.Max(1, Mathf.Min(tornadoHelperCount, tornadoHelperRoot.childCount)) : 1;
        tornadoHelperIndex = (tornadoHelperIndex + 1) % helperCount;
    }

    private void FacePlayer()
    {
        Camera camera = VrCameraResolver.GetCamera();
        Transform target = camera != null ? camera.transform : null;
        if (target == null)
        {
            PlayerHealth playerHealth = FindAnyObjectByType<PlayerHealth>();
            target = playerHealth != null ? playerHealth.transform : null;
        }
        if (target == null) return;

        Vector3 direction = target.position - transform.position;
        if (direction.sqrMagnitude <= 0.0001f) return;
        transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
    }
}
