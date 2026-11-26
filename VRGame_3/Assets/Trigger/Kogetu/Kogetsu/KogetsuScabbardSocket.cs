using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

[DisallowMultipleComponent]
public sealed class KogetsuScabbardSocket : MonoBehaviour
{
    [Header("XR Socket")]
    [SerializeField]
    private XRSocketInteractor socketInteractor;

    [SerializeField]
    private Transform socketAttachTransform;

    [Header("Sheathe Alignment")]
    [SerializeField, Min(0.001f)]
    private float maximumAttachDistance = 0.12f;

    [SerializeField, Range(0f, 180f)]
    private float maximumAttachAngle = 25f;

    private KogetsuWeapon weapon;

    public Transform SocketAttachTransform => socketAttachTransform;
    public bool HasValidReferences => socketInteractor != null && socketAttachTransform != null;

    private void Awake()
    {
        if (socketInteractor == null)
            socketInteractor = GetComponentInChildren<XRSocketInteractor>(true);
    }

    public void Initialize(KogetsuWeapon targetWeapon)
    {
        weapon = targetWeapon;

        if (socketInteractor == null || socketAttachTransform == null || weapon == null)
        {
            Debug.LogError("KogetsuScabbardSocket: Socket, SocketAttach, or Weapon is missing.", this);
            return;
        }

        socketInteractor.attachTransform = socketAttachTransform;
        socketInteractor.hoverSocketSnapping = true;
        socketInteractor.showInteractableHoverMeshes = false;
        socketInteractor.socketActive = true;
    }

    /// <summary>
    /// 生成直後の武器を、このSocketに明示的に選択させる。
    /// 初期配置時のTrigger検出タイミングに依存せず納刀させる。
    /// </summary>
    public bool TrySelectSpawnedWeapon(KogetsuWeapon targetWeapon)
    {
        if (socketInteractor == null ||
            targetWeapon == null ||
            targetWeapon.GrabInteractable == null)
        {
            Debug.LogError(
                "KogetsuScabbardSocket: Socket or Weapon reference is missing.",
                this);
            return false;
        }

        UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable grab = targetWeapon.GrabInteractable;
        XRInteractionManager manager = socketInteractor.interactionManager;

        if (manager == null)
        {
            Debug.LogError(
                "KogetsuScabbardSocket: XRInteractionManager is missing.",
                this);
            return false;
        }

        if (!socketInteractor.isActiveAndEnabled ||
            !grab.isActiveAndEnabled)
        {
            Debug.LogError(
                "KogetsuScabbardSocket: Socket or Weapon is inactive.",
                this);
            return false;
        }

        if (grab.interactionManager != manager)
        {
            Debug.LogError(
                "KogetsuScabbardSocket: Socket and Weapon use different InteractionManagers.",
                this);
            return false;
        }

        socketInteractor.socketActive = true;

        // すでに選択されていなければ、通常の選択処理を実行する。
        if (!socketInteractor.IsSelecting(grab))
        {
            manager.SelectEnterUnconditionally(socketInteractor, grab);
        }

        // 選択が成立したか確認する。
        if (!socketInteractor.IsSelecting(grab))
        {
            Debug.LogError(
                "KogetsuScabbardSocket: Failed to select spawned weapon.",
                this);
            return false;
        }

        return true;
    }
    private void LateUpdate()
    {
        if (socketInteractor == null || weapon == null)
            return;

        if (weapon.State == KogetsuWeaponState.Sheathed)
        {
            socketInteractor.socketActive = true;
            return;
        }

        if (weapon.State != KogetsuWeaponState.Dropped)
        {
            socketInteractor.socketActive = false;
            return;
        }

        Transform gripAttach = weapon.GripAttachTransform;
        if (gripAttach == null || socketAttachTransform == null)
        {
            socketInteractor.socketActive = false;
            return;
        }

        float distance = Vector3.Distance(gripAttach.position, socketAttachTransform.position);
        float angle = Quaternion.Angle(gripAttach.rotation, socketAttachTransform.rotation);

        // 距離と姿勢の両方が合った場合だけSocketを有効にする。
        // 近くを通過しただけでは納刀しない。
        socketInteractor.socketActive =
            distance <= maximumAttachDistance &&
            angle <= maximumAttachAngle;
    }

    public void SetSocketActive(bool active)
    {
        if (socketInteractor != null)
            socketInteractor.socketActive = active;
    }

    private void OnValidate()
    {
        maximumAttachDistance = Mathf.Max(0.001f, maximumAttachDistance);
        maximumAttachAngle = Mathf.Clamp(maximumAttachAngle, 0f, 180f);
    }
}
