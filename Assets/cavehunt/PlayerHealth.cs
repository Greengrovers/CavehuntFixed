using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using System.Collections;

public class PlayerHealth : MonoBehaviour
{
    [SerializeField] private float maxHealth = 5f;
    [SerializeField] private float damageFlashDuration = 0.18f;
    [SerializeField] private float deathRespawnDelay = 1.5f;
    [SerializeField] private bool resetHealthOnDeath = true;
    [SerializeField] private bool showHealthHud = true;

    [Header("Score HUD")]
    [SerializeField] private Vector3 scoreHudLocalPosition = new Vector3(0f, 0f, 1.1f);
    [SerializeField] private Vector2 scoreHudWorldSize = new Vector2(1.55f, 0.34f);
    [SerializeField] private Color scoreHudBackgroundColor = new Color(0f, 0f, 0f, 0.58f);
    [SerializeField] private Vector2 scoreHudCanvasSize = new Vector2(1800f, 520f);
    [SerializeField] private float scoreHudCanvasScale = 0.0022f;
    [SerializeField] private int scoreHudFontSize = 660;
    [SerializeField] private float scoreHudCharacterSize = 0.055f;

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
    private GameObject scoreHudRoot;
    private TextMesh scoreHudLabel;
    private TextMesh scoreHudShadow;
    private MeshRenderer scoreHudRenderer;
    private MeshRenderer scoreHudShadowRenderer;
    private MeshRenderer scoreHudBackgroundRenderer;
    private string lastScoreHudText;
    private Canvas scoreHudCanvas;
    private RectTransform scoreHudCanvasRect;
    private Text scoreHudCanvasLabel;
    private Text scoreHudCanvasShadow;
    private Image scoreHudCanvasBackground;

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
                CavehuntRuntimeCleanup.DestroyGameplayLeftovers();
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
        CavehuntRuntimeCleanup.DestroyGameplayLeftovers();
        ClearDamageFlash();
        BowStartExperience.ResetAllBowsForRetry();

        if (gameOverMenu != null)
        {
            gameOverMenu.Hide();
        }

        Debug.Log($"Player health reset: {currentHealth}/{maxHealth}");
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

        Debug.Log($"Player max health set: {currentHealth}/{maxHealth}");
    }

    public void RetryFromGameOver()
    {
        CavehuntScoreSystem.ResetScore();
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
        CavehuntRuntimeCleanup.DestroyGameplayLeftovers();
        ClearDamageFlash();
        BowStartExperience.ResetAllBowsForRetry();
        Debug.Log($"Player health reset: {currentHealth}/{maxHealth}");
        deathRoutine = null;
    }

    private void LateUpdate()
    {
        HideLegacyScoreHud();
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

        bool menuVisible = IsGameEndMenuVisible();

        if (showHealthHud && !IsDead && !menuVisible)
        {
            GUI.depth = previousDepth;
            GUI.color = Color.white;
            GUI.Label(new Rect(16f, 16f, 180f, 28f), $"Health {currentHealth:0}/{maxHealth:0}");
        }

        if (!menuVisible)
        {
            GUI.depth = previousDepth;
            GUI.color = Color.white;
            GUIStyle scoreStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.UpperRight,
                fontSize = 24,
                fontStyle = FontStyle.Bold
            };
            GUI.Label(new Rect(Screen.width - 276f, 16f, 260f, 34f), $"Score {CavehuntScoreSystem.Score}", scoreStyle);
        }

        GUI.color = previousColor;
        GUI.depth = previousDepth;
    }

    private void HideLegacyScoreHud()
    {
        if (scoreHudCanvas != null && scoreHudCanvas.gameObject.activeSelf)
        {
            scoreHudCanvas.gameObject.SetActive(false);
        }

        SetScoreHudVisible(false);
    }
    private void UpdateScoreHud()
    {
        EnsureScoreHudCanvas();
        SetScoreHudVisible(false);

        bool shouldShow = !IsDead && !IsGameEndMenuVisible();
        if (scoreHudCanvas != null)
        {
            scoreHudCanvas.gameObject.SetActive(shouldShow);
        }

        if (!shouldShow || scoreHudCanvasLabel == null) return;

        string scoreText = $"Score: {CavehuntScoreSystem.Score}";
        if (scoreText != lastScoreHudText)
        {
            scoreHudCanvasLabel.text = scoreText;
            if (scoreHudCanvasShadow != null)
            {
                scoreHudCanvasShadow.text = scoreText;
            }
            lastScoreHudText = scoreText;
        }

        UpdateScoreHudCanvasTransform();
    }

    private void SetScoreHudVisible(bool visibleHud)
    {
        if (scoreHudRoot != null && scoreHudRoot.activeSelf != visibleHud)
        {
            scoreHudRoot.SetActive(visibleHud);
        }

        if (scoreHudRenderer != null) scoreHudRenderer.enabled = visibleHud;
        if (scoreHudShadowRenderer != null) scoreHudShadowRenderer.enabled = visibleHud;
        if (scoreHudBackgroundRenderer != null) scoreHudBackgroundRenderer.enabled = visibleHud;
    }

    private void EnsureScoreHudCanvas()
    {
        if (scoreHudCanvas != null)
        {
            UpdateScoreHudCanvasTransform();
            return;
        }

        Camera camera = VrCameraResolver.GetCamera();
        if (camera == null) return;

        GameObject canvasObject = new GameObject("Cavehunt VR Score Canvas");
        canvasObject.transform.SetParent(camera.transform, false);

        scoreHudCanvas = canvasObject.AddComponent<Canvas>();
        scoreHudCanvas.renderMode = RenderMode.WorldSpace;
        scoreHudCanvas.worldCamera = camera;
        scoreHudCanvas.sortingOrder = 7200;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 1400f;

        scoreHudCanvasRect = canvasObject.GetComponent<RectTransform>();
        scoreHudCanvasRect.sizeDelta = scoreHudCanvasSize;
        scoreHudCanvasRect.localScale = Vector3.one * Mathf.Max(0.0001f, scoreHudCanvasScale);

        scoreHudCanvasBackground = CreateScoreCanvasBackground("Score HUD Background");
        scoreHudCanvasShadow = CreateScoreCanvasText("Score HUD Shadow", new Color(0f, 0f, 0f, 0.92f), new Vector2(20f, -20f));
        scoreHudCanvasLabel = CreateScoreCanvasText("Score HUD Label", Color.white, Vector2.zero);
        lastScoreHudText = null;
        UpdateScoreHudCanvasTransform();
    }

    private Image CreateScoreCanvasBackground(string objectName)
    {
        GameObject imageObject = new GameObject(objectName);
        imageObject.transform.SetParent(scoreHudCanvasRect, false);
        imageObject.transform.SetAsFirstSibling();

        RectTransform rect = imageObject.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.anchoredPosition = Vector2.zero;

        Image background = imageObject.AddComponent<Image>();
        background.color = scoreHudBackgroundColor;
        background.raycastTarget = false;
        return background;
    }
    private Text CreateScoreCanvasText(string objectName, Color color, Vector2 anchoredPosition)
    {
        GameObject textObject = new GameObject(objectName);
        textObject.transform.SetParent(scoreHudCanvasRect, false);

        RectTransform rect = textObject.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.anchoredPosition = anchoredPosition;

        Text label = textObject.AddComponent<Text>();
        label.font = ResolveHudFont();
        label.fontSize = Mathf.Max(12, scoreHudFontSize);
        label.fontStyle = FontStyle.Bold;
        label.alignment = TextAnchor.MiddleCenter;
        label.horizontalOverflow = HorizontalWrapMode.Overflow;
        label.verticalOverflow = VerticalWrapMode.Overflow;
        label.color = color;
        label.raycastTarget = false;
        return label;
    }

    private void UpdateScoreHudCanvasTransform()
    {
        Camera camera = VrCameraResolver.GetCamera();
        if (camera == null || scoreHudCanvasRect == null) return;

        scoreHudCanvas.worldCamera = camera;
        scoreHudCanvasRect.SetParent(camera.transform, false);
        scoreHudCanvasRect.sizeDelta = scoreHudCanvasSize;
        scoreHudCanvasRect.localPosition = scoreHudLocalPosition;
        scoreHudCanvasRect.localRotation = Quaternion.identity;
        scoreHudCanvasRect.localScale = Vector3.one * Mathf.Max(0.0001f, scoreHudCanvasScale);
        if (scoreHudCanvasLabel != null) scoreHudCanvasLabel.fontSize = Mathf.Max(12, scoreHudFontSize);
        if (scoreHudCanvasShadow != null) scoreHudCanvasShadow.fontSize = Mathf.Max(12, scoreHudFontSize);
    }

    private void EnsureScoreHud()
    {
        if (scoreHudLabel != null)
        {
            UpdateScoreHudTransform();
            return;
        }

        Camera camera = VrCameraResolver.GetCamera();
        if (camera == null) return;

        scoreHudRoot = new GameObject("Cavehunt VR Score HUD Root");
        scoreHudRoot.hideFlags = HideFlags.DontSave;

        scoreHudBackgroundRenderer = CreateScoreHudBackground();
        scoreHudShadow = CreateScoreHudLabel("Cavehunt VR Score HUD Shadow", new Color(0f, 0f, 0f, 0.9f), 6999);
        scoreHudLabel = CreateScoreHudLabel("Cavehunt VR Score HUD", Color.white, 7000);

        scoreHudShadowRenderer = scoreHudShadow != null ? scoreHudShadow.GetComponent<MeshRenderer>() : null;
        scoreHudRenderer = scoreHudLabel != null ? scoreHudLabel.GetComponent<MeshRenderer>() : null;
        lastScoreHudText = null;
        UpdateScoreHudTransform();
    }

    private MeshRenderer CreateScoreHudBackground()
    {
        if (scoreHudRoot == null) return null;

        GameObject backgroundObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
        backgroundObject.name = "Cavehunt VR Score HUD Background";
        backgroundObject.hideFlags = HideFlags.DontSave;
        backgroundObject.transform.SetParent(scoreHudRoot.transform, false);

        Collider collider = backgroundObject.GetComponent<Collider>();
        if (collider != null)
        {
            Destroy(collider);
        }

        MeshRenderer renderer = backgroundObject.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            renderer.sortingOrder = 6998;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            }

            if (shader != null)
            {
                Material material = new Material(shader);
                material.name = "Cavehunt VR Score HUD Background Material";
                material.hideFlags = HideFlags.DontSave;
                material.color = scoreHudBackgroundColor;
                material.renderQueue = 6998;
                renderer.sharedMaterial = material;
            }
        }

        return renderer;
    }

    private TextMesh CreateScoreHudLabel(string objectName, Color color, int sortingOrder)
    {
        if (scoreHudRoot == null) return null;

        GameObject labelObject = new GameObject(objectName);
        labelObject.hideFlags = HideFlags.DontSave;
        labelObject.transform.SetParent(scoreHudRoot.transform, false);

        TextMesh label = labelObject.AddComponent<TextMesh>();
        label.font = ResolveHudFont();
        label.anchor = TextAnchor.MiddleCenter;
        label.alignment = TextAlignment.Center;
        label.fontSize = Mathf.Max(12, scoreHudFontSize);
        label.fontStyle = FontStyle.Bold;
        label.characterSize = Mathf.Max(0.001f, scoreHudCharacterSize);
        label.color = color;
        label.richText = false;

        MeshRenderer renderer = labelObject.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            renderer.sortingOrder = sortingOrder;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            if (label.font != null && label.font.material != null)
            {
                Material material = new Material(label.font.material);
                material.name = objectName + " Material";
                material.hideFlags = HideFlags.DontSave;
                material.renderQueue = sortingOrder;
                renderer.sharedMaterial = material;
            }
        }

        return label;
    }

    private void UpdateScoreHudTransform()
    {
        Camera camera = VrCameraResolver.GetCamera();
        if (camera == null || scoreHudRoot == null || scoreHudLabel == null) return;

        Transform cameraTransform = camera.transform;
        float distance = Mathf.Max(camera.nearClipPlane + 0.55f, scoreHudLocalPosition.z);
        Vector3 center =
            cameraTransform.position +
            cameraTransform.forward * distance +
            cameraTransform.right * scoreHudLocalPosition.x +
            cameraTransform.up * scoreHudLocalPosition.y;

        Quaternion rotation = Quaternion.LookRotation(center - cameraTransform.position, Vector3.up);
        scoreHudRoot.transform.SetPositionAndRotation(center, rotation);

        if (scoreHudBackgroundRenderer != null)
        {
            Transform backgroundTransform = scoreHudBackgroundRenderer.transform;
            backgroundTransform.localPosition = new Vector3(0f, 0f, 0.018f);
            backgroundTransform.localRotation = Quaternion.identity;
            backgroundTransform.localScale = new Vector3(
                Mathf.Max(0.15f, scoreHudWorldSize.x),
                Mathf.Max(0.05f, scoreHudWorldSize.y),
                1f
            );
        }

        scoreHudLabel.transform.localPosition = Vector3.zero;
        scoreHudLabel.transform.localRotation = Quaternion.identity;
        scoreHudLabel.transform.localScale = Vector3.one;
        scoreHudLabel.fontSize = Mathf.Max(12, scoreHudFontSize);
        scoreHudLabel.characterSize = Mathf.Max(0.001f, scoreHudCharacterSize);

        if (scoreHudShadow != null)
        {
            scoreHudShadow.transform.localPosition = new Vector3(0.012f, -0.012f, 0.001f);
            scoreHudShadow.transform.localRotation = Quaternion.identity;
            scoreHudShadow.transform.localScale = Vector3.one;
            scoreHudShadow.fontSize = Mathf.Max(12, scoreHudFontSize);
            scoreHudShadow.characterSize = Mathf.Max(0.001f, scoreHudCharacterSize);
        }
    }

    private static Font ResolveHudFont()
    {
        try
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font != null) return font;
        }
        catch (System.ArgumentException)
        {
        }

        try
        {
            return Resources.GetBuiltinResource<Font>("Arial.ttf");
        }
        catch (System.ArgumentException)
        {
            return null;
        }
    }

    private bool IsGameEndMenuVisible()
    {
        return gameOverMenu != null && gameOverMenu.IsVisible;
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
