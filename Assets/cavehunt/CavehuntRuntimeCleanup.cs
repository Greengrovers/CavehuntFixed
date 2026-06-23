using UnityEngine;

public class CavehuntRuntimeCleanup : MonoBehaviour
{
    public enum CleanupKind
    {
        Pickup,
        Trap,
        Projectile
    }

    [SerializeField] private CleanupKind kind;

    public static void Mark(GameObject target, CleanupKind cleanupKind)
    {
        if (target == null) return;

        CavehuntRuntimeCleanup marker = target.GetComponent<CavehuntRuntimeCleanup>();
        if (marker == null)
        {
            marker = target.AddComponent<CavehuntRuntimeCleanup>();
        }

        marker.kind = cleanupKind;
    }

    public static void DestroyGameplayLeftovers()
    {
        DestroyMarkedObjects();
        DestroyComponents<BatProjectile>();
        DestroyShotArrows();
    }

    private static void DestroyMarkedObjects()
    {
        CavehuntRuntimeCleanup[] markers = FindObjectsByType<CavehuntRuntimeCleanup>(FindObjectsInactive.Include);
        for (int i = 0; i < markers.Length; i++)
        {
            CavehuntRuntimeCleanup marker = markers[i];
            if (marker == null) continue;

            Destroy(marker.gameObject);
        }
    }

    private static void DestroyComponents<T>() where T : Component
    {
        T[] components = FindObjectsByType<T>(FindObjectsInactive.Include);
        for (int i = 0; i < components.Length; i++)
        {
            T component = components[i];
            if (component == null) continue;

            Destroy(component.gameObject);
        }
    }

    private static void DestroyShotArrows()
    {
        Arrow[] arrows = FindObjectsByType<Arrow>(FindObjectsInactive.Include);
        for (int i = 0; i < arrows.Length; i++)
        {
            Arrow arrow = arrows[i];
            if (arrow == null) continue;
            if (!arrow.HasBeenShot) continue;

            Destroy(arrow.gameObject);
        }
    }
}
