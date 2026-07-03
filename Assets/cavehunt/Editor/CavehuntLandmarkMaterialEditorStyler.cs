#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class CavehuntLandmarkMaterialEditorStyler
{
    private const string RootName = "Landmarks";
    private const string WarmMaterialPath = "Assets/cavehunt/Materiials/Landmark_Cave_Warm.mat";
    private const string CoolMaterialPath = "Assets/cavehunt/Materiials/Landmark_Cave_Cool.mat";

    static CavehuntLandmarkMaterialEditorStyler()
    {
        EditorApplication.delayCall += ApplyIfLandmarksExist;
    }

    [MenuItem("Cavehunt/Apply Landmark Materials")]
    public static void ApplyIfLandmarksExist()
    {
        GameObject root = GameObject.Find(RootName);
        if (root == null) return;

        Material warmMaterial = AssetDatabase.LoadAssetAtPath<Material>(WarmMaterialPath);
        Material coolMaterial = AssetDatabase.LoadAssetAtPath<Material>(CoolMaterialPath);
        if (warmMaterial == null || coolMaterial == null) return;

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null) continue;

            string objectName = renderer.gameObject.name;
            Material material = objectName.Contains(".001") || i % 2 == 1 ? coolMaterial : warmMaterial;
            renderer.sharedMaterial = material;
            EditorUtility.SetDirty(renderer);

            EnsureBlockCollider(renderer);
        }

        EditorSceneManager.MarkSceneDirty(root.scene);
    }

    private static void EnsureBlockCollider(Renderer renderer)
    {
        GameObject target = renderer.gameObject;
        Collider existingCollider = target.GetComponent<Collider>();
        if (existingCollider != null)
        {
            existingCollider.isTrigger = false;
            EditorUtility.SetDirty(existingCollider);
            return;
        }

        BoxCollider collider = target.AddComponent<BoxCollider>();
        Bounds localBounds = renderer.localBounds;
        collider.center = localBounds.center;
        collider.size = localBounds.size;
        collider.isTrigger = false;
        EditorUtility.SetDirty(collider);
    }
}
#endif