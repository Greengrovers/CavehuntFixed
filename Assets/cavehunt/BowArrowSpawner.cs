using UnityEngine;

public class BowArrowSpawner : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform arrowSpawnPoint;
    [SerializeField] private GameObject arrowPrefab;
    [SerializeField] private PlayerAmmoInventory ammoInventory;

    [Header("Shoot Settings")]
    [SerializeField] private float shootForce = 20f;
    [SerializeField] private float airForceMultiplier = 2f;
    [SerializeField] private float airShotOffset = 0.16f;
    [SerializeField] private bool clampNockedArrowMovement = true;
    [SerializeField] private float maxNockedArrowPullDistance = 0.41f;

    private GameObject currentArrowInstance;
    private Arrow currentArrow;
    private Collider[] bowColliders;
    private Collider[] playerColliders;
    private Vector3 arrowStartLocalPosition;
    private float stringPullPointStartLocalX;
    private bool hasPullStartX;

    public Transform ArrowSpawnPoint => arrowSpawnPoint;

    private void Awake()
    {
        RefreshBowColliders();
        CachePlayerColliders();
    }

    private void Start()
    {
        ResolveAmmoInventory();
        RefreshBowColliders();
        CachePlayerColliders();
        SpawnArrow();
    }

    public void SpawnArrow()
    {
        if (currentArrowInstance != null) return;
        if (arrowPrefab == null || arrowSpawnPoint == null) return;

        ResolveAmmoInventory();
        CachePlayerColliders();

        currentArrowInstance = Instantiate(
            arrowPrefab,
            arrowSpawnPoint.position,
            arrowSpawnPoint.rotation,
            transform
        );

        currentArrow = currentArrowInstance.GetComponent<Arrow>();
        arrowStartLocalPosition = currentArrowInstance.transform.localPosition;
        stringPullPointStartLocalX = 0f;
        hasPullStartX = false;

        if (currentArrow == null)
        {
            Debug.LogWarning("Arrow Prefab hat kein Arrow-Script.");
            return;
        }

        currentArrow.PrepareForNockedArrow();
        currentArrow.SetAmmoType(ResolveCurrentAmmoType());

        Collider[] arrowColliders = currentArrowInstance.GetComponentsInChildren<Collider>();
        currentArrow.IgnoreCollisionsWith(bowColliders);
        currentArrow.IgnoreCollisionsWith(playerColliders);

        foreach (Collider bowCol in bowColliders)
        {
            foreach (Collider arrowCol in arrowColliders)
            {
                Physics.IgnoreCollision(bowCol, arrowCol, true);
            }
        }

        IgnorePlayerCollisions(arrowColliders);
    }

    public void MoveCurrentArrowToString(Transform stringPullPoint)
    {
        if (currentArrowInstance == null || stringPullPoint == null || arrowSpawnPoint == null) return;

        Vector3 pullPointLocalPosition = transform.InverseTransformPoint(stringPullPoint.position);

        if (!hasPullStartX)
        {
            stringPullPointStartLocalX = pullPointLocalPosition.x;
            hasPullStartX = true;
        }

        float pullDeltaX = pullPointLocalPosition.x - stringPullPointStartLocalX;
        if (clampNockedArrowMovement)
        {
            pullDeltaX = Mathf.Clamp(pullDeltaX, 0f, Mathf.Max(0f, maxNockedArrowPullDistance));
        }

        Vector3 arrowLocalPosition = arrowStartLocalPosition;
        arrowLocalPosition.x += pullDeltaX;

        currentArrowInstance.transform.localPosition = arrowLocalPosition;
        currentArrowInstance.transform.rotation = arrowSpawnPoint.rotation;
    }

    public void ShootCurrentArrow()
    {
        ShootCurrentArrow(shootForce);
    }

    public void ShootCurrentArrow(float force)
    {
        if (currentArrow == null) return;

        AmmoType shotAmmo = ResolveShotAmmoType();
        float adjustedForce = force;

        currentArrow.SetAmmoType(shotAmmo);

        ProceduralGameAudio.PlayArrowShot(arrowSpawnPoint != null ? arrowSpawnPoint.position : transform.position);

        if (shotAmmo == AmmoType.Air)
        {
            adjustedForce *= Mathf.Max(1f, airForceMultiplier);
            currentArrow.transform.position += arrowSpawnPoint.right * airShotOffset;
        }

        currentArrow.Shoot(arrowSpawnPoint.forward, Mathf.Max(0.01f, adjustedForce));

        currentArrow = null;
        currentArrowInstance = null;
        hasPullStartX = false;
        stringPullPointStartLocalX = 0f;

        Invoke(nameof(SpawnArrow), 0.3f);
    }

    private AmmoType ResolveCurrentAmmoType()
    {
        ResolveAmmoInventory();
        return ammoInventory != null ? ammoInventory.CurrentAmmo : AmmoType.Normal;
    }

    private AmmoType ResolveShotAmmoType()
    {
        ResolveAmmoInventory();
        return ammoInventory != null ? ammoInventory.ConsumeCurrentShot() : AmmoType.Normal;
    }

    private void ResolveAmmoInventory()
    {
        if (ammoInventory != null) return;

        ammoInventory = FindAnyObjectByType<PlayerAmmoInventory>();
    }

    private void CachePlayerColliders()
    {
        Transform playerRoot = ResolvePlayerRoot();
        playerColliders = playerRoot != null
            ? playerRoot.GetComponentsInChildren<Collider>(true)
            : new Collider[0];
    }

    private void RefreshBowColliders()
    {
        Collider[] allColliders = GetComponentsInChildren<Collider>(true);
        int validCount = 0;
        for (int i = 0; i < allColliders.Length; i++)
        {
            if (IsBowCollider(allColliders[i]))
            {
                validCount++;
            }
        }

        bowColliders = new Collider[validCount];
        int writeIndex = 0;
        for (int i = 0; i < allColliders.Length; i++)
        {
            Collider collider = allColliders[i];
            if (!IsBowCollider(collider)) continue;

            bowColliders[writeIndex] = collider;
            writeIndex++;
        }
    }

    private bool IsBowCollider(Collider collider)
    {
        if (collider == null) return false;
        if (currentArrowInstance != null && collider.transform.IsChildOf(currentArrowInstance.transform)) return false;
        if (collider.GetComponentInParent<Arrow>() != null) return false;
        if (collider.GetComponentInParent<PlayerHealth>() != null) return false;

        return true;
    }

    private Transform ResolvePlayerRoot()
    {
        if (ammoInventory != null)
        {
            return ammoInventory.transform;
        }

        PlayerHealth playerHealth = FindAnyObjectByType<PlayerHealth>();
        if (playerHealth != null)
        {
            return playerHealth.transform;
        }

        GameObject xrOrigin = GameObject.Find("XR Origin (XR Rig)") ?? GameObject.Find("XR Origin (VR)");
        if (xrOrigin != null)
        {
            return xrOrigin.transform;
        }

        return Camera.main != null ? Camera.main.transform.root : null;
    }

    private void IgnorePlayerCollisions(Collider[] arrowColliders)
    {
        if (arrowColliders == null || arrowColliders.Length == 0) return;

        CachePlayerColliders();

        for (int i = 0; i < arrowColliders.Length; i++)
        {
            Collider arrowCollider = arrowColliders[i];
            if (arrowCollider == null) continue;

            for (int j = 0; j < playerColliders.Length; j++)
            {
                Collider playerCollider = playerColliders[j];
                if (playerCollider != null)
                {
                    Physics.IgnoreCollision(arrowCollider, playerCollider, true);
                }
            }
        }
    }
}
