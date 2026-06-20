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

    [Header("Game Over")]
    [SerializeField] private bool autoCreateGameOverMenu = true;

    [SerializeField] private UnityEvent onDamage;
    [SerializeField] private UnityEvent onDeath;

    private float currentHealth;
    private MeshRenderer damageFlashRenderer;
    private Material damageFlashMaterial;
    private Coroutine flashRoutine;
    private Coroutine deathRoutine;
    private float hudFlashAlpha;
    private GameOverMenu gameOverMenu;

    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;
    public bool IsDead => currentHealth <= 0f;

    private void Awake()
    {
        currentHealth = maxHealth;

        if (autoCreateGameOverMenu)
        {
            EnsureGameOverMenu();
            if (gameOverMenu != null)
            {
                gameOverMenu.Hide();
            }
        }
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

            if (autoCreateGameOverMenu)
            {
                ClearDamageFlash();
                BowStartExperience.HideAllBowsForPlayerDeath();
                ShowGameOverMenu();
            }
            else if (resetHealthOnDeath && deathRoutine == null)
            {
                deathRoutine = StartCoroutine(ResetHealthAfterDelay());
            }
        }
    }

    public float Heal(float amount)
    {
        if (amount <= 0f || currentHealth <= 0f || currentHealth >= maxHealth) return 0f;

        float previousHealth = currentHealth;
        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
        float healed = currentHealth - previousHealth;
        Debug.Log($"Player healed: {currentHealth}/{maxHealth}");
        return healed;
    }

    public void ResetToFullHealth()
    {
        if (deathRoutine != null)
        {
            StopCoroutine(deathRoutine);
            deathRoutine = null;
        }

        currentHealth = maxHealth;
        ClearDamageFlash();
        BowStartExperience.ResetAllBowsForRetry();

        if (gameOverMenu != null)
        {
            gameOverMenu.Hide();
        }

        Debug.Log($"Player health reset: {currentHealth}/{maxHealth}");
    }

    public void RetryFromGameOver()
    {
        ResetToFullHealth();
    }

    public void ShowGameOverMenu()
    {
        EnsureGameOverMenu();
        if (gameOverMenu != null)
        {
            gameOverMenu.ShowGameOver();
        }
    }

    public void ShowGameWonMenu()
    {
        EnsureGameOverMenu();
        if (gameOverMenu != null)
        {
            gameOverMenu.ShowGameWon();
        }
    }

    private IEnumerator ResetHealthAfterDelay()
    {
        yield return new WaitForSeconds(Mathf.Max(0.05f, deathRespawnDelay));
        currentHealth = maxHealth;
        ClearDamageFlash();
        BowStartExperience.ResetAllBowsForRetry();
        Debug.Log($"Player health reset: {currentHealth}/{maxHealth}");
        deathRoutine = null;
    }

    private void OnGUI()
    {
        int previousDepth = GUI.depth;
        Color previousColor = GUI.color;

        if (hudFlashAlpha > 0f)
        {
            GUI.depth = -1000;
            GUI.color = new Color(1f, 0f, 0f, hudFlashAlpha);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
        }

        if (showHealthHud && !IsDead)
        {
            GUI.depth = previousDepth;
            GUI.color = Color.white;
            GUI.Label(new Rect(16f, 16f, 180f, 28f), $"Health {currentHealth:0}/{maxHealth:0}");
        }

        GUI.color = previousColor;
        GUI.depth = previousDepth;
    }

    private void Flash(float alpha)
    {
        EnsureDamageFlash();

        if (flashRoutine != null)
        {
            StopCoroutine(flashRoutine);
        }

        flashRoutine = StartCoroutine(FlashRoutine(alpha));
    }

    private IEnumerator FlashRoutine(float alpha)
    {
        float startAlpha = Mathf.Clamp01(alpha);
        hudFlashAlpha = startAlpha;
        SetDamageFlashAlpha(startAlpha);

        float duration = Mathf.Max(0.01f, damageFlashDuration);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            hudFlashAlpha = Mathf.Lerp(startAlpha, 0f, elapsed / duration);
            SetDamageFlashAlpha(hudFlashAlpha);
            UpdateDamageFlashTransform();
            yield return null;
        }

        hudFlashAlpha = 0f;
        SetDamageFlashAlpha(0f);
        flashRoutine = null;
    }

    private void EnsureDamageFlash()
    {
        if (damageFlashRenderer != null)
        {
            UpdateDamageFlashTransform();
            return;
        }

        Camera camera = VrCameraResolver.GetCamera();
        if (camera == null) return;

        GameObject flash = GameObject.CreatePrimitive(PrimitiveType.Quad);
        flash.name = "Player Damage Flash";
        flash.transform.SetParent(camera.transform, false);

        Collider collider = flash.GetComponent<Collider>();
        if (collider != null)
        {
            Destroy(collider);
        }

        damageFlashRenderer = flash.GetComponent<MeshRenderer>();
        Shader shader = Shader.Find("Cavehunt/DamageFlash");
        if (shader == null)
        {
            shader = Shader.Find("Sprites/Default");
        }
        if (shader == null)
        {
            shader = Shader.Find("Universal Render Pipeline/Unlit");
        }

        if (shader != null)
        {
            damageFlashMaterial = new Material(shader);
            damageFlashMaterial.name = "Runtime Player Damage Flash";
            damageFlashMaterial.hideFlags = HideFlags.DontSave;
            damageFlashMaterial.renderQueue = 5000;
            damageFlashRenderer.sharedMaterial = damageFlashMaterial;
        }

        damageFlashRenderer.enabled = false;
        UpdateDamageFlashTransform();
    }

    private void UpdateDamageFlashTransform()
    {
        if (damageFlashRenderer == null) return;

        Camera camera = VrCameraResolver.GetCamera();
        if (camera == null) return;

        float distance = Mathf.Max(camera.nearClipPlane + 0.12f, 0.45f);
        float fov = Mathf.Max(camera.fieldOfView, 95f);
        float height = 2f * distance * Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad) * 1.55f;
        float width = height * Mathf.Max(2.1f, camera.aspect * 1.35f);

        Transform flashTransform = damageFlashRenderer.transform;
        flashTransform.SetParent(camera.transform, false);
        flashTransform.localPosition = new Vector3(0f, 0f, distance);
        flashTransform.localRotation = Quaternion.identity;
        flashTransform.localScale = new Vector3(width, height, 1f);
    }

    private void SetDamageFlashAlpha(float alpha)
    {
        if (damageFlashRenderer == null) return;

        Color color = new Color(1f, 0f, 0f, Mathf.Clamp01(alpha));
        if (damageFlashMaterial != null)
        {
            damageFlashMaterial.color = color;
        }
        else if (damageFlashRenderer.sharedMaterial != null)
        {
            damageFlashRenderer.sharedMaterial.color = color;
        }

        damageFlashRenderer.enabled = color.a > 0f;
    }

    public void ClearDamageFlash()
    {
        if (flashRoutine != null)
        {
            StopCoroutine(flashRoutine);
            flashRoutine = null;
        }

        hudFlashAlpha = 0f;
        SetDamageFlashAlpha(0f);
    }

    private void EnsureGameOverMenu()
    {
        if (!autoCreateGameOverMenu) return;

        if (gameOverMenu == null)
        {
            gameOverMenu = GetComponent<GameOverMenu>();
        }

        if (gameOverMenu == null)
        {
            gameOverMenu = gameObject.AddComponent<GameOverMenu>();
        }

        gameOverMenu.Configure(this);
    }
}
public static class VrCameraResolver
{
    public static Camera GetCamera()
    {
        Camera xrCamera = FindCameraUnder("XR Origin (XR Rig)") ?? FindCameraUnder("XR Origin (VR)");
        if (xrCamera != null) return xrCamera;

        Camera mainCamera = Camera.main;
        if (mainCamera != null) return mainCamera;

        Camera[] cameras = Object.FindObjectsByType<Camera>(FindObjectsInactive.Exclude);
        for (int i = 0; i < cameras.Length; i++)
        {
            Camera camera = cameras[i];
            if (camera != null && camera.enabled) return camera;
        }

        return cameras.Length > 0 ? cameras[0] : null;
    }

    private static Camera FindCameraUnder(string rootName)
    {
        GameObject root = GameObject.Find(rootName);
        if (root == null) return null;

        Camera[] cameras = root.GetComponentsInChildren<Camera>(true);
        for (int i = 0; i < cameras.Length; i++)
        {
            Camera camera = cameras[i];
            if (camera != null && camera.enabled) return camera;
        }

        return cameras.Length > 0 ? cameras[0] : null;
    }
}
