using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[RequireComponent(typeof(XRGrabInteractable))]
[DisallowMultipleComponent]
public class PlayerBowHealthBar : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private Renderer bowHealthRenderer;
    [SerializeField] private string bowHealthRendererName;

    [Header("Held Bow Health")]
    [SerializeField] private Color bowHealthColor;
    [SerializeField] private Color missingHealthColor;
    [SerializeField] private Vector3 bowHealthAxis;
    [SerializeField] private bool restoreBowWhenNotHeld;

    [Header("Screen Health")]
    [SerializeField] private bool showScreenBarWhenBowNotHeld;
    [SerializeField] private Rect screenBarNormalizedRect;
    [SerializeField] private Color screenBarBackgroundColor;
    [SerializeField] private Color screenBarFillColor;

    private XRGrabInteractable grabInteractable;
    private Material[] originalBowMaterials;
    private Material bowHealthMaterial;
    private bool hasOriginalBowState;
    private bool isBowHeld;
    private bool warnedMissingSegmentShader;
    private Texture2D screenBarTexture;

    private void Awake()
    {
        ResolveReferences();
        CacheOriginalBowState();
        ApplyVisualState();
    }

    private void OnEnable()
    {
        ResolveGrabInteractable();

        if (grabInteractable != null)
        {
            grabInteractable.selectEntered.AddListener(OnBowSelected);
            grabInteractable.selectExited.AddListener(OnBowReleased);
        }

        ApplyVisualState();
    }

    private void OnDisable()
    {
        if (grabInteractable != null)
        {
            grabInteractable.selectEntered.RemoveListener(OnBowSelected);
            grabInteractable.selectExited.RemoveListener(OnBowReleased);
        }

        RestoreBowVisual();
    }

    private void LateUpdate()
    {
        ResolveReferences();

        if (grabInteractable != null)
        {
            isBowHeld = grabInteractable.isSelected;
        }

        ApplyVisualState();
    }

    private void OnValidate()
    {
        screenBarNormalizedRect.x = Mathf.Clamp01(screenBarNormalizedRect.x);
        screenBarNormalizedRect.y = Mathf.Clamp01(screenBarNormalizedRect.y);
        screenBarNormalizedRect.width = Mathf.Clamp01(screenBarNormalizedRect.width);
        screenBarNormalizedRect.height = Mathf.Clamp01(screenBarNormalizedRect.height);
    }

    private void OnGUI()
    {
        if (!ShouldDrawScreenBar()) return;

        Rect barRect = GetScreenBarRect();
        GUI.color = screenBarBackgroundColor;
        GUI.DrawTexture(barRect, GetScreenBarTexture());

        float healthFraction = GetHealthFraction();
        Rect fillRect = barRect;
        fillRect.yMin = fillRect.yMax - fillRect.height * healthFraction;

        GUI.color = screenBarFillColor;
        GUI.DrawTexture(fillRect, GetScreenBarTexture());
        GUI.color = Color.white;
    }

    private void OnBowSelected(SelectEnterEventArgs args)
    {
        isBowHeld = true;
        ApplyVisualState();
    }

    private void OnBowReleased(SelectExitEventArgs args)
    {
        isBowHeld = grabInteractable != null && grabInteractable.isSelected;
        ApplyVisualState();
    }

    private void ResolveReferences()
    {
        ResolveGrabInteractable();

        if (playerHealth == null)
        {
            playerHealth = FindAnyObjectByType<PlayerHealth>();
        }

        if (bowHealthRenderer == null && !string.IsNullOrWhiteSpace(bowHealthRendererName))
        {
            Transform rendererTransform = FindChildRecursive(transform, bowHealthRendererName);
            if (rendererTransform != null)
            {
                bowHealthRenderer = rendererTransform.GetComponent<Renderer>();
            }
        }

        if (bowHealthRenderer == null)
        {
            bowHealthRenderer = ResolveFallbackBowRenderer();
        }

        CacheOriginalBowState();
    }

    private Renderer ResolveFallbackBowRenderer()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null) continue;

            string lowerName = renderer.name.ToLowerInvariant();
            if (lowerName.Contains("bogen") || lowerName.Contains("bow"))
            {
                return renderer;
            }
        }

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null) continue;

            string lowerName = renderer.name.ToLowerInvariant();
            if (lowerName.Contains("string") || lowerName.Contains("arrow") || lowerName.Contains("health") || lowerName.Contains("prompt"))
            {
                continue;
            }

            return renderer;
        }

        return null;
    }

    private void ResolveGrabInteractable()
    {
        if (grabInteractable == null)
        {
            grabInteractable = GetComponent<XRGrabInteractable>();
        }
    }

    private void CacheOriginalBowState()
    {
        if (hasOriginalBowState || bowHealthRenderer == null) return;

        originalBowMaterials = bowHealthRenderer.sharedMaterials;
        hasOriginalBowState = true;
    }

    private void ApplyVisualState()
    {
        if (bowHealthRenderer == null || !hasOriginalBowState) return;

        if (isBowHeld)
        {
            ApplyBowHealthVisual();
        }
        else if (restoreBowWhenNotHeld)
        {
            RestoreBowVisual();
        }
    }

    private bool ShouldDrawScreenBar()
    {
        if (!showScreenBarWhenBowNotHeld || isBowHeld) return false;
        if (playerHealth == null || playerHealth.MaxHealth <= 0f || playerHealth.IsDead) return false;

        return playerHealth.CurrentHealth < playerHealth.MaxHealth;
    }

    private Rect GetScreenBarRect()
    {
        return new Rect(
            screenBarNormalizedRect.x * Screen.width,
            screenBarNormalizedRect.y * Screen.height,
            screenBarNormalizedRect.width * Screen.width,
            screenBarNormalizedRect.height * Screen.height
        );
    }

    private Texture2D GetScreenBarTexture()
    {
        if (screenBarTexture == null)
        {
            screenBarTexture = Texture2D.whiteTexture;
        }

        return screenBarTexture;
    }

    private void ApplyBowHealthVisual()
    {
        float healthFraction = GetHealthFraction();
        Material healthMaterial = GetBowHealthMaterial();
        UpdateBowHealthMaterial(healthMaterial, healthFraction);

        Material[] healthMaterials = bowHealthRenderer.sharedMaterials;
        for (int i = 0; i < healthMaterials.Length; i++)
        {
            healthMaterials[i] = healthMaterial;
        }

        bowHealthRenderer.sharedMaterials = healthMaterials;
    }

    private void RestoreBowVisual()
    {
        if (bowHealthRenderer == null || !hasOriginalBowState) return;

        if (originalBowMaterials != null && originalBowMaterials.Length > 0)
        {
            bowHealthRenderer.sharedMaterials = originalBowMaterials;
        }
    }

    private Material GetBowHealthMaterial()
    {
        if (bowHealthMaterial != null)
        {
            return bowHealthMaterial;
        }

        Material template = Resources.Load<Material>("BowHealthBarRuntime");
        if (template != null)
        {
            bowHealthMaterial = new Material(template);
            bowHealthMaterial.name = "Runtime Bow Health Material";
            bowHealthMaterial.hideFlags = HideFlags.DontSave;
            return bowHealthMaterial;
        }

        Shader shader = Shader.Find("Cavehunt/BowHealthBar");
        if (shader == null)
        {
            shader = Shader.Find("Universal Render Pipeline/Unlit");
        }
        if (shader == null)
        {
            shader = Shader.Find("Unlit/Color");
        }
        if (shader == null)
        {
            shader = Shader.Find("Sprites/Default");
        }

        bowHealthMaterial = new Material(shader);
        if (!bowHealthMaterial.HasProperty("_MissingSegmentCount") && !warnedMissingSegmentShader)
        {
            warnedMissingSegmentShader = true;
            Debug.LogWarning("Bow health shader is missing segment properties. Check Always Included Shaders for Cavehunt/BowHealthBar.");
        }
        bowHealthMaterial.name = "Runtime Bow Health Material";
        bowHealthMaterial.hideFlags = HideFlags.DontSave;
        return bowHealthMaterial;
    }

    private void UpdateBowHealthMaterial(Material material, float healthFraction)
    {
        if (material == null) return;

        SetMaterialColor(material, bowHealthColor);
        SetMaterialColor(material, "_HealthColor", bowHealthColor);
        SetMaterialColor(material, "_MissingHealthColor", missingHealthColor);
        SetMaterialFloat(material, "_HealthFraction", healthFraction);
        SetMaterialFloat(material, "_SegmentCount", GetHealthSegmentCount());
        SetMaterialFloat(material, "_MissingSegmentCount", GetMissingHealthSegmentCount());

        Vector3 axis = bowHealthAxis.sqrMagnitude > 0.0001f ? bowHealthAxis.normalized : Vector3.up;
        Bounds localBounds = bowHealthRenderer.localBounds;
        float axisCenter = Vector3.Dot(localBounds.center, axis);
        Vector3 extents = localBounds.extents;
        float axisExtent =
            Mathf.Abs(axis.x) * extents.x +
            Mathf.Abs(axis.y) * extents.y +
            Mathf.Abs(axis.z) * extents.z;

        SetMaterialVector(material, "_Axis", axis);
        SetMaterialFloat(material, "_AxisMin", axisCenter - axisExtent);
        SetMaterialFloat(material, "_AxisMax", axisCenter + axisExtent);
    }

    private float GetHealthFraction()
    {
        if (playerHealth == null || playerHealth.MaxHealth <= 0f) return 0f;

        return Mathf.Clamp01(playerHealth.CurrentHealth / playerHealth.MaxHealth);
    }

    private float GetHealthSegmentCount()
    {
        if (playerHealth == null || playerHealth.MaxHealth <= 0f) return 1f;

        return Mathf.Max(1f, Mathf.Round(playerHealth.MaxHealth));
    }
    private float GetMissingHealthSegmentCount()
    {
        if (playerHealth == null || playerHealth.MaxHealth <= 0f) return 0f;

        return Mathf.Clamp(
            Mathf.Ceil(playerHealth.MaxHealth - playerHealth.CurrentHealth),
            0f,
            GetHealthSegmentCount()
        );
    }
    private static void SetMaterialColor(Material material, Color color)
    {
        if (material == null) return;

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }
        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", color);
        }
    }

    private static void SetMaterialColor(Material material, string propertyName, Color color)
    {
        if (material != null && material.HasProperty(propertyName))
        {
            material.SetColor(propertyName, color);
        }
    }

    private static void SetMaterialFloat(Material material, string propertyName, float value)
    {
        if (material != null && material.HasProperty(propertyName))
        {
            material.SetFloat(propertyName, value);
        }
    }

    private static void SetMaterialVector(Material material, string propertyName, Vector3 value)
    {
        if (material != null && material.HasProperty(propertyName))
        {
            material.SetVector(propertyName, value);
        }
    }

    private static Transform FindChildRecursive(Transform parent, string childName)
    {
        if (parent == null || string.IsNullOrWhiteSpace(childName)) return null;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name == childName)
            {
                return child;
            }

            Transform found = FindChildRecursive(child, childName);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }
}
