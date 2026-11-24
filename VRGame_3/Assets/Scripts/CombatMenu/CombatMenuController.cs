
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

[System.Serializable]
public class SlotConfirmedEvent : UnityEvent<int>
{
}

public class CombatMenuController : MonoBehaviour
{
    [Header("Input")]
    [SerializeField]
    private InputActionReference menuHoldAction;

    [Header("Tracking")]
    [SerializeField]
    private Transform handTransform;

    [SerializeField]
    private Transform headTransform;

    [Header("Menu")]
    [SerializeField]
    private Transform menuAnchor;

    [SerializeField]
    private GameObject menuCanvasRoot;

    [SerializeField]
    private float forwardOffset = 0.25f;

    [SerializeField]
    private float verticalOffset = 0.04f;

    [Header("Components")]
    [SerializeField]
    private CombatHandPositionSelector selector;

    [SerializeField]
    private CombatMenuUI menuUI;

    [Header("Events")]
    [SerializeField]
    private SlotConfirmedEvent onSlotConfirmed =
        new SlotConfirmedEvent();

    private bool isOpen;

    private void Awake()
    {
        if (menuCanvasRoot != null)
        {
            menuCanvasRoot.SetActive(false);
        }
    }

    private void OnEnable()
    {
        if (menuHoldAction == null ||
            menuHoldAction.action == null)
        {
            Debug.LogError(
                "MenuHold Action is not assigned.", this);
            return;
        }

        InputAction action = menuHoldAction.action;

        action.performed += OnMenuPressed;
        action.canceled += OnMenuReleased;

        action.Enable();
    }

    private void OnDisable()
    {
        if (menuHoldAction != null &&
            menuHoldAction.action != null)
        {
            InputAction action = menuHoldAction.action;

            action.performed -= OnMenuPressed;
            action.canceled -= OnMenuReleased;

            action.Disable();
        }

        CancelMenu();
    }

    private void LateUpdate()
    {
        if (!isOpen)
            return;

        if (handTransform == null ||
            selector == null ||
            menuUI == null)
        {
            CancelMenu();
            return;
        }

        int selected =
            selector.UpdateSelection(handTransform.position);

        menuUI.SetSelected(selected);
    }

    private void OnMenuPressed(InputAction.CallbackContext context)
    {
        OpenMenu();
    }

    private void OnMenuReleased(InputAction.CallbackContext context)
    {
        ConfirmAndClose();
    }

    private void OpenMenu()
    {
        if (isOpen)
            return;

        if (handTransform == null ||
            headTransform == null ||
            menuAnchor == null ||
            menuCanvasRoot == null ||
            selector == null ||
            menuUI == null)
        {
            Debug.LogError(
                "Combat Menu references are incomplete.", this);
            return;
        }

        // 視線方向を水平面に投影
        Vector3 forward =
            Vector3.ProjectOnPlane(
                headTransform.forward,
                Vector3.up);

        if (forward.sqrMagnitude < 0.0001f)
        {
            forward = Vector3.forward;
        }

        forward.Normalize();

        // 手の前方にメニューを配置
        Vector3 displayPosition =
            handTransform.position +
            forward * forwardOffset +
            Vector3.up * verticalOffset;

        // Canvasの横・縦軸を視線基準に合わせる
        Quaternion displayRotation =
            Quaternion.LookRotation(forward, Vector3.up);

        menuAnchor.SetPositionAndRotation(
            displayPosition,
            displayRotation);

        // 選択判定の基準位置を保存
        selector.Begin(
            handTransform.position,
            headTransform.right);

        menuCanvasRoot.SetActive(true);
        menuUI.SetSelected(-1);

        isOpen = true;
    }

    private void ConfirmAndClose()
    {
        if (!isOpen)
            return;

        int selectedSlot = selector.SelectedSlot;

        isOpen = false;

        menuCanvasRoot.SetActive(false);

        if (selectedSlot >= 0)
        {
            Debug.Log(
                $"Combat Menu Selected: Slot {selectedSlot + 1}");

            onSlotConfirmed?.Invoke(selectedSlot);
        }
        else
        {
            Debug.Log("Combat Menu: No slot selected.");
        }
    }

    private void CancelMenu()
    {
        isOpen = false;

        if (menuCanvasRoot != null)
        {
            menuCanvasRoot.SetActive(false);
        }
    }
}
