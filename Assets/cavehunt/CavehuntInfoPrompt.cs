using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR;

public class CavehuntInfoPrompt : MonoBehaviour
{
    private const string GameplaySceneName = "03-Interactions";
    private const string StartupSplashResourcePath = "Intro/cavehunt_mechanics_overview";

    private enum PromptStyle
    {
        Screen,
        ImageScreen,
        TextOnly
    }

    private static CavehuntInfoPrompt instance;

    private GameObject visualRoot;
    private Transform screenTransform;
    private MeshRenderer screenRenderer;
    private Material screenMaterial;
    private TextMesh titleLabel;
    private TextMesh bodyLabel;
    private TextMesh confirmLabel;
    private Action onDismiss;
    private PromptStyle currentStyle;
    private bool visible;
    private bool pauseGame;
    private bool confirmArmed;
    private float previousTimeScale = 1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void ShowStartupSplash()
    {
        if (SceneManager.GetActiveScene().name != GameplaySceneName) return;

        CavehuntInfoPrompt prompt = EnsureInstance();
        prompt.StartCoroutine(prompt.ShowStartupSplashAfterFrame());
    }

    public static void Show(string title, string body, Action dismissed = null, bool pause = true)
    {
        EnsureInstance().ShowInternal(title, body, dismissed, pause, PromptStyle.Screen);
    }

    public static void ShowTextOnly(string title, string body, Action dismissed = null, bool pause = true)
    {
        EnsureInstance().ShowInternal(title, body, dismissed, pause, PromptStyle.TextOnly);
    }

    public static void HideActive()
    {
        if (instance != null)
        {
            instance.HideInternal();
        }
    }

    private static CavehuntInfoPrompt EnsureInstance()
    {
        if (instance != null) return instance;

        CavehuntInfoPrompt existing = FindAnyObjectByType<CavehuntInfoPrompt>(FindObjectsInactive.Include);
        if (existing != null)
        {
            instance = existing;
            return instance;
        }

        GameObject promptObject = new GameObject("Cavehunt Info Prompt");
        instance = promptObject.AddComponent<CavehuntInfoPrompt>();
        return instance;
    }

    private IEnumerator ShowStartupSplashAfterFrame()
    {
        yield return null;
        ShowInternal(string.Empty, string.Empty, null, true, PromptStyle.ImageScreen);
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        EnsureVisuals();
        HideInternal(false);
    }

    private void Update()
    {
        if (!visible) return;

        bool confirmPressed = IsConfirmPressed();
        if (!confirmPressed)
        {
            confirmArmed = true;
        }
        else if (confirmArmed)
        {
            Action callback = onDismiss;
            HideInternal();
            callback?.Invoke();
        }
    }

    private void LateUpdate()
    {
        if (!visible) return;
        PositionInFrontOfCamera();
    }

    private void ShowInternal(string title, string body, Action dismissed, bool pause, PromptStyle style)
    {
        EnsureVisuals();

        if (!visible)
        {
            previousTimeScale = Time.timeScale;
        }

        visible = true;
        pauseGame = pause;
        onDismiss = dismissed;
        currentStyle = style;
        confirmArmed = false;

        ConfigureStyle(style);
        titleLabel.text = title;
        bodyLabel.text = body;
        confirmLabel.text = "Press A";

        if (pauseGame)
        {
            Time.timeScale = 0f;
        }

        visualRoot.SetActive(true);
        PositionInFrontOfCamera();
    }

    private void HideInternal(bool restoreTime = true)
    {
        if (restoreTime && visible && pauseGame)
        {
            Time.timeScale = previousTimeScale <= 0f ? 1f : previousTimeScale;
        }

        visible = false;
        pauseGame = false;
        onDismiss = null;
        confirmArmed = false;

        if (visualRoot != null)
        {
            visualRoot.SetActive(false);
        }
    }

    private void EnsureVisuals()
    {
        if (visualRoot != null) return;

        visualRoot = new GameObject("Cavehunt Info Prompt Visuals");
        visualRoot.transform.SetParent(transform, false);

        GameObject screen = GameObject.CreatePrimitive(PrimitiveType.Quad);
        screen.name = "Prompt Screen";
        screen.transform.SetParent(visualRoot.transform, false);
        screenTransform = screen.transform;

        Collider collider = screen.GetComponent<Collider>();
        if (collider != null)
        {
            Destroy(collider);
        }

        screenRenderer = screen.GetComponent<MeshRenderer>();
        if (screenRenderer != null)
        {
            screenRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            screenRenderer.receiveShadows = false;
            screenRenderer.sortingOrder = 6400;

            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            }

            if (shader != null)
            {
                screenMaterial = new Material(shader);
                screenMaterial.name = "Cavehunt Info Prompt Screen Material";
                screenMaterial.color = new Color(0.015f, 0.018f, 0.024f, 0.96f);
                screenRenderer.sharedMaterial = screenMaterial;
            }
        }

        titleLabel = CreateTextMesh("Prompt Title", 86, 0.030f, FontStyle.Bold);
        bodyLabel = CreateTextMesh("Prompt Body", 52, 0.018f, FontStyle.Bold);
        confirmLabel = CreateTextMesh("Prompt Confirm", 42, 0.016f, FontStyle.Bold);
    }

    private TextMesh CreateTextMesh(string objectName, int fontSize, float characterSize, FontStyle style)
    {
        GameObject labelObject = new GameObject(objectName);
        labelObject.transform.SetParent(visualRoot.transform, false);

        TextMesh label = labelObject.AddComponent<TextMesh>();
        label.anchor = TextAnchor.MiddleCenter;
        label.alignment = TextAlignment.Center;
        label.font = ResolveFont();
        label.fontSize = fontSize;
        label.fontStyle = style;
        label.characterSize = characterSize;
        label.color = Color.white;
        label.richText = false;

        MeshRenderer renderer = labelObject.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            renderer.sortingOrder = 6500;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            if (label.font != null && label.font.material != null)
            {
                renderer.sharedMaterial = label.font.material;
            }
        }

        return label;
    }

    private void ConfigureStyle(PromptStyle style)
    {
        bool imageScreen = style == PromptStyle.ImageScreen;
        bool textScreen = style == PromptStyle.Screen;
        bool showScreen = imageScreen || textScreen;

        if (screenRenderer != null)
        {
            screenRenderer.gameObject.SetActive(showScreen);
        }

        if (screenMaterial != null)
        {
            screenMaterial.mainTexture = imageScreen ? Resources.Load<Texture2D>(StartupSplashResourcePath) : null;
            screenMaterial.color = imageScreen
                ? Color.white
                : new Color(0.015f, 0.018f, 0.024f, 0.96f);
        }

        if (screenTransform != null)
        {
            screenTransform.localScale = imageScreen
                ? new Vector3(2.7f, 1.52f, 1f)
                : new Vector3(2.45f, 1.35f, 1f);
        }

        titleLabel.gameObject.SetActive(textScreen || style == PromptStyle.TextOnly);
        bodyLabel.gameObject.SetActive(textScreen || style == PromptStyle.TextOnly);
        confirmLabel.gameObject.SetActive(true);

        titleLabel.fontSize = textScreen ? 86 : 70;
        titleLabel.characterSize = textScreen ? 0.030f : 0.032f;
        bodyLabel.fontSize = textScreen ? 52 : 44;
        bodyLabel.characterSize = textScreen ? 0.018f : 0.020f;
        confirmLabel.fontSize = imageScreen ? 34 : (textScreen ? 42 : 36);
        confirmLabel.characterSize = imageScreen ? 0.012f : (textScreen ? 0.016f : 0.017f);
    }

    private void PositionInFrontOfCamera()
    {
        if (visualRoot == null) return;

        Camera camera = VrCameraResolver.GetCamera();
        if (camera == null) camera = Camera.main;
        if (camera == null) return;

        Transform cameraTransform = camera.transform;
        Vector3 flatForward = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up);
        if (flatForward.sqrMagnitude < 0.0001f)
        {
            flatForward = Vector3.forward;
        }

        flatForward.Normalize();
        bool imageScreen = currentStyle == PromptStyle.ImageScreen;
        float distance = imageScreen ? 1.65f : (currentStyle == PromptStyle.Screen ? 1.45f : 1.85f);
        Vector3 center = cameraTransform.position + flatForward * distance;
        center.y = cameraTransform.position.y + (currentStyle == PromptStyle.TextOnly ? 0.08f : 0f);

        Quaternion rotation = Quaternion.LookRotation(flatForward, Vector3.up);
        visualRoot.transform.position = center;
        visualRoot.transform.rotation = rotation;
        visualRoot.transform.localScale = Vector3.one;

        float textZ = -0.055f;
        if (imageScreen)
        {
            SetLocalText(confirmLabel, new Vector3(0f, -0.86f, textZ));
        }
        else if (currentStyle == PromptStyle.Screen)
        {
            SetLocalText(titleLabel, new Vector3(0f, 0.36f, textZ));
            SetLocalText(bodyLabel, new Vector3(0f, 0.02f, textZ));
            SetLocalText(confirmLabel, new Vector3(0f, -0.40f, textZ));
        }
        else
        {
            SetLocalText(titleLabel, new Vector3(0f, 0.12f, textZ));
            SetLocalText(bodyLabel, new Vector3(0f, -0.04f, textZ));
            SetLocalText(confirmLabel, new Vector3(0f, -0.22f, textZ));
        }
    }

    private static void SetLocalText(TextMesh label, Vector3 localPosition)
    {
        if (label == null) return;

        label.transform.localPosition = localPosition;
        label.transform.localRotation = Quaternion.identity;
        label.transform.localScale = Vector3.one;
    }

    private static bool IsConfirmPressed()
    {
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.Space) || Input.GetKey(KeyCode.Return))
        {
            return true;
        }

        return IsXRButtonPressed(XRNode.RightHand, CommonUsages.primaryButton)
            || IsXRButtonPressed(XRNode.LeftHand, CommonUsages.primaryButton);
    }

    private static bool IsXRButtonPressed(XRNode node, InputFeatureUsage<bool> usage)
    {
        InputDevice device = InputDevices.GetDeviceAtXRNode(node);
        return device.isValid && device.TryGetFeatureValue(usage, out bool pressed) && pressed;
    }

    private static Font ResolveFont()
    {
        Font font = TryGetBuiltinFont("LegacyRuntime.ttf");
        if (font != null) return font;

        return TryGetBuiltinFont("Arial.ttf");
    }

    private static Font TryGetBuiltinFont(string fontName)
    {
        try
        {
            return Resources.GetBuiltinResource<Font>(fontName);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }
}