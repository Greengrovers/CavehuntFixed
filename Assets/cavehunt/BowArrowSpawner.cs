using UnityEngine;

public class BowArrowSpawner : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform arrowSpawnPoint;
    [SerializeField] private GameObject arrowPrefab;

    [Header("Shoot Settings")]
    [SerializeField] private float shootForce = 20f;

    private GameObject currentArrowInstance;
    private Arrow currentArrow;
    private Collider[] bowColliders;
    private Vector3 arrowStartLocalPosition;
    private float stringPullPointStartLocalX;
    private bool hasPullStartX;

    public Transform ArrowSpawnPoint => arrowSpawnPoint;

    private void Awake()
    {
        bowColliders = GetComponentsInChildren<Collider>();
    }

    private void Start()
    {
        SpawnArrow();
    }

    public void SpawnArrow()
    {
        if (currentArrowInstance != null) return;
        if (arrowPrefab == null || arrowSpawnPoint == null) return;

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

        Collider[] arrowColliders = currentArrowInstance.GetComponentsInChildren<Collider>();

        foreach (Collider bowCol in bowColliders)
        {
            foreach (Collider arrowCol in arrowColliders)
            {
                Physics.IgnoreCollision(bowCol, arrowCol, true);
            }
        }
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

        Vector3 arrowLocalPosition = arrowStartLocalPosition;
        arrowLocalPosition.x += pullPointLocalPosition.x - stringPullPointStartLocalX;

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

        currentArrow.Shoot(arrowSpawnPoint.forward, force);

        currentArrow = null;
        currentArrowInstance = null;
        hasPullStartX = false;
        stringPullPointStartLocalX = 0f;

        Invoke(nameof(SpawnArrow), 0.3f);
    }
}