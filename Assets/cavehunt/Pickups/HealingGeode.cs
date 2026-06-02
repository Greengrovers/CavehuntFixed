using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class HealingGeode : MonoBehaviour
{
    [SerializeField, Min(0f)] private float healAmount;
    [SerializeField, Min(0.05f)] private float healDuration;
    [SerializeField, Min(0.1f)] private float activationRadius;
    [SerializeField] private bool consumeAtFullHealth;
    [SerializeField, Min(0f)] private float destroyDelay;
    [SerializeField, Min(0.01f)] private float disappearScaleDuration;

    private Transform playerTarget;
    private Coroutine channelRoutine;
    private bool consumed;

    private void Awake()
    {
        EnsureTriggerCollider();
        playerTarget = ResolvePlayerTarget();
    }

    private void Update()
    {
        if (consumed) return;

        if (playerTarget == null)
        {
            playerTarget = ResolvePlayerTarget();
        }

        PlayerHealth playerHealth = ResolvePlayerHealth(playerTarget);
        if (playerHealth == null)
        {
            StopChannel();
            return;
        }

        Vector3 delta = playerHealth.transform.position - transform.position;
        delta.y = 0f;
        bool playerInside = delta.sqrMagnitude <= activationRadius * activationRadius;

        if (playerInside)
        {
            if (channelRoutine == null)
            {
                channelRoutine = StartCoroutine(HealAfterHoldRoutine(playerHealth));
            }
        }
        else
        {
            StopChannel();
        }
    }

    private void OnValidate()
    {
        healAmount = Mathf.Max(0f, healAmount);
        healDuration = Mathf.Max(0.05f, healDuration);
        activationRadius = Mathf.Max(0.1f, activationRadius);
        destroyDelay = Mathf.Max(0f, destroyDelay);
        disappearScaleDuration = Mathf.Max(0.01f, disappearScaleDuration);

        SphereCollider sphereCollider = GetComponent<SphereCollider>();
        if (sphereCollider != null)
        {
            sphereCollider.isTrigger = true;
            sphereCollider.radius = activationRadius;
            sphereCollider.center = Vector3.zero;
        }
    }

    private IEnumerator HealAfterHoldRoutine(PlayerHealth playerHealth)
    {
        float elapsed = 0f;
        float duration = Mathf.Max(0.05f, healDuration);

        while (elapsed < duration)
        {
            if (playerHealth == null || !IsPlayerInside(playerHealth))
            {
                channelRoutine = null;
                yield break;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (consumed || playerHealth == null)
        {
            yield break;
        }

        float healed = playerHealth.Heal(healAmount);
        if (healed > 0f || consumeAtFullHealth)
        {
            consumed = true;
            StartCoroutine(DisappearRoutine());
        }

        channelRoutine = null;
    }

    private IEnumerator DisappearRoutine()
    {
        Collider[] colliders = GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            colliders[i].enabled = false;
        }

        Vector3 startScale = transform.localScale;
        float duration = Mathf.Max(0.01f, disappearScaleDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            transform.localScale = Vector3.LerpUnclamped(startScale, Vector3.zero, Mathf.SmoothStep(0f, 1f, t));
            yield return null;
        }

        Destroy(gameObject, Mathf.Max(0f, destroyDelay));
    }

    private void StopChannel()
    {
        if (channelRoutine == null) return;

        StopCoroutine(channelRoutine);
        channelRoutine = null;
    }

    private bool IsPlayerInside(PlayerHealth playerHealth)
    {
        Vector3 delta = playerHealth.transform.position - transform.position;
        delta.y = 0f;
        return delta.sqrMagnitude <= activationRadius * activationRadius;
    }

    private void EnsureTriggerCollider()
    {
        SphereCollider sphereCollider = GetComponent<SphereCollider>();
        if (sphereCollider == null)
        {
            sphereCollider = gameObject.AddComponent<SphereCollider>();
        }

        sphereCollider.isTrigger = true;
        sphereCollider.radius = Mathf.Max(0.1f, activationRadius);
        sphereCollider.center = Vector3.zero;
    }

    private static PlayerHealth ResolvePlayerHealth(Transform target)
    {
        if (target != null)
        {
            PlayerHealth playerHealth = target.GetComponentInParent<PlayerHealth>();
            if (playerHealth != null) return playerHealth;
        }

        return FindAnyObjectByType<PlayerHealth>();
    }

    private static Transform ResolvePlayerTarget()
    {
        PlayerHealth playerHealth = FindAnyObjectByType<PlayerHealth>();
        if (playerHealth != null) return playerHealth.transform;

        GameObject xrOrigin = GameObject.Find("XR Origin (XR Rig)") ?? GameObject.Find("XR Origin (VR)");
        if (xrOrigin != null) return xrOrigin.transform;

        Camera camera = Camera.main;
        return camera != null ? camera.transform : null;
    }
}
