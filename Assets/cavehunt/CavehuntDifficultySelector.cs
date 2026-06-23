using System.Collections.Generic;
using UnityEngine;

public class CavehuntDifficultySelector : MonoBehaviour
{
    private const string TargetBackgroundResourceName = "DifficultyTargetBackground";

    [SerializeField] private float distanceFromPlayer = 3.5f;
    [SerializeField] private float verticalOffset = -0.05f;
    [SerializeField] private float targetSpacing = 1.45f;
    [SerializeField] private float targetScale = 0.85f;
    [SerializeField] private float targetBackgroundScaleMultiplier = 1.15f;
    [SerializeField] private float labelCharacterSize = 0.11f;
    [SerializeField] private Color labelColor = Color.white;
    [SerializeField] private bool useSceneAnchors = true;

    private CavehuntEncounterDirector director;
    private CavehuntDifficultySettings settings;
    private Transform selectionRoot;
    private bool selectionOpen;
    private bool usingSceneAnchors;
    private readonly List<GameObject> sceneAnchorVisuals = new List<GameObject>();
    private static Texture2D cachedTargetBackground;
    private static Texture cachedCaveTextTexture;

    public void ShowSelection(CavehuntEncounterDirector encounterDirector, CavehuntDifficultySettings difficultySettings)
    {
        director = encounterDirector;
        settings = difficultySettings != null ? difficultySettings : CavehuntDifficultySettings.Resolve();
        selectionOpen = true;

        EnsureSelectionRoot();
        RebuildTargets();
        PositionSelectionRoot();
        selectionRoot.gameObject.SetActive(true);
    }

    public void HideSelection()
    {
        selectionOpen = false;
        ClearSceneAnchorVisuals();
        if (selectionRoot != null)
        {
            selectionRoot.gameObject.SetActive(false);
        }
    }

    public void SelectDifficulty(int profileIndex)
    {
        if (!selectionOpen) return;

        HideSelection();
        if (director != null)
        {
            director.SelectDifficulty(profileIndex);
        }
    }

    private void LateUpdate()
    {
        if (!selectionOpen || selectionRoot == null || !selectionRoot.gameObject.activeSelf) return;

        FaceTargetsToCamera();
    }

    private void EnsureSelectionRoot()
    {
        if (selectionRoot != null) return;

        GameObject rootObject = new GameObject("Difficulty Selection Targets");
        rootObject.transform.SetParent(transform, false);
        selectionRoot = rootObject.transform;
    }

    private void RebuildTargets()
    {
        ClearSceneAnchorVisuals();
        for (int i = selectionRoot.childCount - 1; i >= 0; i--)
        {
            Destroy(selectionRoot.GetChild(i).gameObject);
        }

        int profileCount = settings != null ? settings.ProfileCount : 0;
        if (profileCount <= 0) return;

        Transform[] anchors = ResolveSceneAnchors(profileCount);
        usingSceneAnchors = anchors != null;
        if (usingSceneAnchors)
        {
            selectionRoot.SetParent(null, true);
            selectionRoot.position = Vector3.zero;
            selectionRoot.rotation = Quaternion.identity;
            selectionRoot.localScale = Vector3.one;
        }

        float centerOffset = (profileCount - 1) * 0.5f;
        for (int i = 0; i < profileCount; i++)
        {
            CavehuntDifficultySettings.DifficultyProfile profile = settings.GetProfile(i);
            string displayLabel = ResolveDisplayLabel(profile.DisplayName);

            if (usingSceneAnchors)
            {
                Transform anchor = anchors[i];
                EnsureDamageable(anchor.gameObject);
                CavehuntDifficultyTarget anchorTarget = EnsureDifficultyTarget(anchor.gameObject);
                anchorTarget.Configure(this, i);

                GameObject visualRoot = new GameObject($"Difficulty Visual {displayLabel}");
                visualRoot.transform.SetParent(anchor, false);
                visualRoot.transform.localPosition = Vector3.zero;
                visualRoot.transform.localRotation = Quaternion.identity;
                visualRoot.transform.localScale = Vector3.one;
                sceneAnchorVisuals.Add(visualRoot);

                CreateBackground(visualRoot.transform, profile.TargetColor, ResolveAnchorVisualScale(anchor, targetBackgroundScaleMultiplier));
                CreateLabel(visualRoot.transform, displayLabel);
                continue;
            }

            GameObject choice = new GameObject($"Difficulty Choice {displayLabel}");
            choice.transform.SetParent(selectionRoot, false);
            choice.transform.localScale = Vector3.one;
            choice.transform.localPosition = new Vector3((i - centerOffset) * targetSpacing, 0f, 0f);

            GameObject target = GameObject.CreatePrimitive(PrimitiveType.Quad);
            target.name = $"Difficulty Target Background {displayLabel}";
            target.transform.SetParent(choice.transform, false);
            target.transform.localPosition = Vector3.zero;
            target.transform.localScale = Vector3.one * targetScale * Mathf.Max(0.1f, targetBackgroundScaleMultiplier);

            Renderer renderer = target.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = CreateTargetMaterial(profile.TargetColor);
            }

            Damageable damageable = target.AddComponent<Damageable>();
            damageable.SetMaxHealth(1f);

            CavehuntDifficultyTarget difficultyTarget = target.AddComponent<CavehuntDifficultyTarget>();
            difficultyTarget.Configure(this, i);

            CreateLabel(choice.transform, displayLabel);
        }

        FaceTargetsToCamera();
    }

    private void CreateBackground(Transform parent, Color color, float visualScale)
    {
        GameObject target = GameObject.CreatePrimitive(PrimitiveType.Quad);
        target.name = "Difficulty Target Background";
        target.transform.SetParent(parent, false);
        target.transform.localPosition = Vector3.zero;
        target.transform.localRotation = Quaternion.identity;
        target.transform.localScale = Vector3.one * Mathf.Max(0.1f, visualScale);

        Collider collider = target.GetComponent<Collider>();
        if (collider != null)
        {
            Destroy(collider);
        }

        Renderer renderer = target.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = CreateTargetMaterial(color);
        }
    }

    private void CreateLabel(Transform target, string label)
    {
        GameObject labelObject = new GameObject("Difficulty Label");
        labelObject.transform.SetParent(target, false);
        labelObject.transform.localPosition = Vector3.back * 0.045f;
        labelObject.transform.localScale = Vector3.one;

        TextMesh text = labelObject.AddComponent<TextMesh>();
        text.text = label;
        text.anchor = TextAnchor.MiddleCenter;
        text.alignment = TextAlignment.Center;
        text.fontSize = 96;
        text.characterSize = Mathf.Max(0.01f, labelCharacterSize);
        text.color = labelColor;
        text.richText = false;

        ApplyCaveTextMaterial(text);
    }

    private void PositionSelectionRoot()
    {
        if (usingSceneAnchors)
        {
            selectionRoot.SetParent(null, true);
            return;
        }

        selectionRoot.SetParent(transform, false);
        Camera camera = VrCameraResolver.GetCamera();
        if (camera != null)
        {
            Vector3 forward = Vector3.ProjectOnPlane(camera.transform.forward, Vector3.up);
            if (forward.sqrMagnitude <= 0.0001f)
            {
                forward = camera.transform.forward;
            }

            selectionRoot.position =
                camera.transform.position +
                forward.normalized * Mathf.Max(0.5f, distanceFromPlayer) +
                Vector3.up * verticalOffset;
            return;
        }

        selectionRoot.position = transform.position + Vector3.forward * Mathf.Max(0.5f, distanceFromPlayer);
    }

    private void FaceTargetsToCamera()
    {
        Camera camera = VrCameraResolver.GetCamera();
        if (camera == null) return;

        if (usingSceneAnchors)
        {
            for (int i = 0; i < sceneAnchorVisuals.Count; i++)
            {
                GameObject visual = sceneAnchorVisuals[i];
                if (visual == null || !visual.activeInHierarchy) continue;

                FaceTransformToCamera(visual.transform, camera);
            }

            return;
        }

        for (int i = 0; i < selectionRoot.childCount; i++)
        {
            Transform target = selectionRoot.GetChild(i);
            if (target == null) continue;

            FaceTransformToCamera(target, camera);
        }
    }

    private static void FaceTransformToCamera(Transform target, Camera camera)
    {
        Vector3 lookDirection = target.position - camera.transform.position;
        if (lookDirection.sqrMagnitude <= 0.0001f) return;

        target.rotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
    }

    private static Material CreateTargetMaterial(Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit")
            ?? Shader.Find("Sprites/Default")
            ?? Shader.Find("Standard");

        Material material = new Material(shader)
        {
            name = "Runtime Difficulty Target Material",
            hideFlags = HideFlags.DontSave
        };

        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
        if (material.HasProperty("_Color")) material.SetColor("_Color", Color.white);
        if (material.HasProperty("_Cull")) material.SetFloat("_Cull", 0f);
        ConfigureTransparentMaterial(material);
        material.doubleSidedGI = true;
        Texture2D targetTexture = ResolveTargetBackgroundTexture();
        if (targetTexture == null)
        {
            targetTexture = CreateTargetTexture(color);
        }
        if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", targetTexture);
        if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", targetTexture);
        return material;
    }

    private Transform[] ResolveSceneAnchors(int profileCount)
    {
        if (!useSceneAnchors) return null;

        Transform[] anchors = new Transform[profileCount];
        for (int i = 0; i < profileCount; i++)
        {
            CavehuntDifficultySettings.DifficultyProfile profile = settings.GetProfile(i);
            anchors[i] = FindAnchorForProfileIndex(i, profile.DisplayName);
            if (anchors[i] == null)
            {
                return null;
            }
        }

        return anchors;
    }

    private static Transform FindAnchorForProfileIndex(int profileIndex, string displayName)
    {
        switch (profileIndex)
        {
            case 0:
                return FindAnchor("Easy");
            case 1:
                return FindAnchor("Medium") ?? FindAnchor("Normal");
            case 2:
                return FindAnchor("Hard");
            default:
                return FindAnchor(displayName);
        }
    }

    private static Transform FindAnchor(string displayName)
    {
        displayName = ResolveDisplayLabel(displayName);
        GameObject anchor = GameObject.Find(displayName);
        if (anchor != null) return anchor.transform;

        if (displayName == "Normal")
        {
            anchor = GameObject.Find("Medium");
        }
        else if (displayName == "Medium")
        {
            anchor = GameObject.Find("Normal");
        }

        return anchor != null ? anchor.transform : null;
    }

    private static string ResolveDisplayLabel(string displayName)
    {
        return displayName == "Normal" ? "Medium" : displayName;
    }

    private static Damageable EnsureDamageable(GameObject target)
    {
        target.SetActive(true);
        Damageable damageable = target.GetComponent<Damageable>();
        if (damageable == null)
        {
            damageable = target.AddComponent<Damageable>();
        }

        damageable.DeactivateOnDeath = false;
        damageable.SetMaxHealth(1f);
        return damageable;
    }

    private static CavehuntDifficultyTarget EnsureDifficultyTarget(GameObject target)
    {
        CavehuntDifficultyTarget difficultyTarget = target.GetComponent<CavehuntDifficultyTarget>();
        if (difficultyTarget == null)
        {
            difficultyTarget = target.AddComponent<CavehuntDifficultyTarget>();
        }

        return difficultyTarget;
    }

    private static float ResolveAnchorVisualScale(Transform anchor, float scaleMultiplier)
    {
        BoxCollider boxCollider = anchor.GetComponent<BoxCollider>();
        if (boxCollider == null)
        {
            return 0.85f * Mathf.Max(0.1f, scaleMultiplier);
        }

        Vector3 size = boxCollider.size;
        float smallestVisibleSide = Mathf.Min(Mathf.Max(0.1f, size.y), Mathf.Max(0.1f, size.z));
        return Mathf.Max(0.4f, smallestVisibleSide * 0.95f * Mathf.Max(0.1f, scaleMultiplier));
    }

    private void ClearSceneAnchorVisuals()
    {
        for (int i = sceneAnchorVisuals.Count - 1; i >= 0; i--)
        {
            GameObject visual = sceneAnchorVisuals[i];
            if (visual != null)
            {
                Destroy(visual);
            }
        }

        sceneAnchorVisuals.Clear();
    }

    private static Texture2D ResolveTargetBackgroundTexture()
    {
        if (cachedTargetBackground != null) return cachedTargetBackground;

        cachedTargetBackground = Resources.Load<Texture2D>(TargetBackgroundResourceName);
        return cachedTargetBackground;
    }

    private static void ConfigureTransparentMaterial(Material material)
    {
        if (material == null) return;

        if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
        if (material.HasProperty("_Blend")) material.SetFloat("_Blend", 0f);
        if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
        if (material.HasProperty("_AlphaClip")) material.SetFloat("_AlphaClip", 0f);
        if (material.HasProperty("_Mode")) material.SetFloat("_Mode", 3f);

        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.EnableKeyword("_ALPHABLEND_ON");
        material.DisableKeyword("_ALPHATEST_ON");
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
    }

    private static void ApplyCaveTextMaterial(TextMesh text)
    {
        if (text == null) return;

        MeshRenderer renderer = text.GetComponent<MeshRenderer>();
        if (renderer == null) return;

        Shader shader = Shader.Find("Cavehunt/DifficultyCaveText");
        if (shader == null) return;

        Material material = new Material(shader)
        {
            name = "Runtime Difficulty Cave Text",
            hideFlags = HideFlags.DontSave
        };

        Font font = text.font;
        if (font != null && font.material != null && font.material.mainTexture != null)
        {
            material.SetTexture("_MainTex", font.material.mainTexture);
        }

        Texture caveTexture = ResolveCaveTextTexture();
        if (caveTexture != null)
        {
            material.SetTexture("_CaveTex", caveTexture);
        }

        material.SetColor("_Color", text.color);
        if (material.HasProperty("_Tiling")) material.SetFloat("_Tiling", 2.4f);
        renderer.sharedMaterial = material;
    }

    private static Texture ResolveCaveTextTexture()
    {
        if (cachedCaveTextTexture != null) return cachedCaveTextTexture;

        Renderer[] renderers = FindObjectsByType<Renderer>(FindObjectsInactive.Include);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null) continue;

            string objectName = renderer.name.ToLowerInvariant();
            if (!objectName.Contains("cave") && !objectName.Contains("level") && !objectName.Contains("geode")) continue;

            Material[] materials = renderer.sharedMaterials;
            for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
            {
                Texture texture = ResolveMainTexture(materials[materialIndex]);
                if (texture != null)
                {
                    cachedCaveTextTexture = texture;
                    return cachedCaveTextTexture;
                }
            }
        }

        cachedCaveTextTexture = CreateProceduralCaveTexture();
        return cachedCaveTextTexture;
    }

    private static Texture ResolveMainTexture(Material material)
    {
        if (material == null) return null;
        if (material.HasProperty("_BaseMap") && material.GetTexture("_BaseMap") != null) return material.GetTexture("_BaseMap");
        if (material.HasProperty("_MainTex") && material.GetTexture("_MainTex") != null) return material.GetTexture("_MainTex");
        return null;
    }

    private static Texture2D CreateProceduralCaveTexture()
    {
        const int size = 64;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "Runtime Cave Text Texture",
            hideFlags = HideFlags.DontSave,
            wrapMode = TextureWrapMode.Repeat,
            filterMode = FilterMode.Bilinear
        };

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float n = Mathf.PerlinNoise(x * 0.12f, y * 0.18f);
                float scratch = Mathf.PerlinNoise(x * 0.55f, y * 0.08f);
                Color color = Color.Lerp(new Color(0.28f, 0.07f, 0.035f, 1f), new Color(0.72f, 0.19f, 0.09f, 1f), n);
                color = Color.Lerp(color, Color.black, scratch * 0.28f);
                texture.SetPixel(x, y, color);
            }
        }

        texture.Apply(false, true);
        return texture;
    }

    private static Texture2D CreateTargetTexture(Color accent)
    {
        const int size = 256;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "Runtime Difficulty Target Texture",
            hideFlags = HideFlags.DontSave,
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        Color dark = new Color(0.03f, 0.03f, 0.03f, 1f);
        Color white = new Color(0.92f, 0.92f, 0.88f, 1f);
        Color center = Color.Lerp(accent, Color.white, 0.15f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size - 0.5f;
                float v = (y + 0.5f) / size - 0.5f;
                float radius = Mathf.Sqrt(u * u + v * v);

                Color pixel;
                if (radius > 0.49f) pixel = dark;
                else if (radius > 0.43f) pixel = dark;
                else if (radius > 0.34f) pixel = white;
                else if (radius > 0.25f) pixel = dark;
                else if (radius > 0.15f) pixel = accent;
                else if (radius > 0.07f) pixel = white;
                else pixel = center;

                texture.SetPixel(x, y, pixel);
            }
        }

        texture.Apply(false, true);
        return texture;
    }
}

public class CavehuntDifficultyTarget : MonoBehaviour
{
    private CavehuntDifficultySelector selector;
    private Damageable damageable;
    private int profileIndex;

    public void Configure(CavehuntDifficultySelector owner, int index)
    {
        selector = owner;
        profileIndex = index;
        Subscribe();
    }

    private void OnEnable()
    {
        Subscribe();
    }

    private void OnDisable()
    {
        if (damageable != null)
        {
            damageable.Died -= SelectDifficulty;
        }
    }

    private void Subscribe()
    {
        if (damageable == null)
        {
            damageable = GetComponent<Damageable>();
        }

        if (damageable == null) return;

        damageable.Died -= SelectDifficulty;
        damageable.Died += SelectDifficulty;
    }

    private void SelectDifficulty()
    {
        if (selector != null)
        {
            selector.SelectDifficulty(profileIndex);
        }
    }
}
