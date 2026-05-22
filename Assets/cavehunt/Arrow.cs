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

    private Rigidbody rb;
    private bool hasBeenShot = false;
    private AmmoType ammoType = AmmoType.Normal;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();

        rb.isKinematic = true;
        rb.useGravity = false;
    }

    public void SetAmmoType(AmmoType newAmmoType)
    {
        ammoType = newAmmoType;
    }

    public void Shoot(Vector3 direction, float force)
    {
        if (hasBeenShot) return;

        hasBeenShot = true;

        transform.parent = null;

        rb.isKinematic = false;
        rb.useGravity = true;

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        rb.AddForce(direction.normalized * force, ForceMode.Impulse);

        Destroy(gameObject, lifeTime);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!hasBeenShot) return;

        Damageable damageable = collision.collider.GetComponentInParent<Damageable>();
        if (ammoType == AmmoType.Grenade)
        {
            Explode(damageable);
            Destroy(gameObject);
            return;
        }

        if (damageable == null) return;

        damageable.TakeDamage(damage);

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
