using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
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

    [Header("Pull Settings")]
    public float shootForceMultiplier = 35f;
    [SerializeField, Range(0f, 1f)] private float requiredPullAmountToShoot = 0.7f;
    [SerializeField] private float stringWidth = 0.008f;

    private IXRSelectInteractor pullingInteractor;
    private Rigidbody pullPointRigidbody;

    private Vector3 startPullPointLocalPosition;
    private Vector3 goalPullPointLocalPosition;
    private Vector3 startStringMiddleLocalPosition;
    private Vector3 grabOffset;
    private float pullAmount;

    private void Awake()
    {
        InitializeString();
        CachePositions();
        CachePullPointRigidbody();
    }

    private void Start()
    {
        InitializeString();
        CachePositions();
        CachePullPointRigidbody();
        UpdateStringMiddle();
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
    }

    public void EndPull(SelectExitEventArgs args)
    {
        if (pullAmount >= requiredPullAmountToShoot)
        {
            Shoot();
        }

        pullingInteractor = null;
        pullAmount = 0f;

        ResetString();
        StartCoroutine(ResetStringAfterDetach());
    }

    private void LateUpdate()
    {
        if (pullingInteractor != null)
        {
            ApplyPullFromInteractor();
        }
    }

    private void ApplyPullFromInteractor()
    {
        if (stringPullPoint == null || stringGoalPoint == null || pullingInteractor == null) return;

        Transform pullSpace = stringPullPoint.parent != null ? stringPullPoint.parent : transform;
        Vector3 pullWorldPosition = pullingInteractor.transform.position + grabOffset;
        Vector3 pullLocalPosition = pullSpace.InverseTransformPoint(pullWorldPosition);

        float startX = startPullPointLocalPosition.x;
        float goalX = goalPullPointLocalPosition.x;

        if (Mathf.Abs(goalX - startX) <= Mathf.Epsilon)
        {
            pullAmount = 0f;
            return;
        }

        pullAmount = Mathf.InverseLerp(startX, goalX, pullLocalPosition.x);
        pullAmount = Mathf.Clamp01(pullAmount);

        stringPullPoint.localPosition = new Vector3(
            Mathf.Lerp(startPullPointLocalPosition.x, goalPullPointLocalPosition.x, pullAmount),
            startPullPointLocalPosition.y,
            startPullPointLocalPosition.z
        );

        UpdateStringMiddle();

        if (arrowSpawner != null)
        {
            arrowSpawner.MoveCurrentArrowToString(stringPullPoint);
        }
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
        stringLineRenderer.startColor = Color.white;
        stringLineRenderer.endColor = Color.white;

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
                stringLineRenderer.material = new Material(shader) { color = Color.white };
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
        middlePosition.x = Mathf.Lerp(startStringMiddleLocalPosition.x, goalPullPointLocalPosition.x, pullAmount);
        stringLineRenderer.SetPosition(1, middlePosition);
    }

    private void ResetString()
    {
        if (stringPullPoint == null) return;

        stringPullPoint.localPosition = startPullPointLocalPosition;

        if (pullPointRigidbody != null)
        {
            pullPointRigidbody.isKinematic = true;
            pullPointRigidbody.useGravity = false;
            pullPointRigidbody.linearVelocity = Vector3.zero;
            pullPointRigidbody.angularVelocity = Vector3.zero;
        }

        UpdateStringMiddle();
    }

    private IEnumerator ResetStringAfterDetach()
    {
        yield return null;
        ResetString();
    }

    private void Shoot()
    {
        if (arrowSpawner == null) return;

        float force = pullAmount * shootForceMultiplier;
        arrowSpawner.ShootCurrentArrow(force);
    }
}