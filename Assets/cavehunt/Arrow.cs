using System.Collections.Generic;
using UnityEngine;

public class Arrow : MonoBehaviour
{
    [SerializeField] private float lifeTime = 10f;
    [SerializeField] private float damage = 1f;
    [SerializeField] private float grenadeExplosionRadius = 2.25f;
    [SerializeField] private float grenadeExplosionDamage = 2f;
    [SerializeField] private float fireTickDamage = 1f;
    [SerializeField] private int fireTickCount = 2;
    [SerializeField] private int extraFireTicksIfAlreadyBurning = 1;
    [SerializeField] private float fireTickInterval = 1f;
    [SerializeField] private float hitScanRadius = 0.24f;

    private Rigidbody rb;
    private Collider[] ownColliders;
    private bool hasBeenShot = false;
    private bool hasResolvedHit;
    private bool hasPreviousPosition;
    private Vector3 previousPosition;
    private AmmoType ammoType = AmmoType.Normal;
    private ArrowAmmoVfx ammoVfx;

    public float GrenadeExplosionRadius => Mathf.Max(0.01f, grenadeExplosionRadius);

    private void Awake()
    {
        CacheComponents();

        if (rb == null) return;

        rb.isKinematic = true;
        rb.useGravity = false;
        rb.detectCollisions = false;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
    }

    public void PrepareForNockedArrow()
    {
        CacheComponents();

        hasBeenShot = false;
        hasResolvedHit = false;
        hasPreviousPosition = false;
        if (ammoVfx != null)
        {
            ammoVfx.PrepareForNockedArrow();
        }

        if (ownColliders != null)
        {
            for (int i = 0; i < ownColliders.Length; i++)
            {
                if (ownColliders[i] != null)
                {
                    ownColliders[i].enabled = true;
                }
            }
        }

        if (rb == null) return;

        rb.isKinematic = true;
        rb.useGravity = false;
        rb.detectCollisions = false;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
    }

    public void SetAmmoType(AmmoType newAmmoType)
    {
        ammoType = newAmmoType;
        if (ammoVfx != null)
        {
            ammoVfx.SetAmmoType(ammoType);
        }
    }

    public void Shoot(Vector3 direction, float force)
    {
        if (hasBeenShot || rb == null) return;

        CacheComponents();
        hasBeenShot = true;
        hasResolvedHit = false;
        if (ammoVfx != null)
        {
            ammoVfx.PlayShot();
        }

        transform.parent = null;

        rb.detectCollisions = true;
        rb.isKinematic = false;
        rb.useGravity = true;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        previousPosition = rb.position;
        hasPreviousPosition = true;

        rb.AddForce(direction.normalized * force, ForceMode.Impulse);

        Destroy(gameObject, lifeTime);
    }

    private void FixedUpdate()
    {
        if (!hasBeenShot || hasResolvedHit) return;

        Vector3 currentPosition = rb != null ? rb.position : transform.position;
        if (!hasPreviousPosition)
        {
            previousPosition = currentPosition;
            hasPreviousPosition = true;
            return;
        }

        ScanFlightPath(previousPosition, currentPosition);
        previousPosition = currentPosition;
    }

    private void CacheComponents()
    {
        if (rb == null)
        {
            rb = GetComponent<Rigidbody>();
        }

        if (ownColliders == null || ownColliders.Length == 0)
        {
            ownColliders = GetComponentsInChildren<Collider>(true);
        }

        if (ammoVfx == null)
        {
            ammoVfx = GetComponent<ArrowAmmoVfx>();
            if (ammoVfx == null)
            {
                ammoVfx = gameObject.AddComponent<ArrowAmmoVfx>();
            }
        }
    }

    public void IgnoreCollisionsWith(Collider[] colliders)
    {
        if (colliders == null || colliders.Length == 0) return;

        if (ownColliders == null || ownColliders.Length == 0)
        {
            ownColliders = GetComponentsInChildren<Collider>(true);
        }

        for (int i = 0; i < ownColliders.Length; i++)
        {
            Collider ownCollider = ownColliders[i];
            if (ownCollider == null) continue;

            for (int j = 0; j < colliders.Length; j++)
            {
                Collider otherCollider = colliders[j];
                if (otherCollider != null && otherCollider != ownCollider)
                {
                    Physics.IgnoreCollision(ownCollider, otherCollider, true);
                }
            }
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!hasBeenShot || hasResolvedHit) return;

        if (collision.collider.GetComponentInParent<PlayerHealth>() != null)
        {
            IgnoreCollisionsWith(new[] { collision.collider });
            return;
        }

        Vector3 hitPoint = collision.contactCount > 0 ? collision.GetContact(0).point : transform.position;
        ResolveHit(collision.collider, hitPoint);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!hasBeenShot || hasResolvedHit) return;
        if (!CanDamageCollider(other)) return;

        ResolveHit(other, transform.position);
    }

    private void ScanFlightPath(Vector3 startPosition, Vector3 endPosition)
    {
        Vector3 segment = endPosition - startPosition;
        float distance = segment.magnitude;
        if (distance <= 0.0001f) return;

        RaycastHit[] hits = Physics.SphereCastAll(
            startPosition,
            Mathf.Max(0.01f, hitScanRadius),
            segment / distance,
            distance,
            ~0,
            QueryTriggerInteraction.Ignore
        );

        float nearestDistance = float.PositiveInfinity;
        RaycastHit nearestHit = default;
        bool foundHit = false;

        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit hit = hits[i];
            if (!CanDamageCollider(hit.collider)) continue;

            if (hit.distance < nearestDistance)
            {
                nearestDistance = hit.distance;
                nearestHit = hit;
                foundHit = true;
            }
        }

        if (foundHit)
        {
            ResolveHit(nearestHit.collider, nearestHit.point);
        }
    }

    private bool CanDamageCollider(Collider collider)
    {
        if (collider == null || collider.GetComponentInParent<PlayerHealth>() != null) return false;

        for (int i = 0; ownColliders != null && i < ownColliders.Length; i++)
        {
            if (collider == ownColliders[i])
            {
                return false;
            }
        }

        return collider.GetComponentInParent<Damageable>() != null;
    }

    private void ResolveHit(Collider hitCollider, Vector3 hitPoint)
    {
        if (hitCollider == null || hasResolvedHit) return;

        Damageable damageable = hitCollider.GetComponentInParent<Damageable>();
        if (ammoType == AmmoType.Grenade)
        {
            hasResolvedHit = true;
            transform.position = hitPoint;
            if (ammoVfx != null)
            {
                ammoVfx.PlayImpact(ammoType, hitPoint, grenadeExplosionRadius);
            }
            Explode(damageable);
            Destroy(gameObject);
            return;
        }

        if (damageable == null)
        {
            if (ammoType != AmmoType.Normal)
            {
                hasResolvedHit = true;
                transform.position = hitPoint;
                if (ammoVfx != null)
                {
                    ammoVfx.PlayImpact(ammoType, hitPoint, grenadeExplosionRadius);
                }

                Destroy(gameObject);
            }

            return;
        }

        hasResolvedHit = true;
        transform.position = hitPoint;
        damageable.TakeDamage(damage);
        if (ammoVfx != null)
        {
            ammoVfx.PlayImpact(ammoType, hitPoint, grenadeExplosionRadius);
        }

        if (ammoType == AmmoType.Fire)
        {
            BurningTarget burningTarget = damageable.GetComponent<BurningTarget>();
            if (burningTarget == null)
            {
                burningTarget = damageable.gameObject.AddComponent<BurningTarget>();
            }

            burningTarget.ApplyBurn(
                fireTickDamage,
                fireTickCount,
                fireTickInterval,
                extraFireTicksIfAlreadyBurning
            );
        }

        Destroy(gameObject);
    }

    private void Explode(Damageable directHit)
    {
        HashSet<Damageable> damagedTargets = new HashSet<Damageable>();
        ApplyExplosionDamage(directHit, damagedTargets);

        Collider[] colliders = Physics.OverlapSphere(
            transform.position,
            Mathf.Max(0.01f, grenadeExplosionRadius),
            ~0,
            QueryTriggerInteraction.Ignore
        );

        for (int i = 0; i < colliders.Length; i++)
        {
            Damageable damageable = colliders[i].GetComponentInParent<Damageable>();
            ApplyExplosionDamage(damageable, damagedTargets);
        }
    }

    private void ApplyExplosionDamage(Damageable damageable, HashSet<Damageable> damagedTargets)
    {
        if (damageable == null || damagedTargets.Contains(damageable)) return;

        damagedTargets.Add(damageable);
        damageable.TakeDamage(grenadeExplosionDamage);
    }
}
