
using UnityEngine;

[DisallowMultipleComponent]
public sealed class TriggerSlotMenuSession : MonoBehaviour
{
    [Header("References")]
    [SerializeField]
    private TriggerSlotManager slotManager;

    [SerializeField]
    private CombatHandMenu menuPrefab;

    [SerializeField]
    private CombatMenuDefinition menuDefinition;

    [Header("Hand References")]
    [SerializeField]
    private Transform mainHandAttachPoint;

    [SerializeField]
    private Transform subHandAttachPoint;

    [Header("View Reference")]
    [SerializeField]
    private Transform menuAnchor;

    [Header("Menu Position")]
    [SerializeField]
    private Vector3 menuLocalPosition =
        new Vector3(0f, 0.05f, 0.15f);

    [SerializeField]
    private Vector3 menuLocalEulerAngles =
        Vector3.zero;

    private CombatHandMenu mainMenu;

    private CombatHandMenu subMenu;

    // ==================================================
    // Public Access
    // ==================================================

    public bool IsOpen(TriggerSide side)
    {
        CombatHandMenu menu = GetMenu(side);

        return menu != null && menu.IsOpen;
    }

    // ==================================================
    // Open
    // ==================================================

    public bool Open(TriggerSide side)
    {
        if (slotManager == null ||
            menuPrefab == null ||
            menuDefinition == null)
        {
            Debug.LogError(
                "TriggerSlotMenuSession: Required references are missing.",
                this
            );

            return false;
        }

        Close(side);

        Transform hand = GetHand(side);

        Transform view = menuAnchor != null
            ? menuAnchor
            : transform;

        if (hand == null)
        {
            Debug.LogError(
                $"TriggerSlotMenuSession: {side} Hand is missing.",
                this
            );

            return false;
        }

        // ----------------------------------------------
        // View Direction
        // ----------------------------------------------

        Vector3 viewRight =
            Vector3.ProjectOnPlane(
                view.right,
                Vector3.up
            );

        if (viewRight.sqrMagnitude < 0.0001f)
            return false;

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

        rotation *= Quaternion.Euler(
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
            hand.position +
            menuRight * menuLocalPosition.x +
            Vector3.up * menuLocalPosition.y +
            menuForward * menuLocalPosition.z;

        // ----------------------------------------------
        // Instantiate
        // ----------------------------------------------

        CombatHandMenu menu = Instantiate(
            menuPrefab
        );

        menu.transform.SetPositionAndRotation(
            position,
            rotation
        );

        if (!menu.Begin(
                menuDefinition,
                hand.position,
                menuRight))
        {
            Destroy(menu.gameObject);
            return false;
        }

        SetMenu(side, menu);

        Debug.Log(
            $"Trigger Slot Menu Opened : {side}",
            this
        );

        return true;
    }

    // ==================================================
    // Update
    // ==================================================

    public void UpdateSelection(TriggerSide side)
    {
        CombatHandMenu menu = GetMenu(side);

        Transform hand = GetHand(side);

        if (menu == null || hand == null)
            return;

        menu.UpdateSelection(
            hand.position
        );
    }

    // ==================================================
    // Confirm
    // ==================================================

    public bool Confirm(TriggerSide side)
    {
        CombatHandMenu menu = GetMenu(side);

        if (menu == null || slotManager == null)
            return false;

        if (!menu.TryConfirm(
                out CombatMenuSelection selection))
        {
            Debug.Log(
                $"Trigger Slot Menu: No Selection [{side}]",
                this
            );

            return false;
        }

        // Menu DefinitionのValueをSlot Indexとして使用
        int slotIndex = selection.Value;

        if (slotIndex < 0 || slotIndex >= 4)
        {
            Debug.LogError(
                $"Trigger Slot Menu: Invalid Slot Index {slotIndex}",
                this
            );

            return false;
        }

        // 実際のトリガー選択変更
        bool success = slotManager.SelectSlot(
            side,
            slotIndex
        );

        if (!success)
        {
            Debug.LogError(
                $"Trigger Slot Change Failed : {side} / {slotIndex}",
                this
            );

            return false;
        }

        Debug.Log(
            $"Trigger Slot Changed : {side} / Slot {slotIndex + 1}",
            this
        );

        Close(side);

        return true;
    }

    // ==================================================
    // Cancel
    // ==================================================

    public void Cancel(TriggerSide side)
    {
        Close(side);
    }

    // ==================================================
    // Close
    // ==================================================

    public void Close(TriggerSide side)
    {
        CombatHandMenu menu = GetMenu(side);

        if (menu == null)
            return;

        menu.Close();

        if (side == TriggerSide.Main)
            mainMenu = null;
        else
            subMenu = null;

        Destroy(menu.gameObject);
    }

    public void CloseAll()
    {
        Close(TriggerSide.Main);
        Close(TriggerSide.Sub);
    }

    // ==================================================
    // References
    // ==================================================

    private Transform GetHand(TriggerSide side)
    {
        return side == TriggerSide.Main
            ? mainHandAttachPoint
            : subHandAttachPoint;
    }

    private CombatHandMenu GetMenu(TriggerSide side)
    {
        return side == TriggerSide.Main
            ? mainMenu
            : subMenu;
    }

    private void SetMenu(
        TriggerSide side,
        CombatHandMenu menu)
    {
        if (side == TriggerSide.Main)
            mainMenu = menu;
        else
            subMenu = menu;
    }

    // ==================================================
    // Cleanup
    // ==================================================

    private void OnDisable()
    {
        CloseAll();
    }
}
