using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;

[ExecuteAlways]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class PlyPointCloudRenderer : MonoBehaviour
{
    [SerializeField] private string relativePath = "PointClouds/FirePickUp.ply";
    [SerializeField] private Color pointColor = new Color(1f, 0.35f, 0.05f, 1f);
    [SerializeField, Min(0.001f)] private float radiusScale = 1f;
    [SerializeField, Min(0.0001f)] private float fallbackRadius = 0.02f;
    [SerializeField] private bool convertBlenderZUpToUnityYUp = true;
    [SerializeField] private bool centerPoints = true;

    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private Mesh generatedMesh;
    private Material generatedMaterial;
#if UNITY_EDITOR
    private bool rebuildQueued;
#endif

    private struct PointData
    {
        public Vector3 Position;
        public float Radius;
    }

    private struct PlyProperty
    {
        public string Name;
        public string Type;

        public PlyProperty(string name, string type)
        {
            Name = name;
            Type = type;
        }
    }

    private void OnEnable()
    {
        Rebuild();
    }

    private void OnValidate()
    {
#if UNITY_EDITOR
        if (rebuildQueued)
        {
            return;
        }

        rebuildQueued = true;
        UnityEditor.EditorApplication.delayCall += RebuildAfterValidate;
#else
        Rebuild();
#endif
    }

    private void OnDestroy()
    {
        DestroyGeneratedObjects();
    }

    [ContextMenu("Rebuild Point Cloud")]
    public void Rebuild()
    {
        Initialize();

        string path = Path.Combine(Application.streamingAssetsPath, relativePath);
        if (!File.Exists(path))
        {
            Debug.LogWarning($"Point cloud file not found: {path}", this);
            return;
        }

        List<PointData> points;
        try
        {
            points = LoadBinaryLittleEndianPly(path);
        }
        catch (Exception exception)
        {
            Debug.LogError($"Could not load point cloud '{path}': {exception.Message}", this);
            return;
        }

        BuildMesh(points);
        EnsureMaterial();
    }

#if UNITY_EDITOR
    private void RebuildAfterValidate()
    {
        rebuildQueued = false;
        if (this == null)
        {
            return;
        }

        Rebuild();
    }
#endif

    private void Initialize()
    {
        if (meshFilter == null)
        {
            meshFilter = GetComponent<MeshFilter>();
        }

        if (meshRenderer == null)
        {
            meshRenderer = GetComponent<MeshRenderer>();
        }
    }

    private List<PointData> LoadBinaryLittleEndianPly(string path)
    {
        byte[] data = File.ReadAllBytes(path);
        int headerLength = FindHeaderLength(data);
        string header = Encoding.ASCII.GetString(data, 0, headerLength);

        if (!header.Contains("format binary_little_endian 1.0"))
        {
            throw new InvalidDataException("Only binary_little_endian PLY files are supported.");
        }

        int vertexCount = 0;
        List<PlyProperty> properties = new List<PlyProperty>();
        bool readingVertexProperties = false;

        string[] lines = header.Replace("\r\n", "\n").Split('\n');
        foreach (string rawLine in lines)
        {
            string line = rawLine.Trim();
            if (line.Length == 0)
            {
                continue;
            }

            string[] parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 3 && parts[0] == "element")
            {
                readingVertexProperties = parts[1] == "vertex";
                if (readingVertexProperties)
                {
                    vertexCount = int.Parse(parts[2]);
                }

                continue;
            }

            if (readingVertexProperties && parts.Length >= 3 && parts[0] == "property")
            {
                properties.Add(new PlyProperty(parts[2], parts[1]));
            }
        }

        if (vertexCount <= 0)
        {
            throw new InvalidDataException("PLY contains no vertices.");
        }

        if (properties.Count == 0)
        {
            throw new InvalidDataException("PLY vertex properties are missing.");
        }

        List<PointData> points = new List<PointData>(vertexCount);
        using MemoryStream stream = new MemoryStream(data);
        using BinaryReader reader = new BinaryReader(stream);
        stream.Position = headerLength;

        for (int i = 0; i < vertexCount; i++)
        {
            float x = 0f;
            float y = 0f;
            float z = 0f;
            float radius = fallbackRadius;

            foreach (PlyProperty property in properties)
            {
                float value = ReadScalarAsFloat(reader, property.Type);
                switch (property.Name)
                {
                    case "x":
                        x = value;
                        break;
                    case "y":
                        y = value;
                        break;
                    case "z":
                        z = value;
                        break;
                    case "radius":
                        radius = Mathf.Max(value, fallbackRadius);
                        break;
                }
            }

            Vector3 position = convertBlenderZUpToUnityYUp
                ? new Vector3(x, z, y)
                : new Vector3(x, y, z);

            points.Add(new PointData
            {
                Position = position,
                Radius = Mathf.Max(radius * radiusScale, fallbackRadius)
            });
        }

        if (centerPoints)
        {
            Center(points);
        }

        return points;
    }

    private static int FindHeaderLength(byte[] data)
    {
        byte[] marker = Encoding.ASCII.GetBytes("end_header");
        for (int i = 0; i <= data.Length - marker.Length; i++)
        {
            bool found = true;
            for (int j = 0; j < marker.Length; j++)
            {
                if (data[i + j] != marker[j])
                {
                    found = false;
                    break;
                }
            }

            if (!found)
            {
                continue;
            }

            int end = i + marker.Length;
            if (end < data.Length && data[end] == '\r')
            {
                end++;
            }

            if (end < data.Length && data[end] == '\n')
            {
                end++;
            }

            return end;
        }

        throw new InvalidDataException("PLY header terminator not found.");
    }

    private static float ReadScalarAsFloat(BinaryReader reader, string type)
    {
        switch (type)
        {
            case "float":
            case "float32":
                return reader.ReadSingle();
            case "double":
            case "float64":
                return (float)reader.ReadDouble();
            case "uchar":
            case "uint8":
                return reader.ReadByte();
            case "char":
            case "int8":
                return reader.ReadSByte();
            case "ushort":
            case "uint16":
                return reader.ReadUInt16();
            case "short":
            case "int16":
                return reader.ReadInt16();
            case "uint":
            case "uint32":
                return reader.ReadUInt32();
            case "int":
            case "int32":
                return reader.ReadInt32();
            default:
                throw new NotSupportedException($"Unsupported PLY property type: {type}");
        }
    }

    private static void Center(List<PointData> points)
    {
        Bounds bounds = new Bounds(points[0].Position, Vector3.zero);
        for (int i = 1; i < points.Count; i++)
        {
            bounds.Encapsulate(points[i].Position);
        }

        Vector3 center = bounds.center;
        for (int i = 0; i < points.Count; i++)
        {
            PointData point = points[i];
            point.Position -= center;
            points[i] = point;
        }
    }

    private void BuildMesh(IReadOnlyList<PointData> points)
    {
        DestroyGeneratedObjects();

        generatedMesh = new Mesh
        {
            name = $"{gameObject.name} Mesh",
            hideFlags = HideFlags.DontSave,
            indexFormat = points.Count * 6 > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16
        };

        Vector3[] vertices = new Vector3[points.Count * 6];
        int[] triangles = new int[points.Count * 24];

        for (int i = 0; i < points.Count; i++)
        {
            PointData point = points[i];
            float radius = point.Radius;
            int vertexIndex = i * 6;
            int triangleIndex = i * 24;

            vertices[vertexIndex] = point.Position + Vector3.up * radius;
            vertices[vertexIndex + 1] = point.Position + Vector3.down * radius;
            vertices[vertexIndex + 2] = point.Position + Vector3.right * radius;
            vertices[vertexIndex + 3] = point.Position + Vector3.left * radius;
            vertices[vertexIndex + 4] = point.Position + Vector3.forward * radius;
            vertices[vertexIndex + 5] = point.Position + Vector3.back * radius;

            int[] localTriangles =
            {
                0, 2, 4,
                0, 4, 3,
                0, 3, 5,
                0, 5, 2,
                1, 4, 2,
                1, 3, 4,
                1, 5, 3,
                1, 2, 5
            };

            for (int t = 0; t < localTriangles.Length; t++)
            {
                triangles[triangleIndex + t] = vertexIndex + localTriangles[t];
            }
        }

        generatedMesh.vertices = vertices;
        generatedMesh.triangles = triangles;
        generatedMesh.RecalculateNormals();
        generatedMesh.RecalculateBounds();
        meshFilter.sharedMesh = generatedMesh;
    }

    private void EnsureMaterial()
    {
        if (meshRenderer == null || meshRenderer.sharedMaterial != null)
        {
            return;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
        {
            shader = Shader.Find("Sprites/Default");
        }

        if (shader == null)
        {
            return;
        }

        generatedMaterial = new Material(shader)
        {
            name = $"{gameObject.name} Material",
            color = pointColor,
            hideFlags = HideFlags.DontSave
        };

        if (generatedMaterial.HasProperty("_BaseColor"))
        {
            generatedMaterial.SetColor("_BaseColor", pointColor);
        }

        meshRenderer.sharedMaterial = generatedMaterial;
    }

    private void DestroyGeneratedObjects()
    {
        if (meshFilter != null)
        {
            meshFilter.sharedMesh = null;
        }

        if (meshRenderer != null && meshRenderer.sharedMaterial == generatedMaterial)
        {
            meshRenderer.sharedMaterial = null;
        }

        DestroyGeneratedObject(generatedMesh);
        DestroyGeneratedObject(generatedMaterial);
        generatedMesh = null;
        generatedMaterial = null;
    }

    private static void DestroyGeneratedObject(UnityEngine.Object target)
    {
        if (target == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(target);
        }
        else
        {
            DestroyImmediate(target);
        }
    }
}
