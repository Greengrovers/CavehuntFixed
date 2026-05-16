using UnityEngine;
using UnityEngine.InputSystem;

public class HandsAnimator : MonoBehaviour
{
    public InputActionReference triggerAction;
    public InputActionReference gripAction;
    public Animator animator;

    void Update()
    {
        animator.SetFloat("Trigger", triggerAction.action.ReadValue<float>());
        animator.SetFloat("Grip", gripAction.action.ReadValue<float>());
    }
}
