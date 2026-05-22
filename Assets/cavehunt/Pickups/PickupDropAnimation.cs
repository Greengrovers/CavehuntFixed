using UnityEngine;

public class PickupDropAnimation : MonoBehaviour
{
    [SerializeField] private float dropHeight = 0.65f;
    [SerializeField] private float dropDuration = 0.35f;
    [SerializeField] private float hoverHeight = 0.12f;
    [SerializeField] private float hoverFrequency = 2.5f;
    [SerializeField] private float rotationSpeed = 90f;
    [SerializeField] private float pulseAmount = 0.06f;

    private Vector3 targetPosition;
    private Vector3 startPosition;
    private Vector3 baseScale;
    private float elapsed;
    private bool dropping;

    private void OnEnable()
    {
        baseScale = transform.localScale;
        PlayDrop();
    }

    public void PlayDrop()
    {
        targetPosition = transform.position;
        startPosition = targetPosition + Vector3.up * Mathf.Max(0f, dropHeight);
        elapsed = 0f;
        dropping = true;
        transform.position = startPosition;
    }

    private void Update()
    {
        elapsed += Time.deltaTime;

        if (dropping)
        {
            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, dropDuration));
            float arc = Mathf.Sin(t * Mathf.PI) * 0.12f;
            transform.position = Vector3.Lerp(startPosition, targetPosition, t) + Vector3.up * arc;
            dropping = t < 1f;
        }
        else
        {
            float bob = Mathf.Sin(Time.time * hoverFrequency) * hoverHeight;
            transform.position = targetPosition + Vector3.up * bob;
        }

        transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.World);

        float pulse = 1f + Mathf.Sin(Time.time * hoverFrequency * 1.4f) * pulseAmount;
        transform.localScale = baseScale * pulse;
    }
}
