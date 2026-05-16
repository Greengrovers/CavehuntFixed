using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class BowShootController : MonoBehaviour
{
    [Header("References")]
    public XRGrabInteractable grabInteractable;
    public BowArrowSpawner bowArrowSpawner;

    private void Start()
    {
        grabInteractable.activated.AddListener(OnTrigger);
    }

    private void OnTrigger(ActivateEventArgs args)
    {
        if (bowArrowSpawner != null)
        {
            bowArrowSpawner.ShootCurrentArrow();
        }
    }
}