using UnityEngine;
using UnityEngine.Events;

public class CavehuntEncounterDirector : MonoBehaviour
{
    private enum EncounterPhase
    {
        WaitingForBow,
        Tutorial,
        RingFight,
        Boss,
        Victory
    }

    [Header("Progression")]
    [SerializeField, Min(1)] private int tutorialKillsToStartRings = 2;
    [SerializeField, Min(1)] private int outerRingKillsToBoss = 14;
    [SerializeField, Min(1)] private int innerRingKillsToBoss = 7;

    [Header("Ring Spawners")]
    [SerializeField] private BatOuterRingSpawner outerRingSpawner;
    [SerializeField] private BatOuterRingSpawner innerRingSpawner;
    [SerializeField, Min(1)] private int innerRingSpawnPointStep = 6;
    [SerializeField, Range(0.05f, 0.95f)] private float innerRingInwardProjection = 0.55f;
    [SerializeField, Min(0.1f)] private float outerRingSpawnInterval = 3f;
    [SerializeField, Min(0.1f)] private float innerRingSpawnInterval = 4f;
    [SerializeField, Min(0)] private int maxActiveOuterRingBats = 3;
    [SerializeField, Min(0)] private int maxActiveInnerRingBats = 2;

    [Header("Boss")]
    [SerializeField] private BossEnemy bossEnemy;
    [SerializeField] private Transform bossSpawnPoint;
    [SerializeField] private string bossSpawnPointName = "BossbatSpawn";

    [Header("Events")]
    [SerializeField] private UnityEvent onVictory;

    private CavehuntDifficultySettings difficultySettings;
    private EncounterPhase phase = EncounterPhase.WaitingForBow;
    private int tutorialKills;
    private int outerRingKills;
    private int innerRingKills;

    public static CavehuntEncounterDirector Resolve(bool createIfMissing = true)
    {
        CavehuntEncounterDirector director = FindAnyObjectByType<CavehuntEncounterDirector>(FindObjectsInactive.Include);
        if (director != null || !createIfMissing) return director;

        GameObject directorObject = new GameObject("Cavehunt Encounter Director");
        return directorObject.AddComponent<CavehuntEncounterDirector>();
    }

    public void BeginRun()
    {
        ResolveReferences();

        tutorialKills = 0;
        outerRingKills = 0;
        innerRingKills = 0;
        phase = EncounterPhase.Tutorial;

        StopRingSpawners();
        PrepareBoss();
        BeginTutorialEnemies();

        Debug.Log("Cavehunt encounter started: Tutorial phase.");
    }

    public void ResetForBowPickup()
    {
        ResolveReferences();

        phase = EncounterPhase.WaitingForBow;
        tutorialKills = 0;
        outerRingKills = 0;
        innerRingKills = 0;

        ResetEnemiesByRole(CavehuntEnemyRole.Tutorial);
        StopRingSpawners();
        PrepareBoss();
    }

    public void ReportEnemyDefeated(CavehuntEnemyRole role, CavehuntEnemyKillTracker tracker)
    {
        if (phase == EncounterPhase.Victory || phase == EncounterPhase.WaitingForBow) return;

        if (role == CavehuntEnemyRole.Boss)
        {
            CompleteVictory();
            return;
        }

        if (phase == EncounterPhase.Tutorial && role == CavehuntEnemyRole.Tutorial)
        {
            tutorialKills++;
            if (tutorialKills >= tutorialKillsToStartRings)
            {
                StartRingFight();
            }

            return;
        }

        if (phase != EncounterPhase.RingFight) return;

        if (role == CavehuntEnemyRole.OuterRing)
        {
            outerRingKills++;
        }
        else if (role == CavehuntEnemyRole.InnerRing)
        {
            innerRingKills++;
        }

        if (outerRingKills >= outerRingKillsToBoss && innerRingKills >= innerRingKillsToBoss)
        {
            StartBossFight();
        }
    }

    private void Awake()
    {
        ResolveReferences();
        ResetForBowPickup();
    }

    private void ResolveReferences()
    {
        difficultySettings = CavehuntDifficultySettings.Resolve();
        ResolveOuterRingSpawner();
        ResolveInnerRingSpawner();
        ResolveBossEnemy();
        ResolveBossSpawnPoint();
    }

    private void ResolveOuterRingSpawner()
    {
        if (outerRingSpawner != null) return;

        BatOuterRingSpawner[] spawners = FindObjectsByType<BatOuterRingSpawner>(FindObjectsInactive.Include);
        for (int i = 0; i < spawners.Length; i++)
        {
            if (spawners[i] != null && spawners[i].name != "Bat Inner Ring Spawner")
            {
                outerRingSpawner = spawners[i];
                return;
            }
        }
    }

    private void ResolveInnerRingSpawner()
    {
        if (innerRingSpawner == null)
        {
            GameObject innerSpawnerObject = GameObject.Find("Bat Inner Ring Spawner");
            if (innerSpawnerObject == null)
            {
                innerSpawnerObject = new GameObject("Bat Inner Ring Spawner");
            }

            innerRingSpawner = innerSpawnerObject.GetComponent<BatOuterRingSpawner>();
            if (innerRingSpawner == null)
            {
                innerRingSpawner = innerSpawnerObject.AddComponent<BatOuterRingSpawner>();
            }
        }

        innerRingSpawner.CopyRuntimeReferencesFrom(outerRingSpawner);
        innerRingSpawner.ConfigureRuntime(
            CavehuntEnemyRole.InnerRing,
            "Bat Outer Ring Spawn Points",
            "Bat Inner Ring",
            innerRingSpawnPointStep,
            innerRingInwardProjection,
            innerRingSpawnInterval,
            maxActiveInnerRingBats,
            innerRingKillsToBoss
        );
    }

    private void ResolveBossEnemy()
    {
        if (bossEnemy != null) return;

        bossEnemy = FindAnyObjectByType<BossEnemy>(FindObjectsInactive.Include);
    }

    private void ResolveBossSpawnPoint()
    {
        if (bossSpawnPoint != null) return;
        if (string.IsNullOrWhiteSpace(bossSpawnPointName)) return;

        GameObject spawnObject = GameObject.Find(bossSpawnPointName) ?? GameObject.Find("Boss Bat Spawn");
        if (spawnObject != null)
        {
            bossSpawnPoint = spawnObject.transform;
        }
    }

    private void BeginTutorialEnemies()
    {
        BatEnemy[] enemies = FindObjectsByType<BatEnemy>(FindObjectsInactive.Include);
        for (int i = 0; i < enemies.Length; i++)
        {
            BatEnemy enemy = enemies[i];
            if (enemy == null) continue;

            CavehuntEnemyKillTracker tracker = enemy.GetComponent<CavehuntEnemyKillTracker>();
            if (tracker == null || tracker.Role != CavehuntEnemyRole.Tutorial) continue;

            enemy.gameObject.SetActive(true);
            enemy.BeginEncounter();
        }
    }

    private void ResetEnemiesByRole(CavehuntEnemyRole role)
    {
        CavehuntEnemyKillTracker[] trackers = FindObjectsByType<CavehuntEnemyKillTracker>(FindObjectsInactive.Include);
        for (int i = 0; i < trackers.Length; i++)
        {
            CavehuntEnemyKillTracker tracker = trackers[i];
            if (tracker == null || tracker.Role != role) continue;

            BatEnemy enemy = tracker.GetComponent<BatEnemy>();
            if (enemy != null)
            {
                enemy.ResetForBowPickup();
            }
        }
    }

    private void StartRingFight()
    {
        phase = EncounterPhase.RingFight;
        ResetEnemiesByRole(CavehuntEnemyRole.Tutorial);

        if (outerRingSpawner != null)
        {
            outerRingSpawner.ConfigureRuntime(
                CavehuntEnemyRole.OuterRing,
                "Bat Outer Ring Spawn Points",
                "Bat Outer Ring",
                1,
                0f,
                outerRingSpawnInterval,
                maxActiveOuterRingBats,
                outerRingKillsToBoss
            );
            outerRingSpawner.gameObject.SetActive(true);
            outerRingSpawner.StartSpawning();
        }

        if (innerRingSpawner != null)
        {
            innerRingSpawner.gameObject.SetActive(true);
            innerRingSpawner.StartSpawning();
        }

        Debug.Log("Cavehunt encounter advanced: Inner and outer ring phase.");
    }

    private void StartBossFight()
    {
        phase = EncounterPhase.Boss;
        StopRingSpawners();

        if (bossEnemy != null)
        {
            bossEnemy.BeginBossEncounter(bossSpawnPoint, difficultySettings);
        }

        Debug.Log("Cavehunt encounter advanced: Boss phase.");
    }

    private void StopRingSpawners()
    {
        if (outerRingSpawner != null)
        {
            outerRingSpawner.ResetForBowPickup();
        }

        if (innerRingSpawner != null)
        {
            innerRingSpawner.ResetForBowPickup();
        }
    }

    private void PrepareBoss()
    {
        if (bossEnemy != null)
        {
            bossEnemy.PrepareForEncounter(difficultySettings);
        }
    }

    private void CompleteVictory()
    {
        if (phase == EncounterPhase.Victory) return;

        phase = EncounterPhase.Victory;
        StopRingSpawners();
        PlayerHealth playerHealth = FindAnyObjectByType<PlayerHealth>(FindObjectsInactive.Include);
        if (playerHealth != null)
        {
            playerHealth.ShowGameWonMenu();
        }

        onVictory?.Invoke();
        Debug.Log("Cavehunt encounter complete: Boss defeated. Victory.");
    }
}
