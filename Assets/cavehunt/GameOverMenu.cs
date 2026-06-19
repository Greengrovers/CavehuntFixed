using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.XR;

#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
public class GameOverMenu : MonoBehaviour
{
    private static GameOverMenu visibleMenu;

    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private string backgroundResourcePath = "GameOver/GameOverBackground";
    [SerializeField] private float menuDistance = 1.0f;
    [SerializeField] private Vector2 canvasSize = new Vector2(900f, 1120f);
    [SerializeField] private float canvasScale = 0.0026f;
    [SerializeField] private float backgroundCurve = 2f;
    [SerializeField] private int backgroundCurveSegments = 40;
    [SerializeField] private Color titleColor = Color.white;
    [SerializeField] private Color hintColor = Color.white;
    [SerializeField] private Color buttonColor = new Color(0.035f, 0.035f, 0.035f, 1f);
    [SerializeField] private Color selectedButtonColor = new Color(0.13f, 0.13f, 0.12f, 1f);
    [SerializeField] private Color selectedTextColor = Color.white;
    [SerializeField] private Color normalTextColor = Color.white;
    [SerializeField] private float navigationRepeatDelay = 0.22f;

    private readonly GameObject[] buttonObjects = new GameObject[2];
    private readonly Image[] buttonImages = new Image[2];
    private readonly Text[] buttonLabels = new Text[2];
    private readonly TextMesh[] buttonWorldLabels = new TextMesh[2];
    private readonly MeshRenderer[] buttonWorldBackgrounds = new MeshRenderer[2];

    private GameObject worldTextRoot;
    private TextMesh titleWorldLabel;
    private TextMesh hintWorldLabel;

    private Canvas canvas;
    private RectTransform canvasRect;
    private bool visible;
    private int selectedIndex;
    private float nextNavigationTime;
    private float previousTimeScale = 1f;
    private bool previousAudioPause;
    private bool previousXRConfirmPressed;

    public bool IsVisible => visible;

    public void Configure(PlayerHealth health)
    {
        playerHealth = health;
    }

    private void Awake()
    {
        if (playerHealth == null)
        {
            playerHealth = GetComponent<PlayerHealth>();
        }

        EnsureMenu();
        Hide();
    }

    private void Update()
    {
        if (!visible) return;

        HandleNavigationInput();

        if (WasConfirmPressed())
        {
            ActivateSelectedButton();
        }
    }

    private void LateUpdate()
    {
        if (visible)
        {
            PositionInFrontOfCamera();
        }
    }

    public void Show()
    {
        EnsureMenu();

        HideOtherMenus();
        visibleMenu = this;

        if (!visible)
        {
            previousTimeScale = Time.timeScale;
            previousAudioPause = AudioListener.pause;
        }

        visible = true;
        selectedIndex = 0;
        ApplyButtonColorDefaults();

        Time.timeScale = 0f;
        AudioListener.pause = true;

        canvas.gameObject.SetActive(true);
        SetWorldTextActive(true);
        PositionInFrontOfCamera();
        RefreshSelectionVisuals();
    }

    public void Hide()
    {
        if (visible)
        {
            Time.timeScale = previousTimeScale <= 0f ? 1f : previousTimeScale;
            AudioListener.pause = previousAudioPause;
        }

        visible = false;
        if (visibleMenu == this)
        {
            visibleMenu = null;
        }

        if (canvas != null)
        {
            canvas.gameObject.SetActive(false);
        }

        SetWorldTextActive(false);
    }


    private void ApplyButtonColorDefaults()
    {
        buttonColor = new Color(0.035f, 0.035f, 0.035f, 1f);
        selectedButtonColor = new Color(0.13f, 0.13f, 0.12f, 1f);
    }
    private void HideOtherMenus()
    {
        GameOverMenu[] menus = FindObjectsByType<GameOverMenu>(FindObjectsInactive.Include);
        for (int i = 0; i < menus.Length; i++)
        {
            GameOverMenu menu = menus[i];
            if (menu == null || menu == this) continue;
            menu.Hide();
        }
    }
    public void Retry()
    {
        Hide();

        if (playerHealth != null)
        {
            playerHealth.ResetToFullHealth();
        }
    }

    public void Quit()
    {
        Hide();

#if UNITY_EDITOR
        EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void EnsureMenu()
    {
        if (canvas != null) return;

        EnsureEventSystem();

        GameObject root = new GameObject("Custom Game Over Menu");
        root.transform.SetParent(transform, false);

        canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 5000;

        root.AddComponent<GraphicRaycaster>();

        CanvasScaler scaler = root.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 1200f;

        canvasRect = root.GetComponent<RectTransform>();
        canvasRect.sizeDelta = canvasSize;
        canvasRect.localScale = Vector3.one * canvasScale;

        CreateBackground(canvasRect);

        CreateText(
            "Game Over Title",
            canvasRect,
            "Game Over",
            220,
            titleColor,
            new Vector2(0.04f, 0.62f),
            new Vector2(0.96f, 0.88f),
            FontStyle.Bold
        );

        CreateText(
            "Game Over Hint",
            canvasRect,
            "The cave claimed this run.",
            100,
            hintColor,
            new Vector2(0.04f, 0.47f),
            new Vector2(0.96f, 0.61f),
            FontStyle.Bold
        );

        CreateButton(
            canvasRect,
            0,
            "Retry",
            new Vector2(0.10f, 0.30f),
            new Vector2(0.90f, 0.43f),
            Retry
        );

        CreateButton(
            canvasRect,
            1,
            "Quit",
            new Vector2(0.18f, 0.02f),
            new Vector2(0.82f, 0.15f),
            Quit
        );

        CreateWorldTextLabels();
        RefreshSelectionVisuals();
    }

    private void CreateBackground(RectTransform parent)
    {
        GameObject backgroundObject = new GameObject("Illustrator Background");
        backgroundObject.transform.SetParent(parent, false);
        backgroundObject.transform.SetAsFirstSibling();

        RectTransform rect = backgroundObject.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(-0.25f, 0f);
        rect.anchorMax = new Vector2(1.25f, 1f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localPosition = Vector3.zero;

        CurvedRawImage image = backgroundObject.AddComponent<CurvedRawImage>();
        image.raycastTarget = false;
        image.color = Color.white;
        image.Curve = backgroundCurve;
        image.Segments = backgroundCurveSegments;

        Texture2D texture = Resources.Load<Texture2D>(backgroundResourcePath);

        if (texture != null)
        {
            image.texture = texture;
        }
        else
        {
            image.color = new Color(0.58f, 0.02f, 0.01f, 1f);
        }
    }

    private Text CreateText(
        string objectName,
        RectTransform parent,
        string text,
        int size,
        Color color,
        Vector2 anchorMin,
        Vector2 anchorMax,
        FontStyle style
    )
    {
        GameObject textObject = new GameObject(objectName);
        textObject.transform.SetParent(parent, false);
        textObject.transform.SetAsLastSibling();

        RectTransform rect = textObject.AddComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localPosition = Vector3.zero;

        Text label = textObject.AddComponent<Text>();
        label.font = ResolveFont();
        label.text = text;
        label.fontSize = size;
        label.fontStyle = style;
        label.color = color;
        label.alignment = TextAnchor.MiddleCenter;
        label.horizontalOverflow = HorizontalWrapMode.Wrap;
        label.verticalOverflow = VerticalWrapMode.Truncate;
        label.resizeTextForBestFit = true;
        label.resizeTextMinSize = 60;
        label.resizeTextMaxSize = size;
        label.raycastTarget = false;

        Shadow shadow = textObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.92f);
        shadow.effectDistance = new Vector2(2f, -2f);

        Outline outline = textObject.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.62f);
        outline.effectDistance = new Vector2(1.25f, 1.25f);

        return label;
    }

    private void CreateButton(
        RectTransform parent,
        int index,
        string text,
        Vector2 anchorMin,
        Vector2 anchorMax,
        UnityEngine.Events.UnityAction action
    )
    {
        GameObject buttonObject = new GameObject(text + " Button");
        buttonObject.transform.SetParent(parent, false);
        buttonObject.transform.SetAsLastSibling();

        RectTransform rect = buttonObject.AddComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localPosition = Vector3.zero;

        Image image = buttonObject.AddComponent<Image>();
        image.color = Color.clear;
        image.raycastTarget = false;

        Button button = buttonObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.onClick.AddListener(action);

        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = Color.white;
        colors.selectedColor = Color.white;
        colors.pressedColor = new Color(0.75f, 0.75f, 0.75f, 1f);
        button.colors = colors;

        Text label = CreateText(
            text + " Label",
            rect,
            text,
            150,
            normalTextColor,
            Vector2.zero,
            Vector2.one,
            FontStyle.Bold
        );

        buttonObjects[index] = buttonObject;
        buttonImages[index] = image;
        buttonLabels[index] = label;
    }

    private void HandleNavigationInput()
    {
        float vertical = Input.GetAxisRaw("Vertical");
        vertical = Mathf.Abs(vertical) > 0.25f ? vertical : GetXRNavigationY();

        bool keyboardDown = Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S);
        bool keyboardUp = Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W);

        bool stickDown = vertical < -0.65f && Time.unscaledTime >= nextNavigationTime;
        bool stickUp = vertical > 0.65f && Time.unscaledTime >= nextNavigationTime;

        if (keyboardDown || stickDown)
        {
            SelectButton(selectedIndex + 1);
            nextNavigationTime = Time.unscaledTime + navigationRepeatDelay;
        }
        else if (keyboardUp || stickUp)
        {
            SelectButton(selectedIndex - 1);
            nextNavigationTime = Time.unscaledTime + navigationRepeatDelay;
        }
    }

    private void SelectButton(int index)
    {
        selectedIndex = Mathf.Clamp(index, 0, buttonObjects.Length - 1);
        RefreshSelectionVisuals();
    }

    private void RefreshSelectionVisuals()
    {
        for (int i = 0; i < buttonImages.Length; i++)
        {
            if (buttonImages[i] == null || buttonLabels[i] == null) continue;

            bool selected = i == selectedIndex;
            buttonImages[i].color = Color.clear;
            SetWorldButtonBackgroundColor(i, selected ? selectedButtonColor : buttonColor);
            buttonLabels[i].color = selected ? selectedTextColor : normalTextColor;
            if (buttonWorldLabels[i] != null)
            {
                buttonWorldLabels[i].color = selected ? selectedTextColor : normalTextColor;
            }
        }

        EventSystem eventSystem = EventSystem.current;

        if (eventSystem != null && selectedIndex >= 0 && selectedIndex < buttonObjects.Length)
        {
            eventSystem.SetSelectedGameObject(buttonObjects[selectedIndex]);
        }
    }

    private void ActivateSelectedButton()
    {
        if (selectedIndex == 0)
        {
            Retry();
        }
        else
        {
            Quit();
        }
    }

    private bool WasConfirmPressed()
    {
        if (
            Input.GetKeyDown(KeyCode.R) ||
            Input.GetKeyDown(KeyCode.Space) ||
            Input.GetKeyDown(KeyCode.Return)
        )
        {
            return true;
        }

        bool xrConfirmPressed =
            IsXRButtonPressed(CommonUsages.primaryButton) ||
            IsXRButtonPressed(CommonUsages.triggerButton);

        bool pressedThisFrame = xrConfirmPressed && !previousXRConfirmPressed;
        previousXRConfirmPressed = xrConfirmPressed;

        return pressedThisFrame;
    }

    private static float GetXRNavigationY()
    {
        if (
            TryGetXRAxis(CommonUsages.primary2DAxis, out Vector2 primaryAxis) &&
            Mathf.Abs(primaryAxis.y) > 0.25f
        )
        {
            return primaryAxis.y;
        }

        if (
            TryGetXRAxis(CommonUsages.secondary2DAxis, out Vector2 secondaryAxis) &&
            Mathf.Abs(secondaryAxis.y) > 0.25f
        )
        {
            return secondaryAxis.y;
        }

        return 0f;
    }

    private static bool TryGetXRAxis(InputFeatureUsage<Vector2> usage, out Vector2 axis)
    {
        axis = Vector2.zero;

        InputDevice leftHand = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);

        if (leftHand.isValid && leftHand.TryGetFeatureValue(usage, out axis))
        {
            return true;
        }

        InputDevice rightHand = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);

        return rightHand.isValid && rightHand.TryGetFeatureValue(usage, out axis);
    }

    private static bool IsXRButtonPressed(InputFeatureUsage<bool> usage)
    {
        InputDevice rightHand = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);

        if (
            rightHand.isValid &&
            rightHand.TryGetFeatureValue(usage, out bool rightPressed) &&
            rightPressed
        )
        {
            return true;
        }

        InputDevice leftHand = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);

        return
            leftHand.isValid &&
            leftHand.TryGetFeatureValue(usage, out bool leftPressed) &&
            leftPressed;
    }

    private void PositionInFrontOfCamera()
    {
        if (canvasRect == null) return;

        Camera camera = VrCameraResolver.GetCamera();
        if (camera == null) return;

        Transform cameraTransform = camera.transform;

        canvasRect.position =
            cameraTransform.position +
            cameraTransform.forward * Mathf.Max(0.55f, menuDistance);

        canvasRect.rotation = Quaternion.LookRotation(
            canvasRect.position - cameraTransform.position,
            Vector3.up
        );

        canvasRect.localScale = Vector3.one * canvasScale;
        UpdateWorldTextTransform(camera);
    }

    private void CreateWorldTextLabels()
    {
        if (worldTextRoot != null) return;

        worldTextRoot = new GameObject("Game Over Readable 3D Text");
        worldTextRoot.transform.SetParent(transform, false);

        titleWorldLabel = CreateWorldTextLabel("Game Over 3D Title", "Game Over", 96, 0.026f, titleColor, FontStyle.Bold);
        hintWorldLabel = CreateWorldTextLabel("Game Over 3D Hint", "The cave claimed this run.", 72, 0.015f, hintColor, FontStyle.Bold);
        buttonWorldLabels[0] = CreateWorldTextLabel("Retry 3D Label", "Retry", 88, 0.022f, normalTextColor, FontStyle.Bold);
        buttonWorldLabels[1] = CreateWorldTextLabel("Quit 3D Label", "Quit", 88, 0.022f, normalTextColor, FontStyle.Bold);
        buttonWorldBackgrounds[0] = CreateWorldButtonBackground("Retry 3D Background");
        buttonWorldBackgrounds[1] = CreateWorldButtonBackground("Quit 3D Background");

        SetWorldTextActive(false);
    }


    private MeshRenderer CreateWorldButtonBackground(string objectName)
    {
        GameObject backgroundObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
        backgroundObject.name = objectName;
        backgroundObject.transform.SetParent(worldTextRoot.transform, false);

        Collider collider = backgroundObject.GetComponent<Collider>();
        if (collider != null)
        {
            Destroy(collider);
        }

        MeshRenderer renderer = backgroundObject.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sortingOrder = 5900;

            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            }

            if (shader != null)
            {
                Material material = new Material(shader);
                material.name = objectName + " Material";
                material.color = buttonColor;
                renderer.sharedMaterial = material;
            }
        }

        return renderer;
    }

    private void SetWorldButtonBackgroundColor(int index, Color color)
    {
        if (index < 0 || index >= buttonWorldBackgrounds.Length) return;

        MeshRenderer renderer = buttonWorldBackgrounds[index];
        if (renderer == null || renderer.sharedMaterial == null) return;

        renderer.sharedMaterial.color = color;
    }

    private static void PositionWorldBackground(MeshRenderer renderer, Vector3 position, Quaternion rotation, Vector2 size)
    {
        if (renderer == null) return;

        Transform backgroundTransform = renderer.transform;
        backgroundTransform.position = position;
        backgroundTransform.rotation = rotation;
        backgroundTransform.localScale = new Vector3(size.x, size.y, 1f);
    }
    private TextMesh CreateWorldTextLabel(string objectName, string text, int fontSize, float characterSize, Color color, FontStyle style)
    {
        GameObject labelObject = new GameObject(objectName);
        labelObject.transform.SetParent(worldTextRoot.transform, false);

        TextMesh label = labelObject.AddComponent<TextMesh>();
        label.text = text;
        label.anchor = TextAnchor.MiddleCenter;
        label.alignment = TextAlignment.Center;
        label.font = ResolveFont();
        label.fontSize = fontSize;
        label.fontStyle = style;
        label.characterSize = characterSize;
        label.color = color;
        label.richText = false;

        MeshRenderer renderer = labelObject.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            renderer.sortingOrder = 6000;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            if (label.font != null && label.font.material != null)
            {
                renderer.sharedMaterial = label.font.material;
            }
        }

        return label;
    }

    private void SetWorldTextActive(bool active)
    {
        if (worldTextRoot != null)
        {
            worldTextRoot.SetActive(active);
        }
    }

    private void UpdateWorldTextTransform(Camera camera)
    {
        if (worldTextRoot == null || canvasRect == null || camera == null) return;

        worldTextRoot.SetActive(visible);

        Quaternion rotation = canvasRect.rotation;
        Vector3 center = canvasRect.position;
        Vector3 up = rotation * Vector3.up;
        Vector3 forward = rotation * Vector3.forward;
        Vector3 textPlane = center - forward * 0.09f;

        Vector3 retryPosition = textPlane - up * 0.04f;
        Vector3 quitPosition = textPlane - up * 0.30f;

        PositionWorldBackground(buttonWorldBackgrounds[0], retryPosition + forward * 0.018f, rotation, new Vector2(1.05f, 0.22f));
        PositionWorldBackground(buttonWorldBackgrounds[1], quitPosition + forward * 0.018f, rotation, new Vector2(0.78f, 0.20f));
        PositionWorldLabel(titleWorldLabel, textPlane + up * 0.43f, rotation);
        PositionWorldLabel(hintWorldLabel, textPlane + up * 0.25f, rotation);
        PositionWorldLabel(buttonWorldLabels[0], retryPosition, rotation);
        PositionWorldLabel(buttonWorldLabels[1], quitPosition, rotation);
    }

    private static void PositionWorldLabel(TextMesh label, Vector3 position, Quaternion rotation)
    {
        if (label == null) return;

        label.transform.position = position;
        label.transform.rotation = rotation;
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
        catch (System.ArgumentException)
        {
            return null;
        }
    }

    private static void EnsureEventSystem()
    {
        if (FindAnyObjectByType<EventSystem>() != null) return;

        GameObject eventSystemObject = new GameObject("EventSystem");
        eventSystemObject.AddComponent<EventSystem>();
        eventSystemObject.AddComponent<StandaloneInputModule>();
    }

    private sealed class CurvedRawImage : RawImage
    {
        private float curve = 2f;
        private int segments = 32;

        public float Curve
        {
            get => curve;
            set
            {
                curve = Mathf.Max(0f, value);
                SetVerticesDirty();
            }
        }

        public int Segments
        {
            get => segments;
            set
            {
                segments = Mathf.Clamp(value, 1, 128);
                SetVerticesDirty();
            }
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();

            Rect rect = GetPixelAdjustedRect();
            Rect uv = uvRect;
            int columnCount = Mathf.Max(1, segments);
            Color32 vertexColor = color;

            for (int x = 0; x <= columnCount; x++)
            {
                float t = x / (float)columnCount;
                float localX = Mathf.Lerp(rect.xMin, rect.xMax, t);
                float distanceFromCenter = Mathf.Abs(t - 0.5f) * 2f;
                float curveOffset = -distanceFromCenter * distanceFromCenter * curve;
                float uvX = Mathf.Lerp(uv.xMin, uv.xMax, t);

                vh.AddVert(new Vector3(localX, rect.yMin, curveOffset), vertexColor, new Vector2(uvX, uv.yMin));
                vh.AddVert(new Vector3(localX, rect.yMax, curveOffset), vertexColor, new Vector2(uvX, uv.yMax));
            }

            for (int x = 0; x < columnCount; x++)
            {
                int baseIndex = x * 2;
                vh.AddTriangle(baseIndex, baseIndex + 1, baseIndex + 2);
                vh.AddTriangle(baseIndex + 2, baseIndex + 1, baseIndex + 3);
            }
        }
    }}
