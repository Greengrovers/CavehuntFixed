using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class CavehuntPickupSetup
{
    private const string ScenePath = "Assets/Scenes/03-Interactions.unity";
    private const string PickupFolder = "Assets/Resources/Pickups";
    private const string FireVisualPath = "Assets/cavehunt/Art/Pickups/Animation trys/FirePickUp PointCloud.prefab";
    private const string AirVisualPath = "Assets/cavehunt/Art/Pickups/Animation trys/Air_PickUP_Pointcloud.prefab";
    private const string GrenadeVisualPath = "Assets/cavehunt/Art/Pickups/Animation trys/FirePickup.fbx_Grenade.fbx";

    public static void Setup()
    {
        EnsureFolder("Assets/Resources");
        EnsureFolder(PickupFolder);

        GameObject firePrefab = CreatePickupPrefab(
            "FireAmmoPickup",
            AmmoType.Fire,
            5,
            FireVisualPath,
            new Color(1f, 0.22f, 0.05f, 1f),
            false
        );

        GameObject grenadePrefab = CreatePickupPrefab(
            "GrenadeAmmoPickup",
            AmmoType.Grenade,
            3,
            GrenadeVisualPath,
            new Color(0.95f, 0.85f, 0.22f, 1f),
            true
        );

        GameObject airPrefab = CreatePickupPrefab(
            "AirAmmoPickup",
            AmmoType.Air,
            5,
            AirVisualPath,
            new Color(0.1f, 0.85f, 0.9f, 1f),
            false
        );

        ConfigureScene(firePrefab, grenadePrefab, airPrefab);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private static GameObject CreatePickupPrefab(string prefabName, AmmoType ammoType, int ammoAmount, string visualPath, Color color, bool useFrameAnimation)
    {
        GameObject visualAsset = AssetDatabase.LoadAssetAtPath<GameObject>(visualPath);
        if (visualAsset == null)
        {
            throw new FileNotFoundException($"Pickup visual missing: {visualPath}");
        }

        string prefabPath = $"{PickupFolder}/{prefabName}.prefab";
        GameObject root = new GameObject(prefabName);

        SphereCollider collider = root.AddComponent<SphereCollider>();
        collider.isTrigger = true;
        collider.radius = 0.55f;

        Rigidbody rigidbody = root.AddComponent<Rigidbody>();
        rigidbody.isKinematic = true;
        rigidbody.useGravity = false;

        AmmoPickup pickup = root.AddComponent<AmmoPickup>();
        SerializedObject pickupObject = new SerializedObject(pickup);
        pickupObject.FindProperty("ammoType").enumValueIndex = (int)ammoType;
        pickupObject.FindProperty("ammoAmount").intValue = ammoAmount;
        pickupObject.FindProperty("switchToAmmoOnPickup").boolValue = true;
        pickupObject.ApplyModifiedPropertiesWithoutUndo();

        root.AddComponent<PickupDropAnimation>();

        GameObject visual = (GameObject)PrefabUtility.InstantiatePrefab(visualAsset);
        visual.name = "Visual";
        visual.transform.SetParent(root.transform, false);
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.identity;

        PlyPointCloudRenderer pointCloud = visual.GetComponent<PlyPointCloudRenderer>();
        if (pointCloud != null)
        {
            SerializedObject pointCloudObject = new SerializedObject(pointCloud);
            pointCloudObject.FindProperty("pointColor").colorValue = color;
            pointCloudObject.FindProperty("centerPoints").boolValue = true;
            pointCloudObject.FindProperty("convertBlenderZUpToUnityYUp").boolValue = false;
            pointCloudObject.ApplyModifiedPropertiesWithoutUndo();
            pointCloud.Rebuild();
        }

        if (useFrameAnimation)
        {
            visual.AddComponent<ModelFrameAnimator>();
            CenterAndScaleVisual(visual.transform, 0.7f);
        }

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        Object.DestroyImmediate(root);
        return prefab;
    }

    private static void ConfigureScene(params GameObject[] pickupPrefabs)
    {
        var scene = EditorSceneManager.OpenScene(ScenePath);

        BatEnemy[] bats = Object.FindObjectsByType<BatEnemy>(FindObjectsInactive.Include);
        for (int i = 0; i < bats.Length; i++)
        {
            EnemyPickupDropper dropper = bats[i].GetComponent<EnemyPickupDropper>();
            if (dropper == null)
            {
                dropper = bats[i].gameObject.AddComponent<EnemyPickupDropper>();
            }

            SerializedObject dropperObject = new SerializedObject(dropper);
            dropperObject.FindProperty("dropChance").floatValue = 0.25f;
            SerializedProperty prefabs = dropperObject.FindProperty("pickupPrefabs");
            prefabs.arraySize = pickupPrefabs.Length;
            for (int prefabIndex = 0; prefabIndex < pickupPrefabs.Length; prefabIndex++)
            {
                prefabs.GetArrayElementAtIndex(prefabIndex).objectReferenceValue = pickupPrefabs[prefabIndex];
            }

            dropperObject.ApplyModifiedPropertiesWithoutUndo();
        }

        PlayerHealth playerHealth = Object.FindAnyObjectByType<PlayerHealth>();
        if (playerHealth != null && playerHealth.GetComponent<PlayerAmmoInventory>() == null)
        {
            playerHealth.gameObject.AddComponent<PlayerAmmoInventory>();
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static void CenterAndScaleVisual(Transform visual, float targetDiameter)
    {
        Renderer[] renderers = visual.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return;

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        Vector3 targetCenter = visual.parent != null ? visual.parent.position : visual.position;
        visual.position += targetCenter - bounds.center;

        float maxSize = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
        if (maxSize > 0.0001f)
        {
            float scale = targetDiameter / maxSize;
            visual.localScale *= scale;
        }
    }

    private static void EnsureFolder(string folderPath)
    {
        if (AssetDatabase.IsValidFolder(folderPath)) return;

        string parent = Path.GetDirectoryName(folderPath)?.Replace("\\", "/");
        string folderName = Path.GetFileName(folderPath);
        if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
        {
            EnsureFolder(parent);
        }

        AssetDatabase.CreateFolder(parent, folderName);
    }
}
