using UnityEngine;

[RequireComponent(typeof(Collider))]
public class AmmoPickup : MonoBehaviour
{
    [SerializeField] private AmmoType ammoType = AmmoType.Fire;
    [SerializeField, Min(1)] private int ammoAmount = 5;
    [SerializeField] private bool switchToAmmoOnPickup = true;

    private bool collected;

    private void Reset()
    {
        Collider ownCollider = GetComponent<Collider>();
        ownCollider.isTrigger = true;
    }

    private void Awake()
    {
        Collider ownCollider = GetComponent<Collider>();
        ownCollider.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        TryCollect(other);
    }

    private void TryCollect(Collider other)
    {
        if (collected || other == null) return;
        if (other.GetComponentInParent<BatEnemy>() != null) return;

        PlayerAmmoInventory inventory = other.GetComponentInParent<PlayerAmmoInventory>();
        if (inventory == null)
        {
            PlayerHealth playerHealth = other.GetComponentInParent<PlayerHealth>();
            if (playerHealth != null)
            {
                inventory = playerHealth.GetComponent<PlayerAmmoInventory>();
            }
        }

        if (inventory == null && IsLikelyPlayerCollider(other))
        {
            inventory = FindAnyObjectByType<PlayerAmmoInventory>();
        }

        if (inventory == null) return;

        collected = true;
        inventory.AddAmmo(ammoType, ammoAmount, switchToAmmoOnPickup);

        if (ammoType == AmmoType.Air)
        {
            ProceduralGameAudio.PlayAirPickup(transform.position);
        }
        else
        {
            ProceduralGameAudio.PlayPickup(transform.position);
        }

        Destroy(gameObject);
    }

    private static bool IsLikelyPlayerCollider(Collider other)
    {
        if (other.GetComponentInParent<CharacterController>() != null) return true;
        if (other.GetComponentInParent<Camera>() != null) return true;

        string rootName = other.transform.root != null
            ? other.transform.root.name.ToLowerInvariant()
            : string.Empty;

        return rootName.Contains("xr")
            || rootName.Contains("player")
            || rootName.Contains("rig")
            || rootName.Contains("hand");
    }
}
