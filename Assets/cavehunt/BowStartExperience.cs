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
    private GameObject respawnTemplate;
    private bool isRespawnTemplate;
    private string originalBowName;

    private static readonly List<BowStartExperience> respawnTemplates = new List<BowStartExperience>();
    private static bool creatingRespawnTemplate;

    private void Awake()
    {
        if (isRespawnTemplate || creatingRespawnTemplate) return;

        grabInteractable = GetComponent<XRGrabInteractable>();
        bowRigidbody = GetComponent<Rigidbody>();
        CacheInitialState();
        EnsureRespawnTemplate();
    }

    private void OnEnable()
    {
        if (isRespawnTemplate || creatingRespawnTemplate) return;

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
        if (isRespawnTemplate) return;

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

    private void DestroyPrompt()
    {
        if (promptObject == null) return;

        Destroy(promptObject);
        promptObject = null;
        promptText = null;
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

        CavehuntEncounterDirector director = CavehuntEncounterDirector.Resolve(false);
        if (director != null)
        {
            director.BeginRun();
            return;
        }

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
        if (isRespawnTemplate) return;

        CacheInitialState();
        EnsureRespawnTemplate();
        encounterStarted = false;
        DestroyPrompt();
        RemovePickupArrow();
        StopEnemiesUntilBowPickup();

        if (grabInteractable != null)
        {
            grabInteractable.enabled = false;
        }

        gameObject.SetActive(false);
        Destroy(gameObject);
    }

    public void ResetForRetry()
    {
        if (isRespawnTemplate) return;

        EnsureRespawnTemplate();
        HideForPlayerDeath();
        SpawnAllBowsFromTemplates();
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
            if (bows[i] != null && !bows[i].isRespawnTemplate)
            {
                bows[i].EnsureRespawnTemplate();
            }
        }

        bows = FindObjectsByType<BowStartExperience>(FindObjectsInactive.Include);
        for (int i = 0; i < bows.Length; i++)
        {
            if (bows[i] != null && !bows[i].isRespawnTemplate)
            {
                bows[i].HideForPlayerDeath();
            }
        }

        SpawnAllBowsFromTemplates();
    }

    private void CacheInitialState()
    {
        if (initialPoseCached) return;

        initialParent = transform.parent;
        initialLocalPosition = transform.localPosition;
        initialLocalRotation = transform.localRotation;
        initialLocalScale = transform.localScale;
        originalBowName = gameObject.name;

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

    private void EnsureRespawnTemplate()
    {
        if (isRespawnTemplate || creatingRespawnTemplate) return;
        if (respawnTemplate != null) return;

        CacheInitialState();

        creatingRespawnTemplate = true;
        GameObject templateObject = Instantiate(gameObject, initialParent);
        templateObject.name = $"{gameObject.name} Respawn Template";
        templateObject.SetActive(false);

        BowStartExperience template = templateObject.GetComponent<BowStartExperience>();
        if (template != null)
        {
            template.isRespawnTemplate = true;
            template.respawnTemplate = templateObject;
            template.originalBowName = string.IsNullOrEmpty(originalBowName) ? gameObject.name : originalBowName;
            template.initialParent = initialParent;
            template.initialLocalPosition = initialLocalPosition;
            template.initialLocalRotation = initialLocalRotation;
            template.initialLocalScale = initialLocalScale;
            template.initialRigidbodyIsKinematic = initialRigidbodyIsKinematic;
            template.initialRigidbodyUseGravity = initialRigidbodyUseGravity;
            template.initialPoseCached = true;
            RegisterRespawnTemplate(template);
        }

        respawnTemplate = templateObject;
        creatingRespawnTemplate = false;
    }

    private static void RegisterRespawnTemplate(BowStartExperience template)
    {
        if (template == null) return;
        if (!respawnTemplates.Contains(template))
        {
            respawnTemplates.Add(template);
        }
    }

    private static void SpawnAllBowsFromTemplates()
    {
        List<BowStartExperience> templates = CollectRespawnTemplates();
        for (int i = 0; i < templates.Count; i++)
        {
            SpawnBowFromTemplate(templates[i]);
        }
    }

    private static List<BowStartExperience> CollectRespawnTemplates()
    {
        respawnTemplates.RemoveAll(template => template == null);

        BowStartExperience[] bows = FindObjectsByType<BowStartExperience>(FindObjectsInactive.Include);
        for (int i = 0; i < bows.Length; i++)
        {
            BowStartExperience bow = bows[i];
            if (bow == null || !bow.isRespawnTemplate) continue;

            RegisterRespawnTemplate(bow);
        }

        return new List<BowStartExperience>(respawnTemplates);
    }

    private static void SpawnBowFromTemplate(BowStartExperience template)
    {
        if (template == null) return;

        GameObject spawnedObject = Instantiate(template.gameObject, template.initialParent);
        spawnedObject.name = string.IsNullOrWhiteSpace(template.originalBowName)
            ? template.gameObject.name.Replace(" Respawn Template", string.Empty)
            : template.originalBowName;

        BowStartExperience spawnedBow = spawnedObject.GetComponent<BowStartExperience>();
        if (spawnedBow != null)
        {
            spawnedBow.CopyRespawnStateFromTemplate(template);
        }

        spawnedObject.SetActive(true);
    }

    private void CopyRespawnStateFromTemplate(BowStartExperience template)
    {
        isRespawnTemplate = false;
        respawnTemplate = template != null ? template.gameObject : null;
        originalBowName = template != null ? template.originalBowName : gameObject.name;

        if (template != null)
        {
            initialParent = template.initialParent;
            initialLocalPosition = template.initialLocalPosition;
            initialLocalRotation = template.initialLocalRotation;
            initialLocalScale = template.initialLocalScale;
            initialRigidbodyIsKinematic = template.initialRigidbodyIsKinematic;
            initialRigidbodyUseGravity = template.initialRigidbodyUseGravity;
        }

        initialPoseCached = true;
        transform.SetParent(initialParent, false);
        transform.localPosition = initialLocalPosition;
        transform.localRotation = initialLocalRotation;
        transform.localScale = initialLocalScale;

        grabInteractable = GetComponent<XRGrabInteractable>();
        bowRigidbody = GetComponent<Rigidbody>();
        encounterStarted = false;
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
        CavehuntEncounterDirector director = CavehuntEncounterDirector.Resolve(false);
        if (director != null)
        {
            director.ResetForBowPickup();
            return;
        }

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
