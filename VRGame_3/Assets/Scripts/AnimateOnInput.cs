
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public enum AnimationInputType
{
    Float,
    Vector2,
    Bool,
    Trigger
}

[System.Serializable]
public class AnimationInput
{
    public AnimationInputType inputType;
    public string animationPropertyName;
    public string secondaryPropertyName;
    public InputActionProperty action;
}

public class AnimateOnInput : MonoBehaviour
{
    public List<AnimationInput> animationInputs = new List<AnimationInput>();
    public Animator animator;

    void Update()
    {
        if (animator == null)
            return;

        foreach (var item in animationInputs)
        {
            if (item.action.action == null ||
                !item.action.action.enabled)
                continue;

            switch (item.inputType)
            {
                case AnimationInputType.Float:
                    animator.SetFloat(
                        item.animationPropertyName,
                        item.action.action.ReadValue<float>());
                    break;

                case AnimationInputType.Vector2:
                    Vector2 value =
                        item.action.action.ReadValue<Vector2>();

                    animator.SetFloat(
                        item.animationPropertyName, value.x);

                    if (!string.IsNullOrEmpty(item.secondaryPropertyName))
                    {
                        animator.SetFloat(
                            item.secondaryPropertyName, value.y);
                    }
                    break;

                case AnimationInputType.Bool:
                    animator.SetBool(
                        item.animationPropertyName,
                        item.action.action.IsPressed());
                    break;

                case AnimationInputType.Trigger:
                    if (item.action.action.WasPressedThisFrame())
                    {
                        animator.SetTrigger(
                            item.animationPropertyName);
                    }
                    break;
            }
        }
    }
}
