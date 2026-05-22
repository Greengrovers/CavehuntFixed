using System.Collections.Generic;
using UnityEngine;

public class EnemyPickupDropper : MonoBehaviour
{
    [SerializeField, Range(0f, 1f)] private float dropChance = 0.25f;
    [SerializeField] private GameObject[] pickupPrefabs;
    [SerializeField] private float spawnLift = 0.08f;
    [SerializeField] private float horizontalJitter = 0.2f;
    [SerializeField] private float groundProbeStartHeight = 2f;
    [SerializeField] private float groundProbeDistance = 50f;

    private void Awake()
    {
        EnsurePickupPrefabs();
    }

    public void Configure(float chance, GameObject[] prefabs = null)
    {
        dropChance = Mathf.Clamp01(chance);

        if (prefabs != null && prefabs.Length > 0)
        {
            pickupPrefabs = prefabs;
        }

        EnsurePickupPrefabs();
    }

    public GameObject TryDrop(Vector3 enemyPosition)
    {
        EnsurePickupPrefabs();

        if (pickupPrefabs == null || pickupPrefabs.Length == 0) return null;
        if (Random.value > dropChance) return null;

        List<GameObject> validPrefabs = new List<GameObject>();
        for (int i = 0; i < pickupPrefabs.Length; i++)
        {
            if (pickupPrefabs[i] != null)
            {
                validPrefabs.Add(pickupPrefabs[i]);
            }
        }

        if (validPrefabs.Count == 0) return null;

        GameObject prefab = validPrefabs[Random.Range(0, validPrefabs.Count)];
        Vector2 jitter = Random.insideUnitCircle * horizontalJitter;
        Vector3 spawnPosition = ResolveGroundDropPosition(enemyPosition, jitter);
        GameObject pickup = Instantiate(prefab, spawnPosition, Quaternion.identity);

        PickupDropAnimation dropAnimation = pickup.GetComponent<PickupDropAnimation>();
        if (dropAnimation != null)
        {
            dropAnimation.PlayDrop();
        }

        return pickup;
    }

    private Vector3 ResolveGroundDropPosition(Vector3 enemyPosition, Vector2 jitter)
    {
        Vector3 horizontalDropPosition = enemyPosition + new Vector3(jitter.x, 0f, jitter.y);
        Vector3 probeStart = horizontalDropPosition + Vector3.up * Mathf.Max(0.1f, groundProbeStartHeight);
        float probeDistance = Mathf.Max(0.1f, groundProbeStartHeight + groundProbeDistance);

        RaycastHit[] hits = Physics.RaycastAll(
            probeStart,
            Vector3.down,
            probeDistance,
            ~0,
            QueryTriggerInteraction.Ignore
        );

        float nearestDistance = float.PositiveInfinity;
        RaycastHit nearestGroundHit = default;
        bool foundGround = false;

        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit hit = hits[i];
            if (!IsGroundDropSurface(hit.collider)) continue;

            if (hit.distance < nearestDistance)
            {
                nearestDistance = hit.distance;
                nearestGroundHit = hit;
                foundGround = true;
            }
        }

        if (foundGround)
        {
            return nearestGroundHit.point + nearestGroundHit.normal * Mathf.Max(0f, spawnLift);
        }

        return horizontalDropPosition + Vector3.up * Mathf.Max(0f, spawnLift);
    }

    private bool IsGroundDropSurface(Collider candidate)
    {
        if (candidate == null) return false;
        if (candidate.transform.IsChildOf(transform)) return false;
        if (candidate.GetComponentInParent<BatEnemy>() != null) return false;
        if (candidate.GetComponentInParent<Damageable>() != null) return false;
        if (candidate.GetComponentInParent<PlayerHealth>() != null) return false;
        if (candidate.GetComponentInParent<AmmoPickup>() != null) return false;

        return true;
    }

    private void EnsurePickupPrefabs()
    {
        if (pickupPrefabs != null && pickupPrefabs.Length > 0) return;

        pickupPrefabs = Resources.LoadAll<GameObject>("Pickups");
    }
}
