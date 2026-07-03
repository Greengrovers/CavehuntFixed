using UnityEngine;

public class PickupLocationArrow : MonoBehaviour
{
    private const string ObjectName = "Pickup Arrow Icon";
    private const float VisibilityScaleBoost = 1.45f;
    private const float VisibilityHeightBoost = 0.45f;
    private const float PulseAmount = 0.16f;
    private const float PulseSpeed = 4.5f;

    private Transform target;
    private float height = 2.4f;
    private Vector3 baseScale = Vector3.one;
    private Camera cachedCamera;

    public static PickupLocationArrow Attach(Transform pickup, Color color, float height, float scale)
    {
        if (pickup == null) return null;

        GameObject arrowObject = new GameObject(ObjectName);
        arrowObject.transform.SetParent(pickup, false);

        PickupLocationArrow arrow = arrowObject.AddComponent<PickupLocationArrow>();
        arrow.target = pickup;
        arrow.height = Mathf.Max(0.1f, height + VisibilityHeightBoost);
        arrow.baseScale = Vector3.one * Mathf.Max(0.05f, scale * VisibilityScaleBoost);
        arrowObject.transform.localPosition = Vector3.up * arrow.height;
        arrowObject.transform.localScale = arrow.baseScale;
        arrow.BuildArrow(Brighten(color));
        return arrow;
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            Destroy(gameObject);
            return;
        }

        transform.position = target.position + Vector3.up * height;
        float pulse = 1f + Mathf.Sin(Time.time * PulseSpeed) * PulseAmount;
        transform.localScale = baseScale * pulse;

        if (cachedCamera == null)
        {
            cachedCamera = Camera.main;
        }

        if (cachedCamera != null)
        {
            transform.rotation = Quaternion.LookRotation(transform.position - cachedCamera.transform.position, Vector3.up);
        }
    }

    private void BuildArrow(Color color)
    {
        MeshFilter meshFilter = gameObject.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = gameObject.AddComponent<MeshRenderer>();
        meshFilter.sharedMesh = CreateArrowMesh();
        meshRenderer.sharedMaterial = CreateMaterial(color);
        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
    }

    private static Mesh CreateArrowMesh()
    {
        Vector3[] vertices =
        {
            new Vector3(-0.18f, 0.48f, 0f),
            new Vector3(0.18f, 0.48f, 0f),
            new Vector3(0.18f, -0.08f, 0f),
            new Vector3(0.43f, -0.08f, 0f),
            new Vector3(0f, -0.56f, 0f),
            new Vector3(-0.43f, -0.08f, 0f),
            new Vector3(-0.18f, -0.08f, 0f)
        };

        int[] front =
        {
            0, 1, 2,
            0, 2, 6,
            6, 2, 3,
            6, 3, 5,
            5, 3, 4
        };

        int[] triangles = new int[front.Length * 2];
        for (int i = 0; i < front.Length; i += 3)
        {
            triangles[i] = front[i];
            triangles[i + 1] = front[i + 1];
            triangles[i + 2] = front[i + 2];

            int back = front.Length + i;
            triangles[back] = front[i + 2];
            triangles[back + 1] = front[i + 1];
            triangles[back + 2] = front[i];
        }

        Mesh mesh = new Mesh
        {
            name = "Pickup Arrow Mesh",
            vertices = vertices,
            triangles = triangles
        };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static Material CreateMaterial(Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
        {
            shader = Shader.Find("Sprites/Default");
        }

        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        Material material = new Material(shader)
        {
            color = color
        };

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }

        if (material.HasProperty("_EmissionColor"))
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", color * 0.65f);
        }

        return material;
    }

    private static Color Brighten(Color color)
    {
        Color.RGBToHSV(color, out float hue, out float saturation, out float value);
        Color brighter = Color.HSVToRGB(hue, Mathf.Clamp01(saturation * 1.1f), Mathf.Clamp01(value * 1.35f));
        brighter.a = 1f;
        return brighter;
    }
}