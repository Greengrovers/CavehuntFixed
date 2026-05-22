using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class IntegrateFirePickupPointCloud
{
    private const string ScenePath = "Assets/Scenes/03-Interactions.unity";
    private const string TargetName = "FirePickup";
    private const string PointCloudName = "FirePickUp PointCloud";
    private const string RelativePointCloudPath = "PointClouds/FirePickUp.ply";

    [MenuItem("Cavehunt/Integrate Fire Pickup Point Cloud")]
    public static void Integrate()
    {
        EnsurePointCloudFileExists();

        var scene = EditorSceneManager.OpenScene(ScenePath);
        GameObject firePickup = GameObject.Find(TargetName);
        if (firePickup == null)
        {
            throw new FileNotFoundException($"Could not find GameObject '{TargetName}' in {ScenePath}.");
        }

        Transform existing = firePickup.transform.Find(PointCloudName);
        GameObject pointCloud = existing != null ? existing.gameObject : new GameObject(PointCloudName);
        pointCloud.transform.SetParent(firePickup.transform, false);
        pointCloud.transform.localPosition = new Vector3(-4.5f, 0f, 0f);
        pointCloud.transform.localRotation = Quaternion.identity;
        pointCloud.transform.localScale = Vector3.one * 40.51f;

        if (pointCloud.GetComponent<MeshFilter>() == null)
        {
            pointCloud.AddComponent<MeshFilter>();
        }

        if (pointCloud.GetComponent<MeshRenderer>() == null)
        {
            pointCloud.AddComponent<MeshRenderer>();
        }

        PlyPointCloudRenderer renderer = pointCloud.GetComponent<PlyPointCloudRenderer>();
        if (renderer == null)
        {
            renderer = pointCloud.AddComponent<PlyPointCloudRenderer>();
        }

        SerializedObject serializedRenderer = new SerializedObject(renderer);
        serializedRenderer.FindProperty("relativePath").stringValue = RelativePointCloudPath;
        serializedRenderer.FindProperty("pointColor").colorValue = new Color(1f, 0.35f, 0.05f, 1f);
        serializedRenderer.FindProperty("radiusScale").floatValue = 1f;
        serializedRenderer.FindProperty("fallbackRadius").floatValue = 0.02f;
        serializedRenderer.FindProperty("convertBlenderZUpToUnityYUp").boolValue = true;
        serializedRenderer.FindProperty("centerPoints").boolValue = true;
        serializedRenderer.ApplyModifiedPropertiesWithoutUndo();

        renderer.Rebuild();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log($"Integrated {RelativePointCloudPath} under {TargetName}/{PointCloudName}.");
    }

    private static void EnsurePointCloudFileExists()
    {
        string targetPath = Path.Combine(Application.streamingAssetsPath, RelativePointCloudPath);
        if (!File.Exists(targetPath))
        {
            throw new FileNotFoundException($"Point cloud file is missing: {targetPath}");
        }
    }
}
