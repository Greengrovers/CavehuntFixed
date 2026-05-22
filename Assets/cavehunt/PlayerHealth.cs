using UnityEngine;
using UnityEngine.Events;
using System.Collections;

public class PlayerHealth : MonoBehaviour
{
    [SerializeField] private float maxHealth = 5f;
    [SerializeField] private float damageFlashDuration = 0.18f;
    [SerializeField] private float deathRespawnDelay = 1.5f;
    [SerializeField] private bool resetHealthOnDeath = true;
    [SerializeField] private bool showHealthHud = true;
    [SerializeField] private UnityEvent onDamage;
    [SerializeField] private UnityEvent onDeath;

    private float currentHealth;
    private MeshRenderer damageFlashRenderer;
    private Coroutine flashRoutine;
    private Coroutine deathRoutine;
    private float hudFlashAlpha;

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
        onDamage?.Invoke();
        Flash(currentHealth <= 0f ? 0.85f : 0.45f);

        if (currentHealth <= 0f)
        {
            onDeath?.Invoke();
            Debug.Log("Player defeated.");

            if (resetHealthOnDeath && deathRoutine == null)
            {
                deathRoutine = StartCoroutine(ResetHealthAfterDelay());
            }
        }
    }

    private IEnumerator ResetHealthAfterDelay()
    {
        yield return new WaitForSeconds(Mathf.Max(0.05f, deathRespawnDelay));
        currentHealth = maxHealth;
        Debug.Log($"Player health reset: {currentHealth}/{maxHealth}");
        deathRoutine = null;
    }

    private void OnGUI()
    {
        if (!showHealthHud) return;

        GUI.color = new Color(1f, 0f, 0f, hudFlashAlpha);
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);

        GUI.color = Color.white;
        GUI.Label(new Rect(16f, 16f, 180f, 28f), $"Health {currentHealth:0}/{maxHealth:0}");
    }

    private void Flash(float alpha)
    {
        EnsureDamageFlash();
        if (damageFlashRenderer == null) return;

        if (flashRoutine != null)
        {
            StopCoroutine(flashRoutine);
        }

        flashRoutine = StartCoroutine(FlashRoutine(alpha));
    }

    private IEnumerator FlashRoutine(float alpha)
    {
        Color color = new Color(1f, 0f, 0f, Mathf.Clamp01(alpha));
        hudFlashAlpha = color.a;
        damageFlashRenderer.sharedMaterial.color = color;
        damageFlashRenderer.enabled = true;

        float duration = Mathf.Max(0.01f, damageFlashDuration);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            color.a = Mathf.Lerp(alpha, 0f, elapsed / duration);
            hudFlashAlpha = color.a;
            damageFlashRenderer.sharedMaterial.color = color;
            yield return null;
        }

        hudFlashAlpha = 0f;
        damageFlashRenderer.enabled = false;
        flashRoutine = null;
    }

    private void EnsureDamageFlash()
    {
        if (damageFlashRenderer != null) return;

        Camera camera = Camera.main;
        if (camera == null) return;

        GameObject flash = GameObject.CreatePrimitive(PrimitiveType.Quad);
        flash.name = "Player Damage Flash";
        flash.transform.SetParent(camera.transform, false);
        flash.transform.localPosition = new Vector3(0f, 0f, 0.35f);
        flash.transform.localRotation = Quaternion.identity;
        flash.transform.localScale = new Vector3(0.55f, 0.35f, 1f);

        Collider collider = flash.GetComponent<Collider>();
        if (collider != null)
        {
            Destroy(collider);
        }

        damageFlashRenderer = flash.GetComponent<MeshRenderer>();
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
        {
            shader = Shader.Find("Universal Render Pipeline/Unlit");
        }

        if (shader != null)
        {
            damageFlashRenderer.sharedMaterial = new Material(shader)
            {
                color = new Color(1f, 0f, 0f, 0f)
            };
        }

        damageFlashRenderer.enabled = false;
    }
}
