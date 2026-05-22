using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Damageable))]
public class BurningTarget : MonoBehaviour
{
    private Damageable damageable;
    private Coroutine burnRoutine;
    private int remainingTicks;
    private float tickDamage = 1f;
    private float tickInterval = 1f;

    private void Awake()
    {
        damageable = GetComponent<Damageable>();
    }

    public void ApplyBurn(float damagePerTick, int tickCount, float interval, int extraTicksIfAlreadyBurning)
    {
        if (damageable == null)
        {
            damageable = GetComponent<Damageable>();
        }

        bool alreadyBurning = remainingTicks > 0;
        tickDamage = Mathf.Max(0f, damagePerTick);
        tickInterval = Mathf.Max(0.05f, interval);
        remainingTicks += Mathf.Max(0, tickCount);

        if (alreadyBurning)
        {
            remainingTicks += Mathf.Max(0, extraTicksIfAlreadyBurning);
        }

        if (burnRoutine == null)
        {
            burnRoutine = StartCoroutine(Burn());
        }
    }

    private IEnumerator Burn()
    {
        while (remainingTicks > 0 && damageable != null && damageable.CurrentHealth > 0f)
        {
            yield return new WaitForSeconds(tickInterval);

            if (damageable == null || damageable.CurrentHealth <= 0f)
            {
                break;
            }

            damageable.TakeDamage(tickDamage);
            remainingTicks--;
        }

        remainingTicks = 0;
        burnRoutine = null;
    }
}
