using UnityEngine;
using UnityEngine.Events;

public class CavehuntEncounterDirector : MonoBehaviour
{
    private enum EncounterPhase
    {
        WaitingForBow,
        DifficultySelection,
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
    [SerializeField, Min(0)] private int minActiveOuterRingBats = 1;
    [SerializeField, Min(0)] private int maxActiveOuterRingBats = 3;
    [SerializeField, Min(0)] private int minActiveInnerRingBats = 1;
    [SerializeField, Min(0)] private int maxActiveInnerRingBats = 2;

    [Header("Boss")]
    [SerializeField] private BossEnemy bossEnemy;
    [SerializeField] private Transform bossSpawnPoint;
    [SerializeField] private string bossSpawnPointName = "BossbatSpawn";

    [Header("Events")]
    [SerializeField] private UnityEvent onVictory;

    private CavehuntDifficultySettings difficultySettings;
    private CavehuntDifficultySelector difficultySelector;
    private EncounterPhase phase = EncounterPhase.WaitingForBow;
    private int tutorialKills;
    private int outerRingKills;
    private int innerRingKills;

    public int OuterRingKills => outerRingKills;
    public int InnerRingKills => innerRingKills;
    public int OuterRingKillsToBoss => outerRingKillsToBoss;
    public int InnerRingKillsToBoss => innerRingKillsToBoss;

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
        phase = EncounterPhase.DifficultySelection;

        StopRingSpawners();
        PrepareBoss();
        ProceduralGameAudio.StopBossMusic();

        if (difficultySelector != null)
        {
            difficultySelector.ShowSelection(this, difficultySettings);
            Debug.Log("Cavehunt encounter waiting for difficulty selection.");
            return;
        }

        SelectDifficulty(0);
    }

    public void SelectDifficulty(int profileIndex)
    {
        ResolveReferences();

        int selectedIndex = difficultySettings != null ? difficultySettings.SelectProfile(profileIndex) : 0;
        ApplySelectedDifficulty();

        tutorialKills = 0;
        outerRingKills = 0;
        innerRingKills = 0;
        phase = EncounterPhase.Tutorial;

        StopRingSpawners();
        PrepareBoss();
        BeginTutorialEnemies();

        string difficultyName = difficultySettings != null ? difficultySettings.GetProfile(selectedIndex).DisplayName : "Default";
        Debug.Log($"Cavehunt encounter started: {difficultyName} difficulty.");
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
        ProceduralGameAudio.StopBossMusic();
        if (difficultySelector != null)
        {
            difficultySelector.HideSelection();
        }
    }

    public void ReportEnemyDefeated(CavehuntEnemyRole role, CavehuntEnemyKillTracker tracker)
    {
        if (phase == EncounterPhase.Victory || phase == EncounterPhase.WaitingForBow) return;

        if (role == CavehuntEnemyRole.Boss)
        {
            AwardScoreForDefeat(role, tracker);
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
            AwardScoreForDefeat(role, tracker);
            outerRingKills++;
        }
        else if (role == CavehuntEnemyRole.InnerRing)
        {
            AwardScoreForDefeat(role, tracker);
            innerRingKills++;
        }

        if (outerRingKills >= outerRingKillsToBoss && innerRingKills >= innerRingKillsToBoss)
        {
            StartBossFight();
        }
    }


    private void AwardScoreForDefeat(CavehuntEnemyRole role, CavehuntEnemyKillTracker tracker)
    {
        if (role == CavehuntEnemyRole.Tutorial) return;

        Vector3 scorePosition = tracker != null ? tracker.transform.position : Vector3.zero;
        CavehuntScoreSystem.AddEnemyScore(role, scorePosition);
    }
    private void Awake()
    {
        ResolveReferences();
        ResetForBowPickup();
    }

    private void ResolveReferences()
    {
        difficultySettings = CavehuntDifficultySettings.Resolve();
        ResolveDifficultySelector();
        ResolveOuterRingSpawner();
        ResolveInnerRingSpawner();
        ResolveBossEnemy();
        ResolveBossSpawnPoint();
    }

    private void ResolveDifficultySelector()
    {
        if (difficultySelector != null) return;

        difficultySelector = FindAnyObjectByType<CavehuntDifficultySelector>(FindObjectsInactive.Include);
        if (difficultySelector != null) return;

        GameObject selectorObject = GameObject.Find("Cavehunt Difficulty Selector");
        if (selectorObject == null)
        {
            selectorObject = new GameObject("Cavehunt Difficulty Selector");
        }

        difficultySelector = selectorObject.GetComponent<CavehuntDifficultySelector>();
        if (difficultySelector == null)
        {
            difficultySelector = selectorObject.AddComponent<CavehuntDifficultySelector>();
        }
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
            minActiveInnerRingBats,
            maxActiveInnerRingBats,
            innerRingKillsToBoss
        );
    }

    private void ResolveBossEnemy()
    {
        if (bossEnemy != null) return;

        bossEnemy = FindAnyObjectByType<BossEnemy>(FindObjectsInactive.Include);
        if (bossEnemy != null) return;

        bossEnemy = BatEncounterBootstrap.EnsureBossEnemyExists(this);
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
            if (difficultySettings != null)
            {
                enemy.SetDescendSpeed(difficultySettings.TutorialBatDescendSpeed);
            }
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
                minActiveOuterRingBats,
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
        ResolveReferences();

        if (bossEnemy == null)
        {
            Debug.LogWarning("Cavehunt encounter could not start boss phase because no BossEnemy exists.");
            return;
        }

        phase = EncounterPhase.Boss;
        StopRingSpawners();
        bossEnemy.BeginBossEncounter(bossSpawnPoint, difficultySettings);
        ProceduralGameAudio.StartBossMusic();

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
        ResolveBossEnemy();

        if (bossEnemy != null)
        {
            bossEnemy.PrepareForEncounter(difficultySettings);
        }
    }

    private void ApplySelectedDifficulty()
    {
        if (difficultySettings == null) return;

        outerRingKillsToBoss = difficultySettings.OuterRingKillsToBoss;
        innerRingKillsToBoss = difficultySettings.InnerRingKillsToBoss;
        minActiveOuterRingBats = difficultySettings.MinActiveOuterRingBats;
        maxActiveOuterRingBats = difficultySettings.MaxActiveOuterRingBats;
        minActiveInnerRingBats = difficultySettings.MinActiveInnerRingBats;
        maxActiveInnerRingBats = difficultySettings.MaxActiveInnerRingBats;

        PlayerHealth playerHealth = FindAnyObjectByType<PlayerHealth>(FindObjectsInactive.Include);
        if (playerHealth != null)
        {
            playerHealth.SetMaxHealth(difficultySettings.PlayerMaxHealth);
        }

        ApplyDifficultyToExistingEnemies();
        if (bossEnemy != null)
        {
            bossEnemy.SetDescendSpeed(difficultySettings.BossDescendSpeed);
        }

        ResolveInnerRingSpawner();
    }

    private float GetDescendSpeedForRole(CavehuntEnemyRole role)
    {
        if (difficultySettings == null) return 1.5f;

        switch (role)
        {
            case CavehuntEnemyRole.Tutorial:
                return difficultySettings.TutorialBatDescendSpeed;
            case CavehuntEnemyRole.Boss:
                return difficultySettings.BossDescendSpeed;
            default:
                return difficultySettings.BatDescendSpeed;
        }
    }

    private void ApplyDifficultyToExistingEnemies()
    {
        CavehuntEnemyKillTracker[] trackers = FindObjectsByType<CavehuntEnemyKillTracker>(FindObjectsInactive.Include);
        for (int i = 0; i < trackers.Length; i++)
        {
            CavehuntEnemyKillTracker tracker = trackers[i];
            if (tracker == null) continue;

            Damageable damageable = tracker.GetComponent<Damageable>();
            difficultySettings.ApplyHealthTo(damageable, tracker.Role);

            BatEnemy enemy = tracker.GetComponent<BatEnemy>();
            if (enemy != null)
            {
                enemy.SetDescendSpeed(GetDescendSpeedForRole(tracker.Role));
            }
        }
    }

    private void CompleteVictory()
    {
        if (phase == EncounterPhase.Victory) return;

        phase = EncounterPhase.Victory;
        StopRingSpawners();
        ProceduralGameAudio.StopBossMusic(false);
        CavehuntRuntimeCleanup.DestroyGameplayLeftovers();
        PlayerHealth playerHealth = FindAnyObjectByType<PlayerHealth>(FindObjectsInactive.Include);
        if (playerHealth != null)
        {
            playerHealth.ClearDamageFlash();
            BowStartExperience.HideAllBowsForPlayerDeath();
            playerHealth.ShowGameWonMenu();
        }

        onVictory?.Invoke();
        Debug.Log("Cavehunt encounter complete: Boss defeated. Victory.");
    }
}
