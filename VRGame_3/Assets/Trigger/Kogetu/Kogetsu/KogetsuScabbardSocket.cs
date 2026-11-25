using UnityEngine;
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
