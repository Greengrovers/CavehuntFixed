using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using XRInputDevice = UnityEngine.XR.InputDevice;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DisallowMultipleComponent]
public class GitHubGroundFogToggle : MonoBehaviour
{
    [SerializeField] private KeyCode keyboardToggleKey = KeyCode.Y;
    [SerializeField] private KeyCode controllerToggleKey = KeyCode.JoystickButton3;
    [SerializeField] private bool listenForLeftControllerY = true;
    [SerializeField] private bool startsVisible = true;

    private readonly List<XRInputDevice> leftControllerDevices = new List<XRInputDevice>();
    private Renderer[] renderers;
    private ParticleSystem[] particleSystems;
    private bool isVisible;
    private bool previousLeftSecondaryPressed;

    private void Awake()
    {
        CacheTargets();
        SetVisible(startsVisible);
    }

    private void Reset()
    {
        CacheTargets();
    }

    private void Update()
    {
        if (WasTogglePressed())
        {
            SetVisible(!isVisible);
        }
    }

    public void SetVisible(bool visible)
    {
        isVisible = visible;
        CacheTargets();

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
            {
                renderers[i].enabled = isVisible;
            }
        }

        for (int i = 0; i < particleSystems.Length; i++)
        {
            if (particleSystems[i] == null) continue;

            if (isVisible)
            {
                particleSystems[i].Play(true);
            }
            else
            {
                particleSystems[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }
    }

    private void CacheTargets()
    {
        renderers = GetComponentsInChildren<Renderer>(true);
        particleSystems = GetComponentsInChildren<ParticleSystem>(true);
    }

    private bool WasTogglePressed()
    {
        bool pressed = Input.GetKeyDown(keyboardToggleKey) || Input.GetKeyDown(controllerToggleKey);

#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            pressed |= Keyboard.current.yKey.wasPressedThisFrame;
        }

        if (Gamepad.current != null)
        {
            pressed |= Gamepad.current.buttonNorth.wasPressedThisFrame;
        }
#endif

        return pressed || WasLeftControllerYPressed();
    }

    private bool WasLeftControllerYPressed()
    {
        if (!listenForLeftControllerY) return false;

        leftControllerDevices.Clear();
        InputDevices.GetDevicesWithCharacteristics(
            InputDeviceCharacteristics.Left | InputDeviceCharacteristics.Controller,
            leftControllerDevices);

        bool currentPressed = false;
        for (int i = 0; i < leftControllerDevices.Count; i++)
        {
            if (leftControllerDevices[i].TryGetFeatureValue(UnityEngine.XR.CommonUsages.secondaryButton, out bool pressed) && pressed)
            {
                currentPressed = true;
                break;
            }
        }

        bool pressedThisFrame = currentPressed && !previousLeftSecondaryPressed;
        previousLeftSecondaryPressed = currentPressed;
        return pressedThisFrame;
    }
}
