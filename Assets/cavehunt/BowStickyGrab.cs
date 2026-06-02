using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

[RequireComponent(typeof(XRGrabInteractable))]
public class BowStickyGrab : MonoBehaviour
{
    private readonly Dictionary<XRBaseInputInteractor, XRBaseInputInteractor.InputTriggerType> originalSelectTriggers = new();
    private XRGrabInteractable grabInteractable;

    private void Awake()
    {
        grabInteractable = GetComponent<XRGrabInteractable>();
    }

    private void OnEnable()
    {
        if (grabInteractable == null)
        {
            grabInteractable = GetComponent<XRGrabInteractable>();
        }

        grabInteractable.selectEntered.AddListener(OnSelectEntered);
        grabInteractable.selectExited.AddListener(OnSelectExited);
    }

    private void OnDisable()
    {
        if (grabInteractable != null)
        {
            grabInteractable.selectEntered.RemoveListener(OnSelectEntered);
            grabInteractable.selectExited.RemoveListener(OnSelectExited);
        }

        RestoreAllInteractors();
    }

    private void OnSelectEntered(SelectEnterEventArgs args)
    {
        XRBaseInputInteractor inputInteractor = args.interactorObject as XRBaseInputInteractor;
        if (inputInteractor == null) return;

        if (!originalSelectTriggers.ContainsKey(inputInteractor))
        {
            originalSelectTriggers.Add(inputInteractor, inputInteractor.selectActionTrigger);
        }

        inputInteractor.selectActionTrigger = XRBaseInputInteractor.InputTriggerType.Toggle;
    }

    private void OnSelectExited(SelectExitEventArgs args)
    {
        XRBaseInputInteractor inputInteractor = args.interactorObject as XRBaseInputInteractor;
        if (inputInteractor == null) return;

        RestoreInteractor(inputInteractor);
    }

    private void RestoreAllInteractors()
    {
        foreach (KeyValuePair<XRBaseInputInteractor, XRBaseInputInteractor.InputTriggerType> originalSelectTrigger in originalSelectTriggers)
        {
            if (originalSelectTrigger.Key != null)
            {
                originalSelectTrigger.Key.selectActionTrigger = originalSelectTrigger.Value;
            }
        }

        originalSelectTriggers.Clear();
    }

    private void RestoreInteractor(XRBaseInputInteractor inputInteractor)
    {
        if (!originalSelectTriggers.TryGetValue(inputInteractor, out XRBaseInputInteractor.InputTriggerType originalSelectTrigger)) return;

        inputInteractor.selectActionTrigger = originalSelectTrigger;
        originalSelectTriggers.Remove(inputInteractor);
    }
}
