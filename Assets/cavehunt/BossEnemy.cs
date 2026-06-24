using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(BatEnemy))]
[RequireComponent(typeof(Damageable))]
public class BossEnemy : MonoBehaviour
{
    private const float DefaultFallbackStartRadius = 18f;
    private const float DefaultTornadoEndRadius = 1.5f;
    private const int DefaultTornadoHelperCount = 24;

    [SerializeField] private bool applyDifficultyHealthOnEnable = true;
    [SerializeField] private string spawnPointName = "BossbatSpawn";
    [SerializeField] private float fallbackStartRadius = DefaultFallbackStartRadius;
    [SerializeField] private float tornadoEndRadius = DefaultTornadoEndRadius;
    [SerializeField] private float tornadoTurns = 5f;
    [SerializeField] private float tornadoMoveSpeed = 4f;
    [SerializeField] private float tornadoDescendSpeed = 0.9f;
    [SerializeField, Min(8)] private int tornadoHelperCount = DefaultTornadoHelperCount;
    [SerializeField] private float groundY = 0.05f;
    [SerializeField, Range(-180f, 180f)] private float facePlayerYawOffsetDegrees = -67f;

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
    private Vector3 tornadoRight = Vector3.right;
    private Vector3 tornadoForward = Vector3.forward;
    private float activeStartRadius;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateSceneTornadoHelpers()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.name != "03-Interactions") return;

        GameObject spawnObject = GameObject.Find("BossbatSpawn") ?? GameObject.Find("Boss Bat Spawn");
        if (spawnObject == null) return;

        Vector3 centerPosition = ResolveArenaCenter(spawnObject.transform.position);
        float startRadius = CalculateHorizontalRadius(spawnObject.transform.position, centerPosition, DefaultFallbackStartRadius);
        CreateHelperSpiral(spawnObject.transform, centerPosition, startRadius, DefaultTornadoEndRadius, 5f, 0.05f, DefaultTornadoHelperCount);
    }

    public void ApplyDifficulty(CavehuntDifficultySettings settings, bool resetCurrentHealth = true)
    {
        if (settings == null)
        {
            settings = CavehuntDifficultySettings.Resolve();
        }

        settings.ApplyHealthTo(GetComponent<Damageable>(), true, resetCurrentHealth);
    }

    public void SetDescendSpeed(float speed)
    {
        tornadoDescendSpeed = Mathf.Max(0.05f, speed);
    }

    public void PrepareForEncounter(CavehuntDifficultySettings settings)
    {
        EnsureReferences();
        ResetBatEnemyForBoss();
        ApplyDifficulty(settings);
        if (settings != null)
        {
            SetDescendSpeed(settings.BossDescendSpeed);
        }

        tornadoActive = false;
        gameObject.SetActive(false);
    }

    public void ConfigureTornadoPath(float endRadius = DefaultTornadoEndRadius, int helperCount = DefaultTornadoHelperCount)
    {
        tornadoEndRadius = Mathf.Max(0f, endRadius);
        tornadoHelperCount = Mathf.Max(8, helperCount);
        tornadoHelperRoot = null;
    }

    public void BeginBossEncounter(Transform fallbackSpawnPoint, CavehuntDifficultySettings settings)
    {
        EnsureReferences();
        ResetBatEnemyForBoss();
        ApplyDifficulty(settings);
        if (settings != null)
        {
            SetDescendSpeed(settings.BossDescendSpeed);
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

    private void ResetBatEnemyForBoss()
    {
        if (batEnemy == null) return;

        batEnemy.ResetForBowPickup();
        batEnemy.SetDescendTowardGround(false);
        batEnemy.SetRespawnOnDeath(false);
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
        transform.position = ResolveTornadoPosition(t);

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
        tornadoCenter = ResolveArenaCenter(centerPosition);

        Vector3 right = Vector3.ProjectOnPlane(centerPosition - tornadoCenter, Vector3.up);
        if (right.sqrMagnitude <= 0.0001f)
        {
            right = Vector3.ProjectOnPlane(startRotation * Vector3.right, Vector3.up);
        }
        if (right.sqrMagnitude <= 0.0001f)
        {
            right = Vector3.ProjectOnPlane(startRotation * Vector3.forward, Vector3.up);
        }
        if (right.sqrMagnitude <= 0.0001f)
        {
            right = Vector3.right;
        }

        tornadoRight = right.normalized;
        tornadoForward = Vector3.Cross(Vector3.up, tornadoRight).normalized;
        if (tornadoForward.sqrMagnitude <= 0.0001f)
        {
            tornadoForward = Vector3.forward;
        }

        activeStartRadius = CalculateHorizontalRadius(centerPosition, tornadoCenter, fallbackStartRadius);
        tornadoEndRadius = Mathf.Clamp(tornadoEndRadius, 0f, activeStartRadius);
        tornadoStart = centerPosition;
        tornadoHeight = Mathf.Max(0f, tornadoStart.y - groundY);
        tornadoElapsed = 0f;
        EnsureTornadoHelpers();

        float averageRadius = (Mathf.Max(0f, activeStartRadius) + Mathf.Max(0f, tornadoEndRadius)) * 0.5f;
        float circumferenceDistance = Mathf.Max(0f, tornadoTurns) * 2f * Mathf.PI * averageRadius;
        float pathLength = Mathf.Sqrt(tornadoHeight * tornadoHeight + circumferenceDistance * circumferenceDistance);
        float horizontalDuration = pathLength / Mathf.Max(0.1f, tornadoMoveSpeed);
        tornadoDuration = Mathf.Max(0.1f, tornadoHeight / Mathf.Max(0.05f, tornadoDescendSpeed));

        Debug.Log($"Boss tornado path is about {pathLength:F1}m. Vertical descent will take {tornadoDuration:F1}s. Horizontal path would take {horizontalDuration:F1}s at move speed {tornadoMoveSpeed:F2}m/s.");
    }

    private void EnsureTornadoHelpers()
    {
        if (tornadoHelperRoot == null)
        {
            tornadoHelperRoot = CreateHelperSpiral(spawnPoint, tornadoCenter, Mathf.Max(0f, activeStartRadius), Mathf.Max(0f, tornadoEndRadius), Mathf.Max(0f, tornadoTurns), groundY, Mathf.Max(8, tornadoHelperCount));
        }

        if (spawnPoint != null)
        {
            tornadoHelperRoot.SetParent(spawnPoint, true);
        }
        tornadoHelperRoot.position = tornadoCenter;
        tornadoHelperRoot.rotation = Quaternion.identity;

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

            float t = helperCount <= 1 ? 1f : i / (float)(helperCount - 1);
            float angle = Mathf.Max(0f, tornadoTurns) * Mathf.PI * 2f * t;
            float radius = Mathf.Lerp(Mathf.Max(0f, activeStartRadius), Mathf.Max(0f, tornadoEndRadius), t);
            Vector3 localPosition = (Mathf.Cos(angle) * tornadoRight + Mathf.Sin(angle) * tornadoForward) * radius;
            localPosition.y = Mathf.Lerp(0f, groundY - tornadoCenter.y, t);
            helper.localPosition = localPosition;
            helper.localRotation = Quaternion.identity;
        }
    }

    private static Transform CreateHelperSpiral(Transform parent, Vector3 centerPosition, float startRadius, float endRadius, float turns, float groundY, int count)
    {
        GameObject root = GameObject.Find("Boss Tornado Path Helpers");
        if (root == null)
        {
            root = new GameObject("Boss Tornado Path Helpers");
        }

        Transform rootTransform = root.transform;
        if (parent != null)
        {
            rootTransform.SetParent(parent, true);
        }

        rootTransform.position = centerPosition;
        rootTransform.rotation = Quaternion.identity;
        rootTransform.localScale = Vector3.one;

        Vector3 startDirection = parent != null ? Vector3.ProjectOnPlane(parent.position - centerPosition, Vector3.up) : Vector3.right;
        if (startDirection.sqrMagnitude <= 0.0001f)
        {
            startDirection = Vector3.right;
        }
        startDirection.Normalize();
        Vector3 sideDirection = Vector3.Cross(Vector3.up, startDirection).normalized;
        if (sideDirection.sqrMagnitude <= 0.0001f)
        {
            sideDirection = Vector3.forward;
        }

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

            float t = helperCount <= 1 ? 1f : i / (float)(helperCount - 1);
            float angle = Mathf.Max(0f, turns) * Mathf.PI * 2f * t;
            float radius = Mathf.Lerp(Mathf.Max(0f, startRadius), Mathf.Max(0f, endRadius), t);
            Vector3 localPosition = (Mathf.Cos(angle) * startDirection + Mathf.Sin(angle) * sideDirection) * radius;
            localPosition.y = Mathf.Lerp(0f, groundY - centerPosition.y, t);
            helper.localPosition = localPosition;
            helper.localRotation = Quaternion.identity;
            helper.localScale = Vector3.one;
        }

        return rootTransform;
    }

    private Vector3 ResolveTornadoPosition(float t)
    {
        float angle = Mathf.Max(0f, tornadoTurns) * Mathf.PI * 2f * t;
        float radius = Mathf.Lerp(Mathf.Max(0f, activeStartRadius), Mathf.Max(0f, tornadoEndRadius), t);
        Vector3 horizontalOffset = (Mathf.Cos(angle) * tornadoRight + Mathf.Sin(angle) * tornadoForward) * radius;
        Vector3 position = tornadoCenter + horizontalOffset;
        position.y = Mathf.Lerp(tornadoStart.y, groundY, t);
        return position;
    }

    private static Vector3 ResolveArenaCenter(Vector3 referencePosition)
    {
        GameObject centerObject = GameObject.Find("BossTornadoCenter")
            ?? GameObject.Find("Boss Arena Center")
            ?? GameObject.Find("Arena Center")
            ?? GameObject.Find("Mitte");

        if (centerObject != null)
        {
            Vector3 center = centerObject.transform.position;
            center.y = referencePosition.y;
            return center;
        }

        return new Vector3(0f, referencePosition.y, 0f);
    }

    private static float CalculateHorizontalRadius(Vector3 position, Vector3 center, float fallbackRadius)
    {
        Vector3 offset = Vector3.ProjectOnPlane(position - center, Vector3.up);
        return offset.sqrMagnitude > 0.0001f ? offset.magnitude : Mathf.Max(0f, fallbackRadius);
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
        transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up) *
            Quaternion.Euler(0f, facePlayerYawOffsetDegrees, 0f);
    }
}
