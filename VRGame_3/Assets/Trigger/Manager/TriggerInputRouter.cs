using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class TriggerInputRouter : MonoBehaviour
{
    [Serializable]
    private class HandInputActions
    {
        public InputActionProperty trigger;
        public InputActionProperty grip;
        public InputActionProperty primary;
        public InputActionProperty secondary;
    }

    [Header("Trigger Manager")]
    [SerializeField]
    private TriggerSlotManager slotManager;

    [Header("MAIN - Right Controller")]
    [SerializeField]
    private HandInputActions mainActions = new HandInputActions();

    [Header("SUB - Left Controller")]
    [SerializeField]
    private HandInputActions subActions = new HandInputActions();

    [Header("Input Settings")]
    [SerializeField]
    private bool enableActionsIfNeeded = true;

    private readonly List<ActionBinding> bindings =
        new List<ActionBinding>();

    private class ActionBinding
    {
        public InputAction Action;

        public Action<InputAction.CallbackContext> Performed;
        public Action<InputAction.CallbackContext> Canceled;

        public bool EnabledByRouter;
    }

    private void OnEnable()
    {
        BindHand(mainActions, TriggerSide.Main);
        BindHand(subActions, TriggerSide.Sub);
    }

    private void OnDisable()
    {
        if (slotManager != null)
        {
            slotManager.ReleaseInputs(TriggerSide.Main);
            slotManager.ReleaseInputs(TriggerSide.Sub);
        }

        UnbindAll();
    }

    // ==================================================
    // ì¸óÕìoò^
    // ==================================================

    private void BindHand(
        HandInputActions actions,
        TriggerSide side)
    {
        BindAction(
            actions.trigger,
            side,
            TriggerControl.Trigger
        );

        BindAction(
            actions.grip,
            side,
            TriggerControl.Grip
        );

        BindAction(
            actions.primary,
            side,
            TriggerControl.Primary
        );

        BindAction(
            actions.secondary,
            side,
            TriggerControl.Secondary
        );
    }

    private void BindAction(
        InputActionProperty property,
        TriggerSide side,
        TriggerControl control)
    {
        InputAction action = property.action;

        if (action == null)
            return;

        Action<InputAction.CallbackContext> performed =
            context =>
            {
                if (slotManager != null)
                {
                    slotManager.SendInput(
                        side,
                        control,
                        true
                    );
                }
            };

        Action<InputAction.CallbackContext> canceled =
            context =>
            {
                if (slotManager != null)
                {
                    slotManager.SendInput(
                        side,
                        control,
                        false
                    );
                }
            };

        action.performed += performed;
        action.canceled += canceled;

        bool enabledByRouter = false;

        if (enableActionsIfNeeded && !action.enabled)
        {
            action.Enable();
            enabledByRouter = true;
        }

        bindings.Add(new ActionBinding
        {
            Action = action,
            Performed = performed,
            Canceled = canceled,
            EnabledByRouter = enabledByRouter
        });
    }

    // ==================================================
    // ì¸óÕâèú
    // ==================================================

    private void UnbindAll()
    {
        foreach (ActionBinding binding in bindings)
        {
            binding.Action.performed -= binding.Performed;
            binding.Action.canceled -= binding.Canceled;

            if (binding.EnabledByRouter &&
                binding.Action.enabled)
            {
                binding.Action.Disable();
            }
        }

        bindings.Clear();
    }
}