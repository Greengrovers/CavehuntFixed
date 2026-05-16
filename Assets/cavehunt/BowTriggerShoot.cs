using UnityEngine;
using UnityEngine.InputSystem;

public class BowTriggerShoot : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BowArrowSpawner bowArrowSpawner;

    [Header("Input Action")]
    [SerializeField] private InputActionProperty triggerAction;

    private bool wasPressedLastFrame = false;

    private void OnEnable()
    {
        triggerAction.action?.Enable();
    }

    private void OnDisable()
    {
        triggerAction.action?.Disable();
    }

    private void Update()
    {
        if (bowArrowSpawner == null || triggerAction.action == null)
            return;

        bool isPressed = triggerAction.action.IsPressed();

        if (isPressed && !wasPressedLastFrame)
        {
            Debug.Log("Trigger pressed - shooting arrow");
            bowArrowSpawner.ShootCurrentArrow();
        }

        wasPressedLastFrame = isPressed;
    }
}