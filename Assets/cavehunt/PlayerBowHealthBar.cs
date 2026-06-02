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

    private void OnGUI()
    {
        if (!showScreenBarWhenBowNotHeld || isBowHeld || playerHealth == null) return;
        if (screenBarNormalizedRect.width <= 0f || screenBarNormalizedRect.height <= 0f) return;

        float healthFraction = GetHealthFraction();
        Rect backgroundRect = new Rect(
            Screen.width * screenBarNormalizedRect.x,
            Screen.height * screenBarNormalizedRect.y,
            Screen.width * screenBarNormalizedRect.width,
            Screen.height * screenBarNormalizedRect.height
        );

        float fillHeight = backgroundRect.height * healthFraction;
        Rect fillRect = new Rect(
            backgroundRect.x,
            backgroundRect.y + backgroundRect.height - fillHeight,
            backgroundRect.width,
            fillHeight
        );

        Color previousColor = GUI.color;
        GUI.color = screenBarBackgroundColor;
        GUI.DrawTexture(backgroundRect, Texture2D.whiteTexture);
        GUI.color = screenBarFillColor;
        GUI.DrawTexture(fillRect, Texture2D.whiteTexture);
        GUI.color = previousColor;
    }

    private void OnValidate()
    {
        screenBarNormalizedRect.x = Mathf.Clamp01(screenBarNormalizedRect.x);
        screenBarNormalizedRect.y = Mathf.Clamp01(screenBarNormalizedRect.y);
        screenBarNormalizedRect.width = Mathf.Clamp01(screenBarNormalizedRect.width);
        screenBarNormalizedRect.height = Mathf.Clamp01(screenBarNormalizedRect.height);
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

        CacheOriginalBowState();
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
