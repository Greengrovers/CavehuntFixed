using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class BowStringPull : MonoBehaviour
{
    [Header("String Points")]
    public Transform stringTop;
    public Transform stringBottom;
    public Transform stringPullPoint;
    public Transform stringGoalPoint;

    [Header("String Visual")]
    [SerializeField] private LineRenderer stringLineRenderer;

    [Header("Arrow")]
    public BowArrowSpawner arrowSpawner;

    [Header("Bow Grab Lockout")]
    [SerializeField] private XRGrabInteractable bowGrabInteractable;
    [SerializeField] private bool disableBowGrabCollidersWhileHeld = true;

    [Header("Feedback")]
    [SerializeField] private Color defaultStringColor = Color.white;
    [SerializeField] private Color readyStringGoalColor = Color.green;

    [Header("Pull Settings")]
    public float shootForceMultiplier = 35f;
    [SerializeField] private bool useStringGoalAsTriggerPoint = true;
    [SerializeField, Range(0f, 1f)] private float requiredPullAmountToShoot = 0.7f;
    [SerializeField] private float stringWidth = 0.008f;
    [SerializeField] private bool clampPullPointLocalX = true;
    [SerializeField] private float minPullPointLocalX = 0.5f;
    [SerializeField] private float maxPullPointLocalX = 0.91f;

    private IXRSelectInteractor pullingInteractor;
    private Rigidbody pullPointRigidbody;

    private Vector3 startPullPointLocalPosition;
    private Vector3 goalPullPointLocalPosition;
    private Vector3 startStringMiddleLocalPosition;
    private Vector3 grabOffset;
    private float pullAmount;
    private bool shotArmed;
    private bool stringGoalReady;
    private bool bowGrabListenersRegistered;
    private bool bowGrabLockoutApplied;
    private Collider[] bowGrabColliders;
    private bool[] bowGrabColliderInitialStates;
    private Renderer[] stringGoalRenderers;
    private Color[] stringGoalOriginalColors;

    private static readonly int BaseColorProperty = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorProperty = Shader.PropertyToID("_Color");

    private void Awake()
    {
        InitializeString();
        CachePositions();
        CachePullPointRigidbody();
        CacheStringGoalVisuals();
        ResolveBowGrabInteractable();
        CacheBowGrabColliders();
    }

    private void OnEnable()
    {
        ResolveBowGrabInteractable();
        RegisterBowGrabCallbacks();
        Application.onBeforeRender += ClampPullPointBeforeRender;
    }

    private void OnDisable()
    {
        Application.onBeforeRender -= ClampPullPointBeforeRender;
        SetBowGrabLockout(false);
        UnregisterBowGrabCallbacks();
    }

    private void Start()
    {
        InitializeString();
        CachePositions();
        CachePullPointRigidbody();
        UpdateStringMiddle();
        CacheStringGoalVisuals();
        SetStringGoalReady(false);

        if (bowGrabInteractable != null && bowGrabInteractable.isSelected)
        {
            SetBowGrabLockout(true);
        }
    }

    private void OnValidate()
    {
        InitializeString();
    }

    public void StartPull(SelectEnterEventArgs args)
    {
        if (stringPullPoint == null) return;

        pullingInteractor = args.interactorObject;
        grabOffset = stringPullPoint.position - pullingInteractor.transform.position;
        pullAmount = 0f;
        shotArmed = false;
        ApplyCurrentPullState(true);
        SetStringGoalReady(false);
    }

    public void EndPull(SelectExitEventArgs args)
    {
        if (shotArmed)
        {
            Shoot();
        }

        pullingInteractor = null;
        pullAmount = 0f;
        shotArmed = false;
        SetStringGoalReady(false);

        ResetString();
        StartCoroutine(ResetStringAfterDetach());
    }

    private void LateUpdate()
    {
        if (pullingInteractor != null)
        {
            ApplyPullFromInteractor();
        }

        if (pullingInteractor != null || shotArmed)
        {
            ApplyCurrentPullState(true);
        }
    }

    private void FixedUpdate()
    {
        if (pullingInteractor != null || shotArmed)
        {
            ApplyPullPointClamp(true);
        }
    }

    private void ClampPullPointBeforeRender()
    {
        if (pullingInteractor != null || shotArmed)
        {
            ApplyPullPointClamp(false);
        }
    }

    private void ApplyPullFromInteractor()
    {
        if (stringPullPoint == null || stringGoalPoint == null || pullingInteractor == null) return;

        Transform pullSpace = stringPullPoint.parent != null ? stringPullPoint.parent : transform;
        Vector3 pullWorldPosition = pullingInteractor.transform.position + grabOffset;
        Vector3 pullLocalPosition = pullSpace.InverseTransformPoint(pullWorldPosition);

        float startX = ClampPullPointLocalX(startPullPointLocalPosition.x);
        float goalX = ClampPullPointLocalX(goalPullPointLocalPosition.x);
        float currentX = ClampPullPointLocalX(pullLocalPosition.x);
        if (Mathf.Abs(goalX - startX) <= Mathf.Epsilon)
        {
            pullAmount = 0f;
            return;
        }

        float rawPullAmount = Mathf.InverseLerp(startX, goalX, currentX);
        rawPullAmount = Mathf.Clamp01(rawPullAmount);

        float triggerPullAmount = GetTriggerPullAmount();
        if (rawPullAmount >= triggerPullAmount)
        {
            shotArmed = true;
        }

        pullAmount = shotArmed
            ? triggerPullAmount
            : Mathf.Min(rawPullAmount, triggerPullAmount);

        SetStringGoalReady(shotArmed);
        ApplyCurrentPullState(true);
    }

    private void ApplyCurrentPullState(bool syncRigidbody)
    {
        ApplyPullPointClamp(syncRigidbody);
        UpdateStringMiddle();

        if (arrowSpawner != null)
        {
            arrowSpawner.MoveCurrentArrowToString(stringPullPoint);
        }
    }

    private void ApplyPullPointClamp(bool syncRigidbody)
    {
        if (stringPullPoint == null) return;

        stringPullPoint.localPosition = new Vector3(
            ClampPullPointLocalX(Mathf.Lerp(startPullPointLocalPosition.x, goalPullPointLocalPosition.x, pullAmount)),
            startPullPointLocalPosition.y,
            startPullPointLocalPosition.z
        );

        if (!syncRigidbody || pullPointRigidbody == null) return;

        pullPointRigidbody.isKinematic = true;
        pullPointRigidbody.useGravity = false;
        pullPointRigidbody.linearVelocity = Vector3.zero;
        pullPointRigidbody.angularVelocity = Vector3.zero;
        pullPointRigidbody.position = stringPullPoint.position;
    }

    private float ClampPullPointLocalX(float localX)
    {
        if (!clampPullPointLocalX) return localX;

        float minX = Mathf.Min(minPullPointLocalX, maxPullPointLocalX);
        float maxX = Mathf.Max(minPullPointLocalX, maxPullPointLocalX);
        return Mathf.Clamp(localX, minX, maxX);
    }

    private void InitializeString()
    {
        if (stringLineRenderer == null)
        {
            stringLineRenderer = GetComponent<LineRenderer>();
        }

        if (stringLineRenderer == null) return;

        stringLineRenderer.enabled = true;
        stringLineRenderer.positionCount = 3;
        stringLineRenderer.useWorldSpace = false;
        stringLineRenderer.startWidth = stringWidth;
        stringLineRenderer.endWidth = stringWidth;
        stringLineRenderer.startColor = defaultStringColor;
        stringLineRenderer.endColor = defaultStringColor;

        MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
        if (meshRenderer != null)
        {
            meshRenderer.enabled = false;
        }

        if (stringLineRenderer.sharedMaterial == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }

            if (shader != null)
            {
                stringLineRenderer.material = new Material(shader) { color = defaultStringColor };
            }
        }
    }

    private void CachePositions()
    {
        if (stringPullPoint == null || stringGoalPoint == null) return;

        startPullPointLocalPosition = stringPullPoint.localPosition;
        goalPullPointLocalPosition = stringGoalPoint.localPosition;

        if (stringLineRenderer != null && stringLineRenderer.positionCount > 1)
        {
            startStringMiddleLocalPosition = stringLineRenderer.GetPosition(1);
        }
        else
        {
            startStringMiddleLocalPosition = startPullPointLocalPosition;
        }
    }

    private void CachePullPointRigidbody()
    {
        if (stringPullPoint == null) return;
        pullPointRigidbody = stringPullPoint.GetComponent<Rigidbody>();
    }

    private void UpdateStringMiddle()
    {
        if (stringLineRenderer == null) return;

        Vector3 middlePosition = startStringMiddleLocalPosition;
        middlePosition.x = ClampPullPointLocalX(Mathf.Lerp(startStringMiddleLocalPosition.x, goalPullPointLocalPosition.x, pullAmount));
        stringLineRenderer.SetPosition(1, middlePosition);
    }

    private void ResetString()
    {
        if (stringPullPoint == null) return;

        shotArmed = false;
        pullAmount = 0f;
        ApplyPullPointClamp(true);

        if (pullPointRigidbody != null)
        {
            pullPointRigidbody.isKinematic = true;
            pullPointRigidbody.useGravity = false;
            pullPointRigidbody.linearVelocity = Vector3.zero;
            pullPointRigidbody.angularVelocity = Vector3.zero;
        }

        UpdateStringMiddle();
        SetStringGoalReady(false);
    }

    private IEnumerator ResetStringAfterDetach()
    {
        yield return null;
        ResetString();
    }

    private void Shoot()
    {
        if (arrowSpawner == null) return;

        float shotPullAmount = shotArmed ? GetTriggerPullAmount() : pullAmount;
        float force = Mathf.Max(0.01f, shotPullAmount) * shootForceMultiplier;
        arrowSpawner.ShootCurrentArrow(force);
    }

    private float GetTriggerPullAmount()
    {
        return useStringGoalAsTriggerPoint
            ? 1f
            : Mathf.Clamp01(requiredPullAmountToShoot);
    }

    private void ResolveBowGrabInteractable()
    {
        if (bowGrabInteractable != null) return;

        Transform bowRoot = stringPullPoint != null && stringPullPoint.parent != null
            ? stringPullPoint.parent
            : transform.parent;

        if (bowRoot == null)
        {
            bowRoot = transform;
        }

        XRGrabInteractable[] interactables = bowRoot.GetComponentsInChildren<XRGrabInteractable>(true);
        XRGrabInteractable fallbackInteractable = null;

        for (int i = 0; i < interactables.Length; i++)
        {
            XRGrabInteractable candidate = interactables[i];
            if (candidate == null || !candidate.enabled || !candidate.gameObject.activeInHierarchy) continue;
            if (IsPullPointObject(candidate.transform)) continue;

            if (candidate.transform == bowRoot)
            {
                bowGrabInteractable = candidate;
                return;
            }

            fallbackInteractable ??= candidate;
        }

        bowGrabInteractable = fallbackInteractable;
    }

    private void RegisterBowGrabCallbacks()
    {
        if (bowGrabListenersRegistered || bowGrabInteractable == null) return;

        bowGrabInteractable.selectEntered.AddListener(OnBowGrabSelected);
        bowGrabInteractable.selectExited.AddListener(OnBowGrabExited);
        bowGrabListenersRegistered = true;
    }

    private void UnregisterBowGrabCallbacks()
    {
        if (!bowGrabListenersRegistered || bowGrabInteractable == null) return;

        bowGrabInteractable.selectEntered.RemoveListener(OnBowGrabSelected);
        bowGrabInteractable.selectExited.RemoveListener(OnBowGrabExited);
        bowGrabListenersRegistered = false;
    }

    private void OnBowGrabSelected(SelectEnterEventArgs args)
    {
        SetBowGrabLockout(true);
    }

    private void OnBowGrabExited(SelectExitEventArgs args)
    {
        SetBowGrabLockout(false);
    }

    private void CacheBowGrabColliders()
    {
        if (bowGrabColliders != null) return;

        ResolveBowGrabInteractable();
        if (bowGrabInteractable == null)
        {
            bowGrabColliders = new Collider[0];
            bowGrabColliderInitialStates = new bool[0];
            return;
        }

        List<Collider> colliders = new List<Collider>();
        for (int i = 0; i < bowGrabInteractable.colliders.Count; i++)
        {
            Collider collider = bowGrabInteractable.colliders[i];
            if (collider != null && !IsPullPointObject(collider.transform))
            {
                colliders.Add(collider);
            }
        }

        if (colliders.Count == 0)
        {
            Collider[] childColliders = bowGrabInteractable.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < childColliders.Length; i++)
            {
                Collider collider = childColliders[i];
                if (collider != null && !IsPullPointObject(collider.transform))
                {
                    colliders.Add(collider);
                }
            }
        }

        bowGrabColliders = colliders.ToArray();
        bowGrabColliderInitialStates = new bool[bowGrabColliders.Length];
        for (int i = 0; i < bowGrabColliders.Length; i++)
        {
            bowGrabColliderInitialStates[i] = bowGrabColliders[i] != null && bowGrabColliders[i].enabled;
        }
    }

    private void SetBowGrabLockout(bool locked)
    {
        if (!disableBowGrabCollidersWhileHeld) return;

        CacheBowGrabColliders();
        if (bowGrabLockoutApplied == locked) return;

        for (int i = 0; i < bowGrabColliders.Length; i++)
        {
            Collider collider = bowGrabColliders[i];
            if (collider == null) continue;

            collider.enabled = locked ? false : bowGrabColliderInitialStates[i];
        }

        bowGrabLockoutApplied = locked;
    }

    private bool IsPullPointObject(Transform candidate)
    {
        return stringPullPoint != null && candidate != null && candidate.IsChildOf(stringPullPoint);
    }

    private void CacheStringGoalVisuals()
    {
        if (stringGoalRenderers != null) return;

        if (stringGoalPoint == null)
        {
            stringGoalRenderers = new Renderer[0];
            stringGoalOriginalColors = new Color[0];
            return;
        }

        stringGoalRenderers = stringGoalPoint.GetComponentsInChildren<Renderer>(true);
        stringGoalOriginalColors = new Color[stringGoalRenderers.Length];

        for (int i = 0; i < stringGoalRenderers.Length; i++)
        {
            stringGoalOriginalColors[i] = TryGetRendererColor(stringGoalRenderers[i], out Color color)
                ? color
                : defaultStringColor;
        }
    }

    private void SetStringGoalReady(bool ready)
    {
        if (ready && !stringGoalReady)
        {
            Vector3 soundPosition = stringGoalPoint != null ? stringGoalPoint.position : transform.position;
            ProceduralGameAudio.PlayBowReady(soundPosition);
        }

        stringGoalReady = ready;
        if (stringLineRenderer != null)
        {
            Color stringColor = ready ? readyStringGoalColor : defaultStringColor;
            stringLineRenderer.startColor = stringColor;
            stringLineRenderer.endColor = stringColor;
            SetMaterialColor(stringLineRenderer.material, stringColor);
        }

        CacheStringGoalVisuals();
        for (int i = 0; i < stringGoalRenderers.Length; i++)
        {
            Renderer renderer = stringGoalRenderers[i];
            if (renderer == null) continue;

            Color color = ready ? readyStringGoalColor : stringGoalOriginalColors[i];
            SetMaterialColor(renderer.material, color);
        }
    }

    private bool TryGetRendererColor(Renderer renderer, out Color color)
    {
        color = defaultStringColor;
        if (renderer == null || renderer.sharedMaterial == null) return false;

        Material material = renderer.sharedMaterial;
        if (material.HasProperty(BaseColorProperty))
        {
            color = material.GetColor(BaseColorProperty);
            return true;
        }

        if (material.HasProperty(ColorProperty))
        {
            color = material.GetColor(ColorProperty);
            return true;
        }

        return false;
    }

    private void SetMaterialColor(Material material, Color color)
    {
        if (material == null) return;

        if (material.HasProperty(BaseColorProperty))
        {
            material.SetColor(BaseColorProperty, color);
        }

        if (material.HasProperty(ColorProperty))
        {
            material.SetColor(ColorProperty, color);
        }
    }
}
