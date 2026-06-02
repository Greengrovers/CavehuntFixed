using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Movement;

[ExecuteAlways]
[DisallowMultipleComponent]
public class PlayerMovementSpeedSlider : MonoBehaviour
{
    [SerializeField] private ContinuousMoveProvider moveProvider;
    [SerializeField, Range(0.5f, 15f)] private float movementSpeed = 7.5f;

    public float MovementSpeed
    {
        get => movementSpeed;
        set
        {
            movementSpeed = Mathf.Clamp(value, 0.5f, 15f);
            ApplySpeed();
        }
    }

    private void Reset()
    {
        ResolveProvider();
        ApplySpeed();
    }

    private void OnEnable()
    {
        ResolveProvider();
        ApplySpeed();
    }

    private void OnValidate()
    {
        movementSpeed = Mathf.Clamp(movementSpeed, 0.5f, 15f);
        ResolveProvider();
        ApplySpeed();
    }

    private void ResolveProvider()
    {
        if (moveProvider == null)
        {
            moveProvider = GetComponent<ContinuousMoveProvider>();
        }
    }

    private void ApplySpeed()
    {
        if (moveProvider != null)
        {
            moveProvider.moveSpeed = movementSpeed;
        }
    }
}
