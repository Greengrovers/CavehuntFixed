using UnityEngine;

[RequireComponent(typeof(Collider))]
public class BatProjectile : MonoBehaviour
{
    [SerializeField] private float damage = 1f;
    [SerializeField] private float lifetime = 6f;
    [SerializeField] private float targetHitRadius = 0.45f;

    private Transform target;
    private Transform targetRoot;
    private PlayerHealth targetHealth;
    private bool hasHit;
    private Vector3 previousPosition;

    private void Start()
    {
        previousPosition = transform.position;
        Destroy(gameObject, lifetime);
    }

    private void Update()
    {
        if (hasHit || target == null) return;

        float sqrHitRadius = targetHitRadius * targetHitRadius;
        float sqrDistanceToPath = SqrDistancePointToSegment(target.position, previousPosition, transform.position);
        previousPosition = transform.position;

        if (sqrDistanceToPath > sqrHitRadius) return;

        Hit(targetHealth != null ? targetHealth : target.GetComponentInParent<PlayerHealth>());
    }

    public void Initialize(float damageAmount, float projectileLifetime, Transform targetTransform = null, float hitRadius = 0.45f, PlayerHealth playerHealth = null)
    {
        damage = damageAmount;
        lifetime = projectileLifetime;
        target = targetTransform;
        targetHealth = playerHealth != null ? playerHealth : targetTransform != null ? targetTransform.GetComponentInParent<PlayerHealth>() : null;
        targetRoot = targetHealth != null ? targetHealth.transform : targetTransform;
        targetHitRadius = Mathf.Max(0.05f, hitRadius);
        previousPosition = transform.position;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (targetRoot != null && !other.transform.IsChildOf(targetRoot)) return;

        PlayerHealth playerHealth = other.GetComponentInParent<PlayerHealth>();
        Hit(playerHealth);
    }

    private void Hit(PlayerHealth playerHealth)
    {
        if (hasHit || playerHealth == null) return;

        hasHit = true;
        playerHealth.TakeDamage(damage);
        Destroy(gameObject);
    }

    private static float SqrDistancePointToSegment(Vector3 point, Vector3 segmentStart, Vector3 segmentEnd)
    {
        Vector3 segment = segmentEnd - segmentStart;
        float segmentLength = segment.sqrMagnitude;
        if (segmentLength <= 0.0001f)
        {
            return (point - segmentEnd).sqrMagnitude;
        }

        float t = Mathf.Clamp01(Vector3.Dot(point - segmentStart, segment) / segmentLength);
        Vector3 closestPoint = segmentStart + segment * t;
        return (point - closestPoint).sqrMagnitude;
    }
}
