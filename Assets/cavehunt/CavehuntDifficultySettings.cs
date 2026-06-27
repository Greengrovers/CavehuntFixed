using UnityEngine;

public class CavehuntDifficultySettings : MonoBehaviour
{
    [System.Serializable]
    public class DifficultyProfile
    {
        [SerializeField] private string displayName = "Normal";
        [SerializeField] private Color targetColor = new Color(1f, 0.86f, 0.2f, 1f);

        [Header("Health")]
        [SerializeField] private float playerMaxHealth = 5f;
        [SerializeField] private float batMaxHealth = 3f;
        [SerializeField] private float innerRingBatMaxHealth = 6f;
        [SerializeField] private float bossMaxHealth = 25f;

        [Header("Movement")]
        [SerializeField] private float batDescendSpeed = 1.5f;
        [SerializeField] private float tutorialBatDescendSpeed = 0.8f;
        [SerializeField] private float bossDescendSpeed = 0.9f;

        [Header("Shooting")]
        [SerializeField] private float tutorialBatBulletSpeed = 8f;

        [Header("Enemy Size")]
        [SerializeField] private float batScaleMultiplier = 1.1f;
        [SerializeField] private float innerRingBatScaleMultiplier = 1.35f;
        [SerializeField] private float bossScaleMultiplier = 5f;

        [Header("Enemy Counts")]
        [SerializeField, Min(0)] private int minActiveOuterRingBats = 1;
        [SerializeField, Min(0)] private int maxActiveOuterRingBats = 3;
        [SerializeField, Min(0)] private int minActiveInnerRingBats = 1;
        [SerializeField, Min(0)] private int maxActiveInnerRingBats = 2;
        [SerializeField, Min(1)] private int outerRingKillsToBoss = 14;
        [SerializeField, Min(1)] private int innerRingKillsToBoss = 7;

        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? "Difficulty" : displayName;
        public Color TargetColor => targetColor;
        public float PlayerMaxHealth => Mathf.Max(1f, playerMaxHealth);
        public float BatMaxHealth => Mathf.Max(1f, batMaxHealth);
        public float InnerRingBatMaxHealth => Mathf.Max(1f, innerRingBatMaxHealth);
        public float BossMaxHealth => Mathf.Max(1f, bossMaxHealth);
        public float BatDescendSpeed => Mathf.Max(0.05f, batDescendSpeed);
        public float TutorialBatDescendSpeed => Mathf.Max(0.05f, tutorialBatDescendSpeed);
        public float BossDescendSpeed => Mathf.Max(0.05f, bossDescendSpeed);
        public float TutorialBatBulletSpeed => Mathf.Max(0.1f, tutorialBatBulletSpeed);
        public float BatScaleMultiplier => Mathf.Max(0.1f, batScaleMultiplier);
        public float InnerRingBatScaleMultiplier => Mathf.Max(0.1f, innerRingBatScaleMultiplier);
        public float BossScaleMultiplier => Mathf.Max(0.1f, bossScaleMultiplier);
        public int MinActiveOuterRingBats => Mathf.Max(0, minActiveOuterRingBats);
        public int MaxActiveOuterRingBats => Mathf.Max(MinActiveOuterRingBats, maxActiveOuterRingBats);
        public int MinActiveInnerRingBats => Mathf.Max(0, minActiveInnerRingBats);
        public int MaxActiveInnerRingBats => Mathf.Max(MinActiveInnerRingBats, maxActiveInnerRingBats);
        public int OuterRingKillsToBoss => Mathf.Max(1, outerRingKillsToBoss);
        public int InnerRingKillsToBoss => Mathf.Max(1, innerRingKillsToBoss);

        public DifficultyProfile(
            string displayName,
            Color targetColor,
            float playerMaxHealth,
            float batMaxHealth,
            float innerRingBatMaxHealth,
            float bossMaxHealth,
            float batDescendSpeed,
            float tutorialBatDescendSpeed,
            float bossDescendSpeed,
            float tutorialBatBulletSpeed,
            float batScaleMultiplier,
            float innerRingBatScaleMultiplier,
            float bossScaleMultiplier,
            int minActiveOuterRingBats,
            int maxActiveOuterRingBats,
            int minActiveInnerRingBats,
            int maxActiveInnerRingBats,
            int outerRingKillsToBoss,
            int innerRingKillsToBoss)
        {
            this.displayName = displayName;
            this.targetColor = targetColor;
            this.playerMaxHealth = playerMaxHealth;
            this.batMaxHealth = batMaxHealth;
            this.innerRingBatMaxHealth = innerRingBatMaxHealth;
            this.bossMaxHealth = bossMaxHealth;
            this.batDescendSpeed = batDescendSpeed;
            this.tutorialBatDescendSpeed = tutorialBatDescendSpeed;
            this.bossDescendSpeed = bossDescendSpeed;
            this.tutorialBatBulletSpeed = tutorialBatBulletSpeed;
            this.batScaleMultiplier = batScaleMultiplier;
            this.innerRingBatScaleMultiplier = innerRingBatScaleMultiplier;
            this.bossScaleMultiplier = bossScaleMultiplier;
            this.minActiveOuterRingBats = minActiveOuterRingBats;
            this.maxActiveOuterRingBats = maxActiveOuterRingBats;
            this.minActiveInnerRingBats = minActiveInnerRingBats;
            this.maxActiveInnerRingBats = maxActiveInnerRingBats;
            this.outerRingKillsToBoss = outerRingKillsToBoss;
            this.innerRingKillsToBoss = innerRingKillsToBoss;
        }
    }

    [SerializeField] private int selectedProfileIndex = 1;
    [SerializeField] private DifficultyProfile[] profiles =
    {
        new DifficultyProfile("Easy", new Color(0.25f, 0.9f, 0.35f, 1f), 7f, 2f, 4f, 18f, 1.0f, 0.65f, 0.65f, 8f, 1.0f, 1.2f, 5f, 1, 3, 1, 2, 10, 5),
        new DifficultyProfile("Normal", new Color(1f, 0.86f, 0.2f, 1f), 5f, 3f, 6f, 25f, 1.5f, 0.8f, 0.9f, 12f, 1.1f, 1.35f, 5f, 1, 3, 1, 2, 14, 7),
        new DifficultyProfile("Hard", new Color(1f, 0.25f, 0.18f, 1f), 4f, 5f, 9f, 40f, 2.1f, 0.9f, 1.2f, 12f, 1.15f, 1.45f, 5f, 2, 5, 1, 3, 18, 9)
    };

    public int ProfileCount => Profiles.Length;
    public DifficultyProfile ActiveProfile => GetProfile(selectedProfileIndex);
    public DifficultyProfile[] Profiles => profiles != null && profiles.Length > 0 ? profiles : CreateDefaultProfiles();
    public float PlayerMaxHealth => ActiveProfile.PlayerMaxHealth;
    public float BatMaxHealth => ActiveProfile.BatMaxHealth;
    public float InnerRingBatMaxHealth => ActiveProfile.InnerRingBatMaxHealth;
    public float BossMaxHealth => ActiveProfile.BossMaxHealth;
    public float BatDescendSpeed => ActiveProfile.BatDescendSpeed;
    public float TutorialBatDescendSpeed => ActiveProfile.TutorialBatDescendSpeed;
    public float BossDescendSpeed => ActiveProfile.BossDescendSpeed;
    public float TutorialBatBulletSpeed => ActiveProfile.TutorialBatBulletSpeed;
    public float BatScaleMultiplier => ActiveProfile.BatScaleMultiplier;
    public float InnerRingBatScaleMultiplier => ActiveProfile.InnerRingBatScaleMultiplier;
    public float BossScaleMultiplier => ActiveProfile.BossScaleMultiplier;
    public int MinActiveOuterRingBats => ActiveProfile.MinActiveOuterRingBats;
    public int MaxActiveOuterRingBats => ActiveProfile.MaxActiveOuterRingBats;
    public int MinActiveInnerRingBats => ActiveProfile.MinActiveInnerRingBats;
    public int MaxActiveInnerRingBats => ActiveProfile.MaxActiveInnerRingBats;
    public int OuterRingKillsToBoss => ActiveProfile.OuterRingKillsToBoss;
    public int InnerRingKillsToBoss => ActiveProfile.InnerRingKillsToBoss;

    public static CavehuntDifficultySettings Resolve()
    {
        CavehuntDifficultySettings settings = FindAnyObjectByType<CavehuntDifficultySettings>();
        if (settings != null) return settings;

        GameObject settingsObject = new GameObject("Cavehunt Difficulty Settings");
        return settingsObject.AddComponent<CavehuntDifficultySettings>();
    }

    public DifficultyProfile GetProfile(int index)
    {
        DifficultyProfile[] resolvedProfiles = Profiles;
        return resolvedProfiles[Mathf.Clamp(index, 0, resolvedProfiles.Length - 1)];
    }

    public int SelectProfile(int index)
    {
        DifficultyProfile[] resolvedProfiles = Profiles;
        selectedProfileIndex = Mathf.Clamp(index, 0, resolvedProfiles.Length - 1);
        return selectedProfileIndex;
    }

    public void SetEnemyHealth(float newBatMaxHealth, float newBossMaxHealth)
    {
        SetEnemyHealth(newBatMaxHealth, newBatMaxHealth, newBossMaxHealth);
    }

    public void SetEnemyHealth(float newBatMaxHealth, float newInnerRingBatMaxHealth, float newBossMaxHealth)
    {
        Debug.LogWarning("SetEnemyHealth is deprecated. Edit Difficulty Profiles on Cavehunt Difficulty Settings instead.");
    }

    public void ApplyHealthTo(Damageable damageable, bool isBoss, bool resetCurrentHealth = true)
    {
        ApplyHealthTo(damageable, isBoss ? CavehuntEnemyRole.Boss : CavehuntEnemyRole.OuterRing, resetCurrentHealth);
    }

    public void ApplyHealthTo(Damageable damageable, CavehuntEnemyRole role, bool resetCurrentHealth = true)
    {
        if (damageable == null) return;

        damageable.SetMaxHealth(GetMaxHealth(role), resetCurrentHealth);
    }

    public float GetMaxHealth(CavehuntEnemyRole role)
    {
        switch (role)
        {
            case CavehuntEnemyRole.InnerRing:
                return InnerRingBatMaxHealth;
            case CavehuntEnemyRole.Boss:
                return BossMaxHealth;
            default:
                return BatMaxHealth;
        }
    }

    public float GetScaleMultiplier(CavehuntEnemyRole role)
    {
        switch (role)
        {
            case CavehuntEnemyRole.InnerRing:
                return InnerRingBatScaleMultiplier;
            case CavehuntEnemyRole.Boss:
                return BossScaleMultiplier;
            default:
                return BatScaleMultiplier;
        }
    }

    private void OnValidate()
    {
        if (profiles == null || profiles.Length == 0)
        {
            profiles = CreateDefaultProfiles();
        }

        selectedProfileIndex = Mathf.Clamp(selectedProfileIndex, 0, profiles.Length - 1);
    }

    private static DifficultyProfile[] CreateDefaultProfiles()
    {
        return new[]
        {
            new DifficultyProfile("Easy", new Color(0.25f, 0.9f, 0.35f, 1f), 7f, 2f, 4f, 18f, 1.0f, 0.65f, 0.65f, 8f, 1.0f, 1.2f, 5f, 1, 3, 1, 2, 10, 5),
            new DifficultyProfile("Normal", new Color(1f, 0.86f, 0.2f, 1f), 5f, 3f, 6f, 25f, 1.5f, 0.8f, 0.9f, 12f, 1.1f, 1.35f, 5f, 1, 3, 1, 2, 14, 7),
            new DifficultyProfile("Hard", new Color(1f, 0.25f, 0.18f, 1f), 4f, 5f, 9f, 40f, 2.1f, 0.9f, 1.2f, 12f, 1.15f, 1.45f, 5f, 2, 5, 1, 3, 18, 9)
        };
    }
}
