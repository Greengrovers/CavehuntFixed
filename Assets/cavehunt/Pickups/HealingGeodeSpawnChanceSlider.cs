using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
public class HealingGeodeSpawnChanceSlider : MonoBehaviour
{
    [SerializeField] private EnemyPickupDropper pickupDropper;
    [SerializeField, Range(0f, 100f)] private float spawnChancePercent;
    [SerializeField, HideInInspector] private float lastSyncedSpawnChancePercent = -1f;

    public float SpawnChancePercent
    {
        get => spawnChancePercent;
        set
        {
            spawnChancePercent = Mathf.Clamp(value, 0f, 100f);
            ApplySpawnChance();
        }
    }

    private void Reset()
    {
        ResolveDropper();
        SyncFromDropper();
    }

    private void OnEnable()
    {
        ResolveDropper();
        SyncFromDropper();
    }

    private void OnValidate()
    {
        spawnChancePercent = Mathf.Clamp(spawnChancePercent, 0f, 100f);
        ResolveDropper();

        if (pickupDropper == null)
        {
            lastSyncedSpawnChancePercent = spawnChancePercent;
            return;
        }

        float dropperPercent = pickupDropper.HealingGeodeSpawnChancePercent;
        bool hasSyncedBefore = lastSyncedSpawnChancePercent >= 0f;
        bool dropperChanged = hasSyncedBefore && !Mathf.Approximately(dropperPercent, lastSyncedSpawnChancePercent);
        bool sliderChanged = hasSyncedBefore && !Mathf.Approximately(spawnChancePercent, lastSyncedSpawnChancePercent);

        if (sliderChanged && !dropperChanged)
        {
            ApplySpawnChance();
        }
        else
        {
            SyncFromDropper();
        }
    }

    private void ResolveDropper()
    {
        if (pickupDropper == null)
        {
            pickupDropper = GetComponent<EnemyPickupDropper>();
        }

        if (pickupDropper == null)
        {
            pickupDropper = FindAnyObjectByType<EnemyPickupDropper>(FindObjectsInactive.Include);
        }
    }

    private void SyncFromDropper()
    {
        if (pickupDropper != null)
        {
            spawnChancePercent = pickupDropper.HealingGeodeSpawnChancePercent;
            lastSyncedSpawnChancePercent = spawnChancePercent;
        }
    }

    private void ApplySpawnChance()
    {
        if (pickupDropper != null)
        {
            pickupDropper.HealingGeodeSpawnChancePercent = spawnChancePercent;
            lastSyncedSpawnChancePercent = pickupDropper.HealingGeodeSpawnChancePercent;
        }
    }
}
