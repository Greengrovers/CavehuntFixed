using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using XRInputDevice = UnityEngine.XR.InputDevice;

[RequireComponent(typeof(XRGrabInteractable))]
public class BowStartExperience : MonoBehaviour
{
    [Header("Pickup Arrow")]
    [SerializeField] private bool showBowArrow = true;
    [SerializeField] private Color bowArrowColor = new Color(0.45f, 0.9f, 1f, 1f);
    [SerializeField] private float bowArrowHeight = 2.4f;
    [SerializeField] private float bowArrowScale = 0.9f;

    [Header("Intro Text")]
    [SerializeField] private string introText = "Pick up the bow and shoot enemies.";
    [SerializeField] private float promptDistance = 2.4f;
    [SerializeField] private float promptVerticalOffset = -0.15f;
    [SerializeField] private float promptCharacterSize = 0.045f;
    [SerializeField] private Color promptColor = Color.white;
    [SerializeField] private bool hidePromptOnBowPickup = true;

    [Header("Encounter")]
    [SerializeField] private bool startEnemiesOnBowPickup = true;
    [SerializeField] private BatOuterRingSpawner[] additionalEnemySpawners;

    private readonly List<XRInputDevice> inputDevices = new List<XRInputDevice>();
    private XRGrabInteractable grabInteractable;
    private PickupLocationArrow bowArrow;
    private GameObject promptObject;
    private TextMesh promptText;
    private bool encounterStarted;
    private bool yButtonWasPressed;
    private Camera cachedCamera;
    private Transform initialParent;
    private Vector3 initialLocalPosition;
    private Quaternion initialLocalRotation;
    private Vector3 initialLocalScale;
    private Rigidbody bowRigidbody;
    private bool initialRigidbodyIsKinematic;
    private bool initialRigidbodyUseGravity;
    private bool initialPoseCached;

    private void Awake()
    {
        grabInteractable = GetComponent<XRGrabInteractable>();
        bowRigidbody = GetComponent<Rigidbody>();
        CacheInitialState();
    }

    private void OnEnable()
    {
        if (grabInteractable == null)
        {
            grabInteractable = GetComponent<XRGrabInteractable>();
        }

        if (bowRigidbody == null)
        {
            bowRigidbody = GetComponent<Rigidbody>();
        }

        CacheInitialState();
        grabInteractable.selectEntered.AddListener(OnBowSelected);
    }

    private void OnDisable()
    {
        if (grabInteractable != null)
        {
            grabInteractable.selectEntered.RemoveListener(OnBowSelected);
        }
    }

    private void Start()
    {
        AttachPickupArrow();
        CreatePrompt();
    }

    private void Update()
    {
        if (promptObject != null && promptObject.activeSelf && WasDismissPressed())
        {
            DismissPrompt();
        }
    }

    private void LateUpdate()
    {
        if (promptObject == null || !promptObject.activeSelf) return;

        Camera promptCamera = ResolveCamera();
        if (promptCamera == null) return;

        Vector3 lookDirection = promptObject.transform.position - promptCamera.transform.position;
        if (lookDirection.sqrMagnitude > 0.0001f)
        {
            promptObject.transform.rotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
        }
    }

    private void OnBowSelected(SelectEnterEventArgs args)
    {
        if (hidePromptOnBowPickup)
        {
            DismissPrompt();
        }

        RemovePickupArrow();
        StartEncounter();
    }

    private void AttachPickupArrow()
    {
        if (!showBowArrow || bowArrow != null) return;

        bowArrow = PickupLocationArrow.Attach(transform, bowArrowColor, bowArrowHeight, bowArrowScale);
    }

    private void RemovePickupArrow()
    {
        if (bowArrow == null) return;

        Destroy(bowArrow.gameObject);
        bowArrow = null;
    }

    private void CreatePrompt()
    {
        if (string.IsNullOrWhiteSpace(introText)) return;

        promptObject = new GameObject("Bow Start Prompt");
        promptText = promptObject.AddComponent<TextMesh>();
        promptText.text = introText;
        promptText.anchor = TextAnchor.MiddleCenter;
        promptText.alignment = TextAlignment.Center;
        promptText.fontSize = 64;
        promptText.characterSize = Mathf.Max(0.005f, promptCharacterSize);
        promptText.color = promptColor;
        promptText.richText = false;

        PositionPrompt();
    }

    private void PositionPrompt()
    {
        Camera promptCamera = ResolveCamera();
        if (promptCamera != null)
        {
            promptObject.transform.position =
                promptCamera.transform.position +
                promptCamera.transform.forward * Mathf.Max(0.5f, promptDistance) +
                Vector3.up * promptVerticalOffset;
            return;
        }

        promptObject.transform.position = transform.position + Vector3.up * 1.8f + Vector3.forward * 1.2f;
    }

    private void DismissPrompt()
    {
        if (promptObject != null)
        {
            promptObject.SetActive(false);
        }
    }

    private bool WasDismissPressed()
    {
        bool keyboardYPressed = Keyboard.current != null && Keyboard.current.yKey.wasPressedThisFrame;
        bool xrYPressed = ReadSecondaryButton();
        bool xrYPressedThisFrame = xrYPressed && !yButtonWasPressed;
        yButtonWasPressed = xrYPressed;

        return keyboardYPressed || xrYPressedThisFrame;
    }

    private bool ReadSecondaryButton()
    {
        inputDevices.Clear();
        InputDevices.GetDevicesWithCharacteristics(
            InputDeviceCharacteristics.Left | InputDeviceCharacteristics.Controller,
            inputDevices);

        for (int i = 0; i < inputDevices.Count; i++)
        {
            if (inputDevices[i].TryGetFeatureValue(UnityEngine.XR.CommonUsages.secondaryButton, out bool pressed) && pressed)
            {
                return true;
            }
        }

        return false;
    }

    private void StartEncounter()
    {
        if (encounterStarted || !startEnemiesOnBowPickup) return;

        encounterStarted = true;
        BatEnemy[] enemies = FindObjectsByType<BatEnemy>(FindObjectsInactive.Include);
        for (int i = 0; i < enemies.Length; i++)
        {
            if (enemies[i] == null) continue;

            enemies[i].gameObject.SetActive(true);
            enemies[i].BeginEncounter();
        }

        StartAdditionalEnemySpawners();
    }

    private void StartAdditionalEnemySpawners()
    {
        BatOuterRingSpawner[] spawners = ResolveAdditionalEnemySpawners();

        for (int i = 0; i < spawners.Length; i++)
        {
            if (spawners[i] == null) continue;

            spawners[i].gameObject.SetActive(true);
            spawners[i].StartSpawning();
        }
    }

    private BatOuterRingSpawner[] ResolveAdditionalEnemySpawners()
    {
        if (additionalEnemySpawners != null && additionalEnemySpawners.Length > 0)
        {
            List<BatOuterRingSpawner> configuredSpawners = new List<BatOuterRingSpawner>();
            for (int i = 0; i < additionalEnemySpawners.Length; i++)
            {
                if (additionalEnemySpawners[i] != null)
                {
                    configuredSpawners.Add(additionalEnemySpawners[i]);
                }
            }

            if (configuredSpawners.Count > 0)
            {
                return configuredSpawners.ToArray();
            }
        }

        return FindObjectsByType<BatOuterRingSpawner>(FindObjectsInactive.Include);
    }

    public void HideForPlayerDeath()
    {
        CacheInitialState();
        encounterStarted = false;
        DismissPrompt();
        RemovePickupArrow();
        StopEnemiesUntilBowPickup();

        if (bowRigidbody != null)
        {
            bowRigidbody.linearVelocity = Vector3.zero;
            bowRigidbody.angularVelocity = Vector3.zero;
            bowRigidbody.isKinematic = true;
        }

        if (grabInteractable != null)
        {
            grabInteractable.enabled = false;
        }

        gameObject.SetActive(false);
    }

    public void ResetForRetry()
    {
        CacheInitialState();
        StopEnemiesUntilBowPickup();

        if (initialParent != null)
        {
            transform.SetParent(initialParent, false);
        }

        transform.localPosition = initialLocalPosition;
        transform.localRotation = initialLocalRotation;
        transform.localScale = initialLocalScale;

        if (bowRigidbody == null)
        {
            bowRigidbody = GetComponent<Rigidbody>();
        }

        if (bowRigidbody != null)
        {
            bowRigidbody.linearVelocity = Vector3.zero;
            bowRigidbody.angularVelocity = Vector3.zero;
            bowRigidbody.isKinematic = initialRigidbodyIsKinematic;
            bowRigidbody.useGravity = initialRigidbodyUseGravity;
        }

        encounterStarted = false;
        gameObject.SetActive(true);

        if (grabInteractable == null)
        {
            grabInteractable = GetComponent<XRGrabInteractable>();
        }

        if (grabInteractable != null)
        {
            grabInteractable.enabled = true;
        }

        AttachPickupArrow();
        ResetPrompt();
    }

    public static void HideAllBowsForPlayerDeath()
    {
        BowStartExperience[] bows = FindObjectsByType<BowStartExperience>(FindObjectsInactive.Include);
        for (int i = 0; i < bows.Length; i++)
        {
            if (bows[i] != null)
            {
                bows[i].HideForPlayerDeath();
            }
        }
    }

    public static void ResetAllBowsForRetry()
    {
        BowStartExperience[] bows = FindObjectsByType<BowStartExperience>(FindObjectsInactive.Include);
        for (int i = 0; i < bows.Length; i++)
        {
            if (bows[i] != null)
            {
                bows[i].ResetForRetry();
            }
        }
    }

    private void CacheInitialState()
    {
        if (initialPoseCached) return;

        initialParent = transform.parent;
        initialLocalPosition = transform.localPosition;
        initialLocalRotation = transform.localRotation;
        initialLocalScale = transform.localScale;

        if (bowRigidbody == null)
        {
            bowRigidbody = GetComponent<Rigidbody>();
        }

        if (bowRigidbody != null)
        {
            initialRigidbodyIsKinematic = bowRigidbody.isKinematic;
            initialRigidbodyUseGravity = bowRigidbody.useGravity;
        }

        initialPoseCached = true;
    }

    private void ResetPrompt()
    {
        if (promptObject == null)
        {
            CreatePrompt();
            return;
        }

        promptObject.SetActive(true);
        PositionPrompt();
    }

    private static void StopEnemiesUntilBowPickup()
    {
        BatEnemy[] enemies = FindObjectsByType<BatEnemy>(FindObjectsInactive.Include);
        for (int i = 0; i < enemies.Length; i++)
        {
            if (enemies[i] != null)
            {
                enemies[i].ResetForBowPickup();
            }
        }

        BatOuterRingSpawner[] spawners = FindObjectsByType<BatOuterRingSpawner>(FindObjectsInactive.Include);
        for (int i = 0; i < spawners.Length; i++)
        {
            if (spawners[i] != null)
            {
                spawners[i].ResetForBowPickup();
            }
        }
    }
    private Camera ResolveCamera()
    {
        if (cachedCamera == null)
        {
            cachedCamera = VrCameraResolver.GetCamera();
        }

        return cachedCamera;
    }
}
