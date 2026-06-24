using UnityEngine;

[DisallowMultipleComponent]
public class CavehuntScoreSystem : MonoBehaviour
{
    [SerializeField] private int outerRingBasePoints = 100;
    [SerializeField] private int innerRingBasePoints = 200;
    [SerializeField] private int bossBasePoints = 300;
    [SerializeField] private float groundY = 0.05f;
    [SerializeField] private float multiplierPerMeter = 0.1f;
    [SerializeField] private float maxHeightMultiplier = 5f;

    private static CavehuntScoreSystem instance;
    private static int currentScore;
    private int score;

    public static int Score => currentScore;

    public static CavehuntScoreSystem Resolve()
    {
        if (instance != null) return instance;

        instance = FindAnyObjectByType<CavehuntScoreSystem>(FindObjectsInactive.Include);
        if (instance != null) return instance;

        GameObject scoreObject = new GameObject("Cavehunt Score System");
        instance = scoreObject.AddComponent<CavehuntScoreSystem>();
        return instance;
    }

    public static void ResetScore()
    {
        currentScore = 0;
        CavehuntScoreSystem resolved = Resolve();
        if (resolved != null)
        {
            resolved.score = 0;
        }
    }

    public static int LockFinalScore(int minimumScore = 0)
    {
        CavehuntScoreSystem resolved = Resolve();
        if (resolved != null)
        {
            resolved.score = currentScore;
        }

        Debug.Log($"Final score kept dynamic: {currentScore}");
        return currentScore;
    }
public static int CalculateMinimumVictoryScore(int outerRingKills, int innerRingKills, bool includeBoss)
    {
        CavehuntScoreSystem resolved = Resolve();
        int score = Mathf.Max(0, outerRingKills) * Mathf.Max(0, resolved.outerRingBasePoints);
        score += Mathf.Max(0, innerRingKills) * Mathf.Max(0, resolved.innerRingBasePoints);
        if (includeBoss)
        {
            score += Mathf.Max(0, resolved.bossBasePoints);
        }

        return score;
    }

    public static int AddEnemyScore(CavehuntEnemyRole role, Vector3 enemyPosition)
    {
        return Resolve().AddScore(role, enemyPosition);
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(this);
            return;
        }

        instance = this;
        score = Score;
    }

    private int AddScore(CavehuntEnemyRole role, Vector3 enemyPosition)
    {
        int basePoints = GetBasePoints(role);
        if (basePoints <= 0) return 0;

        float heightAboveGround = Mathf.Max(0f, enemyPosition.y - groundY);
        float multiplier = Mathf.Clamp(1f + heightAboveGround * Mathf.Max(0f, multiplierPerMeter), 1f, Mathf.Max(1f, maxHeightMultiplier));
        int awardedPoints = Mathf.RoundToInt(basePoints * multiplier);
        currentScore += awardedPoints;
        score = currentScore;

        Debug.Log($"Score +{awardedPoints} ({role}, x{multiplier:F1}, total {currentScore})");
        return awardedPoints;
    }

    private int GetBasePoints(CavehuntEnemyRole role)
    {
        switch (role)
        {
            case CavehuntEnemyRole.OuterRing:
                return Mathf.Max(0, outerRingBasePoints);
            case CavehuntEnemyRole.InnerRing:
                return Mathf.Max(0, innerRingBasePoints);
            case CavehuntEnemyRole.Boss:
                return Mathf.Max(0, bossBasePoints);
            default:
                return 0;
        }
    }
}