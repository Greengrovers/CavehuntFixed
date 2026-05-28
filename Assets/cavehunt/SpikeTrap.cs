using System.Collections;
using UnityEngine;

public class SpikeTrap : MonoBehaviour
{
    [SerializeField] private float damage = 1f;
    [SerializeField] private float triggerDamageDelay = 0.18f;
    [SerializeField] private float triggerCooldown = 1.25f;
    [SerializeField] private bool destroyAfterTrigger = true;
    [SerializeField] private float destroyDelay = 0.15f;
    [SerializeField] private float damageRadius = 1.75f;
    [SerializeField] private float triggerRadius = 1.45f;
    [SerializeField] private string triggerParameterName = "Trigger";
    [SerializeField] private RuntimeAnimatorController trapAnimatorController;
    [SerializeField] private string animationStateName = "Armature|ArmatureAction";
    [SerializeField] private Transform fallbackAnimatedRoot;
    [SerializeField] private float fallbackRiseHeight = 0.45f;
    [SerializeField] private float fallbackRiseDuration = 0.16f;
    [SerializeField] private float fallbackResetDuration = 0.35f;

    private Animator animator;
    private Animation legacyAnimation;
    private Transform playerTarget;
    private Vector3 fallbackStartLocalPosition;
    private Coroutine fallbackAnimationRoutine;
    private bool controlsAnimatorPlayback;
    private bool isTriggered;

    private void Awake()
    {
        ResolveFallbackAnimatedRoot();
        animator = GetComponentInChildren<Animator>(true);
        legacyAnimation = GetComponentInChildren<Animation>(true);
        EnsureAnimatorController();
        playerTarget = ResolvePlayerTarget();
        EnsureTriggerCollider();
    }

    private void Update()
    {
        if (isTriggered) return;

        if (playerTarget == null)
        {
            playerTarget = ResolvePlayerTarget();
        }

        if (playerTarget == null) return;

        Vector3 delta = playerTarget.position - transform.position;
        delta.y = 0f;
        if (delta.sqrMagnitude <= triggerRadius * triggerRadius)
        {
            PlayerHealth playerHealth = playerTarget.GetComponentInParent<PlayerHealth>() ?? FindAnyObjectByType<PlayerHealth>();
            if (playerHealth != null)
            {
                StartCoroutine(TriggerRoutine(playerHealth));
            }
        }
    }

    public void Configure(float newDamage)
    {
        damage = Mathf.Max(0f, newDamage);
        playerTarget = ResolvePlayerTarget();
        ResolveFallbackAnimatedRoot();
        EnsureAnimatorController();
        EnsureTriggerCollider();
    }

    private void OnTriggerEnter(Collider other)
    {
        PlayerHealth playerHealth = ResolvePlayerHealth(other);
        if (playerHealth == null || isTriggered) return;

        StartCoroutine(TriggerRoutine(playerHealth));
    }

    private IEnumerator TriggerRoutine(PlayerHealth initialPlayerHealth)
    {
        isTriggered = true;
        ProceduralGameAudio.PlayTrap(transform.position);
        PlayTrapAnimation();

        yield return new WaitForSeconds(Mathf.Max(0f, triggerDamageDelay));

        PlayerHealth playerHealth = ResolvePlayerInDamageRange(initialPlayerHealth);
        if (playerHealth != null)
        {
            playerHealth.TakeDamage(damage);
        }

        yield return new WaitForSeconds(Mathf.Max(0.05f, triggerCooldown));

        if (destroyAfterTrigger)
        {
            Destroy(gameObject, Mathf.Max(0f, destroyDelay));
            yield break;
        }

        ResetAnimatorBool();
        isTriggered = false;
    }

    private void PlayTrapAnimation()
    {
        bool animationPlayed = false;

        if (animator != null)
        {
            animator.enabled = true;
            bool usedParameter = false;

            for (int i = 0; i < animator.parameterCount; i++)
            {
                AnimatorControllerParameter parameter = animator.parameters[i];
                if (parameter.name != triggerParameterName) continue;

                if (parameter.type == AnimatorControllerParameterType.Trigger)
                {
                    animator.SetTrigger(triggerParameterName);
                    usedParameter = true;
                    animationPlayed = true;
                }
                else if (parameter.type == AnimatorControllerParameterType.Bool)
                {
                    animator.SetBool(triggerParameterName, true);
                    usedParameter = true;
                    animationPlayed = true;
                }

                break;
            }

            if (!usedParameter && animator.runtimeAnimatorController != null)
            {
                if (!string.IsNullOrEmpty(animationStateName))
                {
                    animator.Play(animationStateName, 0, 0f);
                }
                else
                {
                    animator.Play(0, 0, 0f);
                }

                animationPlayed = true;
            }
        }

        if (legacyAnimation != null)
        {
            legacyAnimation.Play();
            animationPlayed = true;
        }

        if (!animationPlayed)
        {
            PlayFallbackAnimation();
        }
    }

    private void ResetAnimatorBool()
    {
        if (animator == null) return;

        for (int i = 0; i < animator.parameterCount; i++)
        {
            AnimatorControllerParameter parameter = animator.parameters[i];
            if (parameter.name == triggerParameterName && parameter.type == AnimatorControllerParameterType.Bool)
            {
                animator.SetBool(triggerParameterName, false);
                break;
            }
        }

        if (controlsAnimatorPlayback)
        {
            animator.enabled = false;
        }
    }

    private void PlayFallbackAnimation()
    {
        ResolveFallbackAnimatedRoot();
        if (fallbackAnimatedRoot == null) return;

        if (fallbackAnimationRoutine != null)
        {
            StopCoroutine(fallbackAnimationRoutine);
        }

        fallbackAnimationRoutine = StartCoroutine(FallbackAnimationRoutine());
    }

    private IEnumerator FallbackAnimationRoutine()
    {
        fallbackAnimatedRoot.localPosition = fallbackStartLocalPosition;
        Vector3 raisedPosition = fallbackStartLocalPosition + Vector3.up * Mathf.Max(0f, fallbackRiseHeight);

        yield return MoveFallbackRoot(fallbackStartLocalPosition, raisedPosition, Mathf.Max(0.01f, fallbackRiseDuration));
        yield return MoveFallbackRoot(raisedPosition, fallbackStartLocalPosition, Mathf.Max(0.01f, fallbackResetDuration));

        fallbackAnimationRoutine = null;
    }

    private IEnumerator MoveFallbackRoot(Vector3 from, Vector3 to, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = Mathf.SmoothStep(0f, 1f, t);
            fallbackAnimatedRoot.localPosition = Vector3.LerpUnclamped(from, to, eased);
            yield return null;
        }

        fallbackAnimatedRoot.localPosition = to;
    }

    private void ResolveFallbackAnimatedRoot()
    {
        if (fallbackAnimatedRoot == null && transform.childCount > 0)
        {
            fallbackAnimatedRoot = transform.GetChild(0);
        }

        if (fallbackAnimatedRoot != null)
        {
            fallbackStartLocalPosition = fallbackAnimatedRoot.localPosition;
        }
    }

    private void EnsureAnimatorController()
    {
        if (trapAnimatorController == null) return;

        Transform animatorRoot = fallbackAnimatedRoot != null ? fallbackAnimatedRoot : transform;
        if (animator == null)
        {
            animator = animatorRoot.gameObject.AddComponent<Animator>();
            controlsAnimatorPlayback = true;
        }

        if (animator.runtimeAnimatorController == null)
        {
            animator.runtimeAnimatorController = trapAnimatorController;
            controlsAnimatorPlayback = true;
        }

        if (controlsAnimatorPlayback)
        {
            animator.enabled = false;
        }
    }

    private PlayerHealth ResolvePlayerInDamageRange(PlayerHealth fallback)
    {
        Collider[] colliders = Physics.OverlapSphere(transform.position, Mathf.Max(0.1f, damageRadius), ~0, QueryTriggerInteraction.Collide);
        for (int i = 0; i < colliders.Length; i++)
        {
            PlayerHealth playerHealth = ResolvePlayerHealth(colliders[i]);
            if (playerHealth != null)
            {
                return playerHealth;
            }
        }

        if (fallback == null) return null;

        Vector3 delta = fallback.transform.position - transform.position;
        delta.y = 0f;
        return delta.magnitude <= damageRadius + 0.75f ? fallback : null;
    }

    private static PlayerHealth ResolvePlayerHealth(Collider other)
    {
        if (other == null) return null;

        PlayerHealth playerHealth = other.GetComponentInParent<PlayerHealth>();
        if (playerHealth != null) return playerHealth;

        if (!IsLikelyPlayerCollider(other)) return null;

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

    private static bool IsLikelyPlayerCollider(Collider other)
    {
        if (other.GetComponentInParent<CharacterController>() != null) return true;
        if (other.GetComponentInParent<Camera>() != null) return true;

        string rootName = other.transform.root != null
            ? other.transform.root.name.ToLowerInvariant()
            : string.Empty;

        return rootName.Contains("xr")
            || rootName.Contains("player")
            || rootName.Contains("rig")
            || rootName.Contains("hand");
    }

    private void EnsureTriggerCollider()
    {
        Collider[] colliders = GetComponents<Collider>();
        if (colliders.Length == 0)
        {
            BoxCollider boxCollider = gameObject.AddComponent<BoxCollider>();
            ConfigureBoxCollider(boxCollider);
            colliders = new Collider[] { boxCollider };
        }

        for (int i = 0; i < colliders.Length; i++)
        {
            colliders[i].isTrigger = true;
        }
    }

    private void ConfigureBoxCollider(BoxCollider boxCollider)
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            boxCollider.center = new Vector3(0f, 0.45f, 0f);
            boxCollider.size = new Vector3(2f, 0.9f, 2f);
            return;
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        Vector3 localCenter = transform.InverseTransformPoint(bounds.center);
        Vector3 localSize = transform.InverseTransformVector(bounds.size);
        localSize = new Vector3(Mathf.Abs(localSize.x), Mathf.Abs(localSize.y), Mathf.Abs(localSize.z));

        boxCollider.center = new Vector3(localCenter.x, Mathf.Max(0.35f, localCenter.y), localCenter.z);
        boxCollider.size = new Vector3(
            Mathf.Max(1.75f, localSize.x),
            Mathf.Max(0.9f, localSize.y + 0.35f),
            Mathf.Max(1.75f, localSize.z)
        );
    }
}
