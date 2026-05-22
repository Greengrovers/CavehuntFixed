using UnityEngine;
using UnityEngine.Events;
using System;

public class Damageable : MonoBehaviour
{
    [SerializeField] private float maxHealth = 3f;
    [SerializeField] private bool deactivateOnDeath = true;
    [SerializeField] private UnityEvent onDeath;

    private float currentHealth;

    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;
    public bool DeactivateOnDeath
    {
        get => deactivateOnDeath;
        set => deactivateOnDeath = value;
    }

    public event Action Died;
    public event Action<float, float> HealthChanged;

    private void Awake()
    {
        EnsureValidMaxHealth();
        currentHealth = maxHealth;
        HealthChanged?.Invoke(currentHealth, maxHealth);
    }

    public void EnsureValidMaxHealth(float fallback = 3f)
    {
        if (maxHealth > 0f) return;

        maxHealth = Mathf.Max(1f, fallback);
    }

    public void SetMaxHealth(float value, bool resetCurrentHealth = true)
    {
        maxHealth = Mathf.Max(1f, value);

        if (resetCurrentHealth)
        {
            currentHealth = maxHealth;
        }
        else
        {
            currentHealth = Mathf.Min(currentHealth, maxHealth);
        }

        HealthChanged?.Invoke(currentHealth, maxHealth);
    }

    public void TakeDamage(float amount)
    {
        if (amount <= 0f || currentHealth <= 0f) return;

        currentHealth = Mathf.Max(0f, currentHealth - amount);
        HealthChanged?.Invoke(currentHealth, maxHealth);

        if (currentHealth <= 0f)
        {
            Died?.Invoke();
            onDeath?.Invoke();

            if (deactivateOnDeath)
            {
                gameObject.SetActive(false);
            }
        }
    }

    public void ResetHealth()
    {
        currentHealth = maxHealth;
        HealthChanged?.Invoke(currentHealth, maxHealth);
    }
}
