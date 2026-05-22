using System.Collections.Generic;
using UnityEngine;

public class EnemyPickupDropper : MonoBehaviour
{
    [SerializeField, Range(0f, 1f)] private float dropChance = 0.25f;
    [SerializeField] private GameObject[] pickupPrefabs;
    [SerializeField] private float spawnLift = 0.25f;
    [SerializeField] private float horizontalJitter = 0.2f;

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
        Vector3 spawnPosition = enemyPosition + new Vector3(jitter.x, spawnLift, jitter.y);
        GameObject pickup = Instantiate(prefab, spawnPosition, Quaternion.identity);

        PickupDropAnimation dropAnimation = pickup.GetComponent<PickupDropAnimation>();
        if (dropAnimation != null)
        {
            dropAnimation.PlayDrop();
        }

        return pickup;
    }

    private void EnsurePickupPrefabs()
    {
        if (pickupPrefabs != null && pickupPrefabs.Length > 0) return;

        pickupPrefabs = Resources.LoadAll<GameObject>("Pickups");
    }
}
