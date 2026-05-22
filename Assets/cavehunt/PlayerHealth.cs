using UnityEngine;
using UnityEngine.Events;

public class PlayerHealth : MonoBehaviour
{
    [SerializeField] private float maxHealth = 5f;
    [SerializeField] private UnityEvent onDeath;

    private float currentHealth;

    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;

    private void Awake()
    {
        currentHealth = maxHealth;
    }

    public void TakeDamage(float amount)
    {
        if (amount <= 0f || currentHealth <= 0f) return;

        currentHealth = Mathf.Max(0f, currentHealth - amount);
        Debug.Log($"Player health: {currentHealth}/{maxHealth}");

        if (currentHealth <= 0f)
        {
            onDeath?.Invoke();
        }
    }
}
