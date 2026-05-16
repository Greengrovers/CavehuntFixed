using UnityEngine;

public class Arrow : MonoBehaviour
{
    [SerializeField] private float lifeTime = 10f;

    private Rigidbody rb;
    private bool hasBeenShot = false;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();

        rb.isKinematic = true;
        rb.useGravity = false;
    }

    public void Shoot(Vector3 direction, float force)
    {
        if (hasBeenShot) return;

        hasBeenShot = true;

        transform.parent = null;

        rb.isKinematic = false;
        rb.useGravity = true;

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        rb.AddForce(direction.normalized * force, ForceMode.Impulse);

        Destroy(gameObject, lifeTime);
    }
}