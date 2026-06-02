using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[ExecuteAlways]
[DisallowMultipleComponent]
public class GroundFogController : MonoBehaviour
{
    private const string FogChildName = "Ground Fog Mesh";
    private const int MaxRevealCenters = 128;


    [Header("Reveal Targets")]
    [SerializeField] private Transform player;
    [SerializeField] private string playerObjectName = "XR Origin (XR Rig)";
    [SerializeField] private string geodeBoundaryPrefix = "Geode Boundary";

    [Header("Reveal Shape")]
    [SerializeField, Min(0.1f)] private float playerClearRadius = 5.5f;
    [SerializeField, Min(0.1f)] private float geodeClearRadius = 3.5f;
    [SerializeField, Min(0.01f)] private float edgeSoftness = 2.25f;

    [Header("Fog Area")]
    [SerializeField] private bool autoFitToGeodeBoundaries = true;
    [SerializeField] private Vector2 manualCenter = Vector2.zero;
    [SerializeField] private Vector2 manualSize = new Vector2(230f, 230f);
    [SerializeField, Min(0f)] private float areaPadding = 12f;
    [SerializeField, Range(12, 128)] private int gridResolution = 72;

    [Header("Ground Placement")]
    [SerializeField] private bool autoHeightFromPlayerGround = true;
    [SerializeField] private LayerMask groundLayers = ~0;
    [SerializeField] private float fallbackGroundY = 0f;
    [SerializeField] private float fogHeightOffset = 0.08f;

    [Header("Look")]
    [SerializeField] private Shader fogShader;

    [SerializeField] private Color fogColor = new Color(0.55f, 0.68f, 0.72f, 0.48f);
    [SerializeField, Range(0f, 1f)] private float baseAlpha = 0.48f;
    [SerializeField, Range(0f, 0.8f)] private float noiseStrength = 0.22f;
    [SerializeField, Min(0.001f)] private float noiseScale = 0.08f;
    [SerializeField, Min(0.02f)] private float updateInterval = 0.12f;
    [SerializeField, Min(0.25f)] private float referenceRefreshInterval = 1f;


    private readonly List<Transform> geodeBoundaries = new List<Transform>();
    private readonly Vector4[] revealCenters = new Vector4[MaxRevealCenters];
    private Mesh fogMesh;
    private MeshRenderer meshRenderer;
    private MeshFilter meshFilter;
    private Material fogMaterial;
    private Vector2 lastCenter;
    private Vector2 lastSize;
        private float nextUpdateTime;
    private float nextReferenceRefreshTime;


    private void OnEnable()
    {
        EnsureReferences(true);
        EnsureFogObjects();
        RebuildMeshIfNeeded(true);
        UpdateFogAlpha(true);
    }

    private void OnDisable()
    {
        if (fogMesh != null)
        {
            if (Application.isPlaying)
            {
                Destroy(fogMesh);
            }
            else
            {
                DestroyImmediate(fogMesh);
            }
        }
    }

    private void Update()
    {
        EnsureReferences(false);
        RebuildMeshIfNeeded(false);

        if (Time.realtimeSinceStartup < nextUpdateTime)
        {
            return;
        }

        nextUpdateTime = Time.realtimeSinceStartup + updateInterval;
        UpdateFogAlpha(false);
    }

    private void OnValidate()
    {
        ClampSettings();

        if (!isActiveAndEnabled)
        {
            return;
        }

        EnsureReferences(true);
        EnsureFogObjects();
        RebuildMeshIfNeeded(true);
        UpdateFogAlpha(true);
    }

    private void ClampSettings()
    {
        gridResolution = Mathf.Clamp(gridResolution, 12, 128);
        playerClearRadius = Mathf.Max(0.1f, playerClearRadius);
        geodeClearRadius = Mathf.Max(0.1f, geodeClearRadius);
        baseAlpha = Mathf.Clamp01(baseAlpha);
        edgeSoftness = Mathf.Max(0.01f, edgeSoftness);
    }

    private void EnsureReferences(bool force)
    {
        if (!force && player != null && geodeBoundaries.Count > 0 && Time.realtimeSinceStartup < nextReferenceRefreshTime)
        {
            return;
        }

        nextReferenceRefreshTime = Time.realtimeSinceStartup + referenceRefreshInterval;
        if (player == null)
        {
            GameObject taggedPlayer = GameObject.FindGameObjectWithTag("Player");
            if (taggedPlayer != null)
            {
                player = taggedPlayer.transform;
            }
        }

        if (player == null && !string.IsNullOrWhiteSpace(playerObjectName))
        {
            GameObject namedPlayer = GameObject.Find(playerObjectName);
            if (namedPlayer != null)
            {
                player = namedPlayer.transform;
            }
        }

        geodeBoundaries.Clear();
        Transform[] transforms = FindObjectsByType<Transform>(FindObjectsInactive.Include);
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform candidate = transforms[i];
            if (candidate != null && candidate.name.StartsWith(geodeBoundaryPrefix, System.StringComparison.OrdinalIgnoreCase))
            {
                geodeBoundaries.Add(candidate);
            }
        }
    }

private void EnsureFogObjects()
    {
        Transform child = transform.Find(FogChildName);
        if (child == null)
        {
            GameObject childObject = new GameObject(FogChildName);
            childObject.transform.SetParent(transform, false);
            child = childObject.transform;
        }

        child.localPosition = Vector3.zero;
        child.localRotation = Quaternion.identity;
        child.localScale = Vector3.one;


        meshFilter = child.GetComponent<MeshFilter>();
        if (meshFilter == null)
        {
            meshFilter = child.gameObject.AddComponent<MeshFilter>();
        }

        meshRenderer = child.GetComponent<MeshRenderer>();
        if (meshRenderer == null)
        {
            meshRenderer = child.gameObject.AddComponent<MeshRenderer>();
        }

        Shader shader = ResolveFogShader();
        if (fogMaterial == null || fogMaterial.shader != shader)
        {
            fogMaterial = new Material(shader);
            fogMaterial.name = "Runtime Ground Fog Material";
            fogMaterial.hideFlags = HideFlags.DontSave;
            ConfigureMaterial(fogMaterial);
        }

        meshRenderer.sharedMaterial = fogMaterial;
        meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
    }

    private void ConfigureMaterial(Material material)
    {
        material.renderQueue = (int)RenderQueue.Transparent;
        SetColorIfPresent(material, "_BaseColor", Color.white);
        SetColorIfPresent(material, "_Color", Color.white);
        SetFloatIfPresent(material, "_Surface", 1f);
        SetFloatIfPresent(material, "_Blend", 0f);
        SetFloatIfPresent(material, "_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        SetFloatIfPresent(material, "_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        SetFloatIfPresent(material, "_ZWrite", 0f);
        SetFloatIfPresent(material, "_Cull", (float)UnityEngine.Rendering.CullMode.Off);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
    }

    private static void SetColorIfPresent(Material material, string property, Color value)
    {
        if (material.HasProperty(property))
        {
            material.SetColor(property, value);
        }
    }

    private static void SetFloatIfPresent(Material material, string property, float value)
    {
        if (material.HasProperty(property))
        {
            material.SetFloat(property, value);
        }
    }

private void RebuildMeshIfNeeded(bool force)
    {
        Vector2 center;
        Vector2 size;
        CalculateArea(out center, out size);

        if (!force && fogMesh != null && lastCenter == center && lastSize == size)
        {
            return;
        }

        lastCenter = center;
        lastSize = size;

        float groundY = ResolveGroundY();
        Vector3[] worldCorners =
        {
            new Vector3(center.x - size.x * 0.5f, groundY + fogHeightOffset, center.y - size.y * 0.5f),
            new Vector3(center.x - size.x * 0.5f, groundY + fogHeightOffset, center.y + size.y * 0.5f),
            new Vector3(center.x + size.x * 0.5f, groundY + fogHeightOffset, center.y - size.y * 0.5f),
            new Vector3(center.x + size.x * 0.5f, groundY + fogHeightOffset, center.y + size.y * 0.5f)
        };

        Vector3[] vertices = new Vector3[worldCorners.Length];
        for (int i = 0; i < worldCorners.Length; i++)
        {
            vertices[i] = meshFilter.transform.InverseTransformPoint(worldCorners[i]);
        }

        Vector2[] uvs =
        {
            new Vector2(0f, 0f),
            new Vector2(0f, 1f),
            new Vector2(1f, 0f),
            new Vector2(1f, 1f)
        };

        int[] triangles =
        {
            0, 1, 2,
            2, 1, 3
        };

        if (fogMesh == null)
        {
            fogMesh = new Mesh();
            fogMesh.name = "Ground Fog Reveal Mesh";
            fogMesh.hideFlags = HideFlags.DontSave;
        }
        else
        {
            fogMesh.Clear();
        }

        fogMesh.indexFormat = IndexFormat.UInt16;
        fogMesh.vertices = vertices;
        fogMesh.uv = uvs;
        fogMesh.triangles = triangles;
        fogMesh.RecalculateBounds();
        fogMesh.RecalculateNormals();
        meshFilter.sharedMesh = fogMesh;
    }

    private void CalculateArea(out Vector2 center, out Vector2 size)
    {
        if (!autoFitToGeodeBoundaries || geodeBoundaries.Count == 0)
        {
            center = manualCenter;
            size = manualSize;
            return;
        }

        bool hasBounds = false;
        Vector2 minimum = Vector2.zero;
        Vector2 maximum = Vector2.zero;

        for (int i = 0; i < geodeBoundaries.Count; i++)
        {
            Transform boundary = geodeBoundaries[i];
            if (boundary == null)
            {
                continue;
            }

            Vector2 point = new Vector2(boundary.position.x, boundary.position.z);
            if (!hasBounds)
            {
                minimum = point;
                maximum = point;
                hasBounds = true;
            }
            else
            {
                minimum = Vector2.Min(minimum, point);
                maximum = Vector2.Max(maximum, point);
            }
        }

        Transform revealTarget = ResolvePlayerRevealTransform();
        if (revealTarget != null)
        {
            Vector2 point = new Vector2(revealTarget.position.x, revealTarget.position.z);
            if (!hasBounds)
            {
                minimum = point;
                maximum = point;
                hasBounds = true;
            }
            else
            {
                minimum = Vector2.Min(minimum, point);
                maximum = Vector2.Max(maximum, point);
            }
        }

        if (!hasBounds)
        {
            center = manualCenter;
            size = manualSize;
            return;
        }

        minimum -= Vector2.one * areaPadding;
        maximum += Vector2.one * areaPadding;
        center = (minimum + maximum) * 0.5f;
        size = new Vector2(Mathf.Max(1f, maximum.x - minimum.x), Mathf.Max(1f, maximum.y - minimum.y));
    }

    private float ResolveGroundY()
    {
        if (!autoHeightFromPlayerGround)
        {
            return fallbackGroundY;
        }

        Transform revealTarget = ResolvePlayerRevealTransform();
        Vector3 origin = revealTarget != null ? revealTarget.position : new Vector3(manualCenter.x, fallbackGroundY + 1f, manualCenter.y);
        Vector3 rayOrigin = origin + Vector3.up * 0.25f;
        Transform ignoredRoot = player != null ? player.root : (revealTarget != null ? revealTarget.root : null);
        RaycastHit[] hits = Physics.RaycastAll(rayOrigin, Vector3.down, 8f, groundLayers, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit hit = hits[i];
            if (ignoredRoot != null && hit.transform != null && hit.transform.root == ignoredRoot)
            {
                continue;
            }

            return hit.point.y;
        }

        return fallbackGroundY;
    }

private void UpdateFogAlpha(bool force)
    {
        if (fogMaterial == null)
        {
            return;
        }

        ClampSettings();

        int revealCount = 0;
        Transform revealTarget = ResolvePlayerRevealTransform();
        if (revealTarget != null && revealCount < MaxRevealCenters)
        {
            Vector3 position = revealTarget.position;
            fogMaterial.SetVector("_PlayerRevealCenter", new Vector4(position.x, 0f, position.z, 0f));
            fogMaterial.SetFloat("_PlayerRevealRadius", playerClearRadius);
        }
        else
        {
            fogMaterial.SetFloat("_PlayerRevealRadius", 0f);
        }

        for (int i = 0; i < geodeBoundaries.Count && revealCount < MaxRevealCenters; i++)
        {
            Transform boundary = geodeBoundaries[i];
            if (boundary == null || !boundary.gameObject.activeInHierarchy)
            {
                continue;
            }

            Vector3 position = boundary.position;
            revealCenters[revealCount++] = new Vector4(position.x, 0f, position.z, geodeClearRadius);
        }

        Color materialFogColor = fogColor;
        materialFogColor.a *= Mathf.Clamp01(baseAlpha);
        fogMaterial.SetColor("_FogColor", materialFogColor);
        fogMaterial.SetFloat("_EdgeSoftness", edgeSoftness);
        fogMaterial.SetFloat("_NoiseStrength", noiseStrength);
        fogMaterial.SetFloat("_NoiseScale", noiseScale);
        fogMaterial.SetInt("_RevealCount", revealCount);
        fogMaterial.SetVectorArray("_RevealCenters", revealCenters);
    }

    private float CalculateReveal(Vector3 worldPosition)
    {
        float reveal = 0f;

        Transform revealTarget = ResolvePlayerRevealTransform();
        if (revealTarget != null)
        {
            reveal = Mathf.Max(reveal, RevealAmount(worldPosition, revealTarget.position, playerClearRadius));
        }

        for (int i = 0; i < geodeBoundaries.Count; i++)
        {
            Transform boundary = geodeBoundaries[i];
            if (boundary == null || !boundary.gameObject.activeInHierarchy)
            {
                continue;
            }

            reveal = Mathf.Max(reveal, RevealAmount(worldPosition, boundary.position, geodeClearRadius));
            if (reveal >= 1f)
            {
                return 1f;
            }
        }

        return reveal;
    }

    private float RevealAmount(Vector3 worldPosition, Vector3 revealPosition, float radius)
    {
        Vector2 a = new Vector2(worldPosition.x, worldPosition.z);
        Vector2 b = new Vector2(revealPosition.x, revealPosition.z);
        float distance = Vector2.Distance(a, b);
        return 1f - Mathf.SmoothStep(radius, radius + edgeSoftness, distance);
    }

    private Transform ResolvePlayerRevealTransform()
    {
        if (player != null)
        {
            Camera playerCamera = player.GetComponentInChildren<Camera>(true);
            if (playerCamera != null)
            {
                return playerCamera.transform;
            }

            return player;
        }

        Camera mainCamera = Camera.main;
        return mainCamera != null ? mainCamera.transform : null;
    }

    private float HashNoise(float x, float z)
    {
        float value = Mathf.Sin((x * 12.9898f + z * 78.233f) * noiseScale) * 43758.5453f;
        return Mathf.Lerp(0.65f, 1.15f, Mathf.Repeat(value, 1f));
    }


private Shader ResolveFogShader()
    {
        Shader shader = fogShader != null ? fogShader : Shader.Find("Cavehunt/GroundFogVertexColor");
        if (shader == null)
        {
            shader = Shader.Find("Sprites/Default");
        }
        if (shader == null)
        {
            shader = Shader.Find("Unlit/Transparent");
        }

        return shader;
    }
}
