using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public class Gun : MonoBehaviour
{
    // Public variables (set these in the Inspector)
    public GameObject bulletPrefab;          // Prefab from Project window
    public Transform spawnPoint;             // Where the bullet spawns
    public UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable interactable;  // XR interactable
    public float firepower = 20f;            // Bullet speed

    void Start()
    {
        // Add trigger listener
        interactable.activated.AddListener(OnTrigger);
    }

    void OnTrigger(ActivateEventArgs args)
    {
        // Spawn bullet
        GameObject bullet = Instantiate(
            bulletPrefab,
            spawnPoint.position,
            spawnPoint.rotation
        );

        // Add force to bullet
        Rigidbody body = bullet.GetComponent<Rigidbody>();

        if (body != null)
        {
            Vector3 force = spawnPoint.forward * firepower;
            body.AddForce(force, ForceMode.Impulse);
        }
    }
}
