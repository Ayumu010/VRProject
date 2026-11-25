
using System;
using UnityEngine;

[CreateAssetMenu(
    fileName = "Shield",
    menuName = "Game/Triggers/Behaviours/Shield"
)]
public sealed class ShieldBehaviour : TriggerBehaviour
{
    // ==================================================
    // References
    // ==================================================

    [Header("Prefabs")]
    [SerializeField]
    private GameObject shieldPrefab;

    [SerializeField]
    private CombatHandMenu menuPrefab;

    [SerializeField]
    private CombatMenuDefinition menuDefinition;

    [SerializeField]
    private GameObject wholeBodyShieldPrefab;

    // ==================================================
    // Menu
    // ==================================================

    [Header("Menu Settings")]
    [SerializeField, Min(0f)]
    private float menuHoldSeconds = 0.35f;

    [SerializeField]
    private Vector3 menuLocalPosition =
        new Vector3(0f, 0.05f, 0.15f);

    [SerializeField]
    private Vector3 menuLocalEulerAngles =
        Vector3.zero;

    // ==================================================
    // Follow
    // ==================================================

    [Header("Follow Settings")]
    [SerializeField]
    private Vector3 followPositionOffset =
        new Vector3(0f, 0f, 0.25f);

    [SerializeField]
    private Vector3 followRotationOffset =
        Vector3.zero;

    [SerializeField, Min(0f)]
    private float followSmoothTime = 0.025f;

    [SerializeField, Min(0f)]
    private float followRotationSpeed = 25f;

    // ==================================================
    // Runtime
    // ==================================================

    [NonSerialized]
    private ShieldController activeShield;

    [NonSerialized]
    private CombatHandMenu activeMenu;

    [NonSerialized]
    private bool triggerHeld;

    [NonSerialized]
    private bool menuOpenAttempted;

    [NonSerialized]
    private float triggerHoldTime;

    private static readonly string[] NormalShieldOptionIds =
    {
        "normal_follow",
        "normal_fixed",
        "concentrated_follow",
        "concentrated_fixed"
    };

    // ==================================================
    // Registration
    // ==================================================

    protected override void OnRegistered()
    {
        ResetInputState();

        activeShield = null;
        activeMenu = null;
    }

    // ==================================================
    // Selected
    // ==================================================

    protected override void OnSelected()
    {
        Debug.Log(
            $"Shield Selected : {Side}"
        );
    }

    // ==================================================
    // Input
    // ==================================================

    protected override void OnInput(
        TriggerControl control,
        bool pressed)
    {
        if (control != TriggerControl.Trigger)
            return;

        if (pressed)
        {
            HandleTriggerPressed();
        }
        else
        {
            HandleTriggerReleased();
        }
    }

    // ==================================================
    // Trigger Press
    // ==================================================

    private void HandleTriggerPressed()
    {
        if (!IsSelected || triggerHeld)
            return;

        triggerHeld = true;

        menuOpenAttempted = false;

        triggerHoldTime = 0f;
    }

    // ==================================================
    // Trigger Release
    // ==================================================

    private void HandleTriggerReleased()
    {
        if (!triggerHeld)
            return;

        triggerHeld = false;

        if (IsForceReleasingInput)
        {
            CloseMenu();

            ResetInputState();

            return;
        }

        // メニューが開いている場合は選択確定
        if (activeMenu != null)
        {
            activeMenu.UpdateSelection(
                HandAttachPoint.position
            );

            if (activeMenu.TryConfirm(
                    out CombatMenuSelection selection))
            {
                ApplyMenuSelection(
                    selection.Value
                );
            }
            else
            {
                Debug.Log(
                    $"Shield Menu: No Selection [{Side}]"
                );
            }

            CloseMenu();

            ResetInputState();

            return;
        }

        // 長押しメニューが開かなかった場合は何もしない
        if (menuOpenAttempted)
        {
            ResetInputState();
            return;
        }

        // 短押し
        ToggleNormalShield();

        ResetInputState();
    }

    // ==================================================
    // Tick
    // ==================================================

    protected override void OnTick(float deltaTime)
    {
        if (!IsSelected)
            return;

        if (triggerHeld)
        {
            triggerHoldTime += deltaTime;

            if (!menuOpenAttempted &&
                triggerHoldTime >= menuHoldSeconds)
            {
                menuOpenAttempted = true;

                OpenMenu();
            }
        }

        if (activeMenu != null &&
            HandAttachPoint != null)
        {
            activeMenu.UpdateSelection(
                HandAttachPoint.position
            );
        }
    }

    // ==================================================
    // Open Menu
    // ==================================================

    private void OpenMenu()
    {
        if (menuPrefab == null ||
            menuDefinition == null ||
            HandAttachPoint == null)
        {
            Debug.LogError(
                $"Shield [{Side}] : Menu References are missing."
            );

            return;
        }

        Transform viewAnchor =
            MenuAnchor != null
                ? MenuAnchor
                : PlayerRoot;

        if (viewAnchor == null)
            return;

        Vector3 viewRight =
            Vector3.ProjectOnPlane(
                viewAnchor.right,
                Vector3.up
            );

        if (viewRight.sqrMagnitude < 0.0001f)
        {
            viewRight = Vector3.ProjectOnPlane(
                PlayerRoot.right,
                Vector3.up
            );
        }

        if (viewRight.sqrMagnitude < 0.0001f)
            return;

        viewRight.Normalize();

        Vector3 viewForward =
            Vector3.Cross(
                viewRight,
                Vector3.up
            ).normalized;

        Quaternion rotation =
            Quaternion.LookRotation(
                viewForward,
                Vector3.up
            );

        rotation *=
            Quaternion.Euler(
                menuLocalEulerAngles
            );

        Vector3 menuRight =
            Vector3.ProjectOnPlane(
                rotation * Vector3.right,
                Vector3.up
            ).normalized;

        Vector3 menuForward =
            rotation * Vector3.forward;

        Vector3 position =
            HandAttachPoint.position +
            menuRight * menuLocalPosition.x +
            Vector3.up * menuLocalPosition.y +
            menuForward * menuLocalPosition.z;

        // ----------------------------------------------
        // Whole Body Availability
        // ----------------------------------------------

        bool wholeBodyAvailable =
            Side == TriggerSide.Main &&
            AreBothHandsShieldSelected();

        // ----------------------------------------------
        // Instantiate
        // ----------------------------------------------

        activeMenu = Instantiate(menuPrefab);

        activeMenu.transform.SetPositionAndRotation(
            position,
            rotation
        );

        string[] allowedOptions =
            wholeBodyAvailable
                ? null
                : NormalShieldOptionIds;

        if (!activeMenu.Begin(
                menuDefinition,
                HandAttachPoint.position,
                menuRight,
                allowedOptions))
        {
            CloseMenu();
            return;
        }

        Debug.Log(
            $"Shield Menu Opened : {Side} / " +
            $"WholeBodyAvailable={wholeBodyAvailable}"
        );
    }

    // ==================================================
    // Apply Selection
    // ==================================================

    private void ApplyMenuSelection(int value)
    {
        switch (value)
        {
            case 0:
                ApplyNormalConfiguration(
                    false,
                    ShieldMovementMode.Follow
                );
                break;

            case 1:
                ApplyNormalConfiguration(
                    false,
                    ShieldMovementMode.Fixed
                );
                break;

            case 2:
                ApplyNormalConfiguration(
                    true,
                    ShieldMovementMode.Follow
                );
                break;

            case 3:
                ApplyNormalConfiguration(
                    true,
                    ShieldMovementMode.Fixed
                );
                break;

            case 4:
                if (Side != TriggerSide.Main)
                {
                    Debug.LogWarning(
                        "WholeBodyShield can only be toggled from Main hand."
                    );

                    break;
                }

                ToggleWholeBodyShield();
                break;

            default:
                Debug.LogWarning(
                    $"Shield : Unknown Menu Value {value}",
                    this
                );
                break;
        }
    }

    // ==================================================
    // Normal Configuration
    // ==================================================

    private void ApplyNormalConfiguration(
        bool concentrated,
        ShieldMovementMode movementMode)
    {
        if (activeShield == null)
        {
            if (!SpawnNormalShield())
                return;
        }

        if (!activeShield.SetConcentrated(concentrated))
        {
            Debug.LogWarning(
                $"Shield [{Side}] : Concentration Change Failed."
            );

            return;
        }

        if (!activeShield.SetMovementMode(movementMode))
        {
            Debug.LogWarning(
                $"Shield [{Side}] : Movement Change Failed."
            );

            return;
        }

        Debug.Log(
            $"Shield Configuration : {movementMode} / " +
            $"Concentrated={concentrated} [{Side}]"
        );
    }

    // ==================================================
    // Toggle Normal Shield
    // ==================================================

    private void ToggleNormalShield()
    {
        // ----------------------------------------------
        // Whole Body Shield Check
        // ----------------------------------------------

        if (PlayerRoot != null)
        {
            ShieldSystemCoordinator coordinator =
                PlayerRoot.GetComponent<ShieldSystemCoordinator>();

            // 全身シールド展開中なら単押しで解除
            if (coordinator != null &&
                coordinator.IsWholeBodyShieldActive)
            {
                coordinator.DeactivateWholeBodyShield();

                Debug.Log(
                    $"WholeBodyShield : Deactivated by single trigger [{Side}]"
                );

                return;
            }
        }

        // ----------------------------------------------
        // Normal Shield Toggle
        // ----------------------------------------------

        if (activeShield != null)
        {
            DestroyActiveShield();

            return;
        }

        SpawnNormalShield();
    }

    // ==================================================
    // Spawn Normal Shield
    // ==================================================

    private bool SpawnNormalShield()
    {
        if (shieldPrefab == null ||
            PlayerRoot == null ||
            HandAttachPoint == null)
        {
            Debug.LogError(
                $"Shield [{Side}] : Spawn References are missing."
            );

            return false;
        }

        // ----------------------------------------------
        // Whole Body Shield Check
        // ----------------------------------------------

        ShieldSystemCoordinator coordinator =
            PlayerRoot.GetComponent<ShieldSystemCoordinator>();

        if (coordinator != null &&
            coordinator.IsWholeBodyShieldActive)
        {
            Debug.Log(
                $"Shield [{Side}] : Normal shield spawn blocked because WholeBodyShield is active."
            );

            return false;
        }

        GameObject instance = Instantiate(
            shieldPrefab,
            PlayerRoot,
            true
        );

        ShieldController controller =
            instance.GetComponent<ShieldController>();

        if (controller == null)
        {
            Debug.LogError(
                "Shield Prefab : ShieldController is missing.",
                instance
            );

            DestroySpawned(instance);

            return false;
        }

        if (!controller.Initialize(
                HandAttachPoint,
                PlayerRoot,
                followPositionOffset,
                followRotationOffset,
                followSmoothTime,
                followRotationSpeed))
        {
            DestroySpawned(instance);

            return false;
        }

        activeShield = controller;

        return true;
    }

    // ==================================================
    // Whole Body Shield
    // ==================================================

    private void ToggleWholeBodyShield()
    {
        if (Side != TriggerSide.Main)
        {
            Debug.LogWarning(
                "WholeBodyShield can only be controlled by Main hand."
            );

            return;
        }

        if (!AreBothHandsShieldSelected())
        {
            Debug.LogWarning(
                "WholeBodyShield requires Main and Sub Shield selection."
            );

            return;
        }

        if (wholeBodyShieldPrefab == null)
        {
            Debug.LogError(
                "Shield : WholeBodyShield Prefab is missing."
            );

            return;
        }

        ShieldSystemCoordinator coordinator =
            GetOrCreateCoordinator();

        if (coordinator == null)
            return;

        coordinator.ToggleWholeBodyShield(
            wholeBodyShieldPrefab,
            PlayerRoot
        );
    }

    // ==================================================
    // Both Hands Check
    // ==================================================

    private bool AreBothHandsShieldSelected()
    {
        if (Manager == null)
            return false;

        TriggerBehaviour main =
            Manager.GetSelectedTrigger(
                TriggerSide.Main
            );

        TriggerBehaviour sub =
            Manager.GetSelectedTrigger(
                TriggerSide.Sub
            );

        return
            main is ShieldBehaviour &&
            sub is ShieldBehaviour &&
            main.IsSelected &&
            sub.IsSelected;
    }

    // ==================================================
    // Coordinator
    // ==================================================

    private ShieldSystemCoordinator GetOrCreateCoordinator()
    {
        if (PlayerRoot == null)
            return null;

        ShieldSystemCoordinator coordinator =
            PlayerRoot.GetComponent<ShieldSystemCoordinator>();

        if (coordinator == null)
        {
            coordinator =
                PlayerRoot.gameObject.AddComponent<ShieldSystemCoordinator>();
        }

        return coordinator;
    }

    // ==================================================
    // Close Menu
    // ==================================================

    private void CloseMenu()
    {
        if (activeMenu == null)
            return;

        activeMenu.Close();

        DestroySpawned(
            activeMenu.gameObject
        );

        activeMenu = null;
    }

    // ==================================================
    // Destroy Shield
    // ==================================================

    private void DestroyActiveShield()
    {
        if (activeShield == null)
            return;

        DestroySpawned(
            activeShield.gameObject
        );

        activeShield = null;
    }

    // ==================================================
    // Reset
    // ==================================================

    private void ResetInputState()
    {
        triggerHeld = false;

        menuOpenAttempted = false;

        triggerHoldTime = 0f;
    }

    // ==================================================
    // Deselected
    // ==================================================

    protected override void OnDeselected()
    {
        ResetInputState();

        CloseMenu();

        DestroyActiveShield();

        ShieldSystemCoordinator coordinator =
            PlayerRoot != null
                ? PlayerRoot.GetComponent<ShieldSystemCoordinator>()
                : null;

        if (coordinator != null)
        {
            coordinator.DeactivateWholeBodyShield();
        }

        Debug.Log(
            $"Shield Deselected : {Side}"
        );
    }

    // ==================================================
    // Unregistered
    // ==================================================

    protected override void OnUnregistered()
    {
        ResetInputState();

        CloseMenu();

        DestroyActiveShield();

        ShieldSystemCoordinator coordinator =
            PlayerRoot != null
                ? PlayerRoot.GetComponent<ShieldSystemCoordinator>()
                : null;

        if (coordinator != null)
        {
            coordinator.DeactivateWholeBodyShield();
        }
    }

    // ==================================================
    // Validation
    // ==================================================

    private void OnValidate()
    {
        menuHoldSeconds = Mathf.Max(
            0f,
            menuHoldSeconds
        );
    }
}
