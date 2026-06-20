using UnityEngine;

public class CavehuntDifficultySettings : MonoBehaviour
{
    [Header("Enemy Health")]
    [SerializeField] private float batMaxHealth = 3f;
    [SerializeField] private float innerRingBatMaxHealth = 6f;
    [SerializeField] private float bossMaxHealth = 25f;

    [Header("Enemy Size")]
    [SerializeField] private float batScaleMultiplier = 1.1f;
    [SerializeField] private float innerRingBatScaleMultiplier = 1.35f;
    [SerializeField] private float bossScaleMultiplier = 5f;

    public float BatMaxHealth => Mathf.Max(1f, batMaxHealth);
    public float InnerRingBatMaxHealth => Mathf.Max(1f, innerRingBatMaxHealth);
    public float BossMaxHealth => Mathf.Max(1f, bossMaxHealth);
    public float BatScaleMultiplier => Mathf.Max(0.1f, batScaleMultiplier);
    public float InnerRingBatScaleMultiplier => Mathf.Max(0.1f, innerRingBatScaleMultiplier);
    public float BossScaleMultiplier => Mathf.Max(0.1f, bossScaleMultiplier);

    public static CavehuntDifficultySettings Resolve()
    {
        CavehuntDifficultySettings settings = FindAnyObjectByType<CavehuntDifficultySettings>();
        if (settings != null) return settings;

        GameObject settingsObject = new GameObject("Cavehunt Difficulty Settings");
        return settingsObject.AddComponent<CavehuntDifficultySettings>();
    }

    public void SetEnemyHealth(float newBatMaxHealth, float newBossMaxHealth)
    {
        batMaxHealth = Mathf.Max(1f, newBatMaxHealth);
        bossMaxHealth = Mathf.Max(1f, newBossMaxHealth);
    }

    public void SetEnemyHealth(float newBatMaxHealth, float newInnerRingBatMaxHealth, float newBossMaxHealth)
    {
        batMaxHealth = Mathf.Max(1f, newBatMaxHealth);
        innerRingBatMaxHealth = Mathf.Max(1f, newInnerRingBatMaxHealth);
        bossMaxHealth = Mathf.Max(1f, newBossMaxHealth);
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
}
