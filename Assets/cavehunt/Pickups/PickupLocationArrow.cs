using UnityEngine;

public class PickupLocationArrow : MonoBehaviour
{
    private const string ObjectName = "Pickup Arrow Icon";

    private Transform target;
    private float height = 2.4f;
    private Camera cachedCamera;

    public static PickupLocationArrow Attach(Transform pickup, Color color, float height, float scale)
    {
        if (pickup == null) return null;

        GameObject arrowObject = new GameObject(ObjectName);
        arrowObject.transform.SetParent(pickup, false);
        arrowObject.transform.localPosition = Vector3.up * Mathf.Max(0.1f, height);
        arrowObject.transform.localScale = Vector3.one * Mathf.Max(0.05f, scale);

        PickupLocationArrow arrow = arrowObject.AddComponent<PickupLocationArrow>();
        arrow.target = pickup;
        arrow.height = Mathf.Max(0.1f, height);
        arrow.BuildArrow(color);
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
    }

    private static Mesh CreateArrowMesh()
    {
        Vector3[] vertices =
        {
            new Vector3(-0.13f, 0.42f, 0f),
            new Vector3(0.13f, 0.42f, 0f),
            new Vector3(0.13f, -0.08f, 0f),
            new Vector3(0.36f, -0.08f, 0f),
            new Vector3(0f, -0.48f, 0f),
            new Vector3(-0.36f, -0.08f, 0f),
            new Vector3(-0.13f, -0.08f, 0f)
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
        return material;
    }
}