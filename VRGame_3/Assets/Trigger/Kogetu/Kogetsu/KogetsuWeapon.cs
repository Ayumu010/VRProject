using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(XRGrabInteractable))]
public sealed class KogetsuWeapon : MonoBehaviour
{
    [Header("XR Grab")]
    [SerializeField]
    private XRGrabInteractable grabInteractable;

    [SerializeField]
    private Transform gripAttachTransform;

    [Header("Attack")]
    [SerializeField]
    private KogetsuBladeDamage bladeDamage;

    [SerializeField]
    private KogetsuBladeExtensionVisual extensionVisual;

    private KogetsuWeaponManager weaponManager;
    private TriggerSide sourceSide;
    private TriggerSide? currentHolderSide;
    private KogetsuWeaponState state = KogetsuWeaponState.NotSpawned;

    public KogetsuWeaponState State => state;
    public TriggerSide SourceSide => sourceSide;
    public TriggerSide? CurrentHolderSide => currentHolderSide;
    public bool IsHeld => state == KogetsuWeaponState.HeldLeft || state == KogetsuWeaponState.HeldRight;
    public bool CanSheathe => state == KogetsuWeaponState.Dropped || state == KogetsuWeaponState.Sheathed;
    public Transform GripAttachTransform =>
        grabInteractable != null && grabInteractable.attachTransform != null
            ? grabInteractable.attachTransform
            : gripAttachTransform;
    public XRGrabInteractable GrabInteractable => grabInteractable;
    public Rigidbody Rigidbody { get; private set; }

    private void Awake()
    {
        CacheComponents();
    }

    private void OnEnable()
    {
        if (grabInteractable != null)
        {
            grabInteractable.selectEntered.AddListener(OnSelectEntered);
            grabInteractable.selectExited.AddListener(OnSelectExited);
        }
    }

    private void OnDisable()
    {
        if (grabInteractable != null)
        {
            grabInteractable.selectEntered.RemoveListener(OnSelectEntered);
            grabInteractable.selectExited.RemoveListener(OnSelectExited);
        }
    }

    public bool Initialize(KogetsuWeaponManager manager, TriggerSide registeredSide)
    {
        CacheComponents();

        if (manager == null || grabInteractable == null || gripAttachTransform == null || Rigidbody == null)
        {
            Debug.LogError("KogetsuWeapon: Manager, Rigidbody, XRGrabInteractable, or GripAttach is missing.", this);
            return false;
        }

        weaponManager = manager;
        sourceSide = registeredSide;
        currentHolderSide = null;

        // 弧月は同時に片手だけで保持する。
        grabInteractable.selectMode = InteractableSelectMode.Single;
        grabInteractable.attachTransform = gripAttachTransform;
        grabInteractable.throwOnDetach = true;

        state = KogetsuWeaponState.Dropped;

        if (bladeDamage != null)
            bladeDamage.Initialize(this, weaponManager);

        if (extensionVisual != null)
            extensionVisual.Initialize();

        return true;
    }

    public bool CanDealDamageNow()
    {
        return weaponManager != null && currentHolderSide.HasValue && weaponManager.CanDealDamage(this);
    }

    public void IgnoreCollisionsWith(
        Transform playerRoot,
        Transform excludedRoot = null)
    {
        if (playerRoot == null)
            return;

        Collider[] weaponColliders =
            GetComponentsInChildren<Collider>(true);

        Collider[] playerColliders =
            playerRoot.GetComponentsInChildren<Collider>(true);

        for (int weaponIndex = 0;
             weaponIndex < weaponColliders.Length;
             weaponIndex++)
        {
            Collider weaponCollider =
                weaponColliders[weaponIndex];

            if (weaponCollider == null)
                continue;

            for (int playerIndex = 0;
                 playerIndex < playerColliders.Length;
                 playerIndex++)
            {
                Collider playerCollider =
                    playerColliders[playerIndex];

                if (playerCollider == null ||
                    weaponCollider == playerCollider)
                    continue;

                // 鞘側のColliderは衝突無視の対象から除外する
                if (excludedRoot != null &&
                    (playerCollider.transform == excludedRoot ||
                     playerCollider.transform.IsChildOf(excludedRoot)))
                {
                    continue;
                }

                Physics.IgnoreCollision(
                    weaponCollider,
                    playerCollider,
                    true);
            }
        }
    }

    public void ResetToDroppedAtSocket()
    {
        currentHolderSide = null;
        state = KogetsuWeaponState.Dropped;

        if (Rigidbody != null)
        {
            // Transform配置後の初期PoseをPhysics側にも明示して、
            // Physics.autoSyncTransforms の設定に依存しないようにする。
            Rigidbody.position = transform.position;
            Rigidbody.rotation = transform.rotation;
            Rigidbody.linearVelocity = Vector3.zero;
            Rigidbody.angularVelocity = Vector3.zero;
        }
    }

    public void SetSenkuExtension(
        bool active,
        float extensionLength,
        float damageMultiplier,
        float minimumSwingSpeed)
    {
        if (bladeDamage != null)
        {
            bladeDamage.SetSenkuParameters(
                active,
                extensionLength,
                damageMultiplier,
                minimumSwingSpeed);
        }

        if (extensionVisual != null)
            extensionVisual.SetExtensionLength(active ? extensionLength : 0f);
    }

    /// <summary>
    /// 旋空Trailの発生を切り替える。
    /// </summary>
    public void SetSenkuTrailEmitting(bool emitting)
    {
        if (extensionVisual == null)
            return;

        extensionVisual.SetTrailEmitting(emitting);
    }

    public void CancelSenkuImmediately()
    {
        SetSenkuExtension(false, 0f, 1f, 0f);
    }

    private void CacheComponents()
    {
        if (grabInteractable == null)
            grabInteractable = GetComponent<XRGrabInteractable>();

        if (Rigidbody == null)
            Rigidbody = GetComponent<Rigidbody>();

        if (bladeDamage == null)
            bladeDamage = GetComponent<KogetsuBladeDamage>();

        if (extensionVisual == null)
            extensionVisual = GetComponentInChildren<KogetsuBladeExtensionVisual>(true);
    }

    private void OnSelectEntered(SelectEnterEventArgs args)
    {
        if (args == null || args.interactorObject == null)
            return;

        if (args.interactorObject is XRSocketInteractor)
        {
            currentHolderSide = null;
            state = KogetsuWeaponState.Sheathed;
            if (weaponManager != null)
                weaponManager.NotifyWeaponHolderChanged(null);
            CancelSenkuImmediately();
            return;
        }

        if (args.interactorObject is XRBaseInteractor baseInteractor)
        {
            switch (baseInteractor.handedness)
            {
                case InteractorHandedness.Left:
                    currentHolderSide = TriggerSide.Sub;
                    state = KogetsuWeaponState.HeldLeft;
                    break;

                case InteractorHandedness.Right:
                    currentHolderSide = TriggerSide.Main;
                    state = KogetsuWeaponState.HeldRight;
                    break;

                default:
                    currentHolderSide = null;
                    state = KogetsuWeaponState.Dropped;
                    break;
            }
        }
        else
        {
            currentHolderSide = null;
            state = KogetsuWeaponState.Dropped;
        }

        if (weaponManager != null)
            weaponManager.NotifyWeaponHolderChanged(currentHolderSide);
    }

    private void OnSelectExited(SelectExitEventArgs args)
    {
        if (args == null || args.interactorObject == null)
            return;

        if (args.interactorObject is XRSocketInteractor)
        {
            if (state == KogetsuWeaponState.Sheathed)
            {
                state = KogetsuWeaponState.Dropped;
                currentHolderSide = null;
            }

            // Socket解除と手による再取得のイベント順序に依存しないよう、
            // 現在の保持手情報をそのままManagerへ通知する。
            if (weaponManager != null)
                weaponManager.NotifyWeaponHolderChanged(currentHolderSide);
            return;
        }

        TriggerSide? exitingSide = null;
        if (args.interactorObject is XRBaseInteractor baseInteractor)
        {
            if (baseInteractor.handedness == InteractorHandedness.Left)
                exitingSide = TriggerSide.Sub;
            else if (baseInteractor.handedness == InteractorHandedness.Right)
                exitingSide = TriggerSide.Main;
        }

        if (exitingSide.HasValue && currentHolderSide == exitingSide)
        {
            currentHolderSide = null;
            state = KogetsuWeaponState.Dropped;
            if (weaponManager != null)
                weaponManager.NotifyWeaponHolderChanged(null);
        }
    }
}
