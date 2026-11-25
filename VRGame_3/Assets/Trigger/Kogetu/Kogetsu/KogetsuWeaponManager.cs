using UnityEngine;

/// <summary>
/// プレイヤー単位で弧月と鞘の1セットを管理する。
/// TriggerBehaviour の寿命とは独立しているため、スロット変更後も武器を保持できる。
/// </summary>
[DisallowMultipleComponent]
public sealed class KogetsuWeaponManager : MonoBehaviour
{
    private const string LeftAnchorName = "Kogetsu_LeftWaistAnchor";
    private const string RightAnchorName = "Kogetsu_RightWaistAnchor";

    private TriggerSlotManager slotManager;
    private Animator ownerAnimator;
    private Transform playerRoot;
    private GameObject swordPrefab;
    private GameObject scabbardPrefab;

    private Vector3 leftWaistLocalPosition;
    private Vector3 leftWaistLocalEulerAngles;
    private Vector3 rightWaistLocalPosition;
    private Vector3 rightWaistLocalEulerAngles;

    private Transform hips;
    private Transform leftWaistAnchor;
    private Transform rightWaistAnchor;

    private KogetsuWeapon currentWeapon;
    private GameObject currentScabbard;
    private KogetsuScabbardSocket currentSocket;
    private TriggerSide currentSourceSide;
    private bool hasActiveSet;
    private SenkuTriggerBehaviour activeSenkuBehaviour;

    public KogetsuWeapon CurrentWeapon => currentWeapon;
    public Transform PlayerRoot => playerRoot;
    public TriggerSide CurrentSourceSide => currentSourceSide;
    public bool HasActiveSet => hasActiveSet && currentWeapon != null;

    public void Configure(
        TriggerSlotManager manager,
        Animator animator,
        Transform skeletonRoot,
        GameObject weaponPrefab,
        GameObject sheathPrefab,
        Vector3 leftPosition,
        Vector3 leftEuler,
        Vector3 rightPosition,
        Vector3 rightEuler)
    {
        if (manager == null || animator == null || skeletonRoot == null)
        {
            Debug.LogError("KogetsuWeaponManager: Required player references are missing.", this);
            return;
        }

        if (slotManager != null && slotManager != manager)
        {
            Debug.LogError("KogetsuWeaponManager: Cannot be shared by different TriggerSlotManagers.", this);
            return;
        }

        slotManager = manager;
        ownerAnimator = animator;
        playerRoot = skeletonRoot;
        swordPrefab = weaponPrefab;
        scabbardPrefab = sheathPrefab;

        leftWaistLocalPosition = leftPosition;
        leftWaistLocalEulerAngles = leftEuler;
        rightWaistLocalPosition = rightPosition;
        rightWaistLocalEulerAngles = rightEuler;

        ResolveHipsAndAnchors();
    }

    /// <summary>
    /// KogetsuTriggerBehaviour.RequestSpawn から呼び出す。
    /// Main(右手)で登録された弧月は左腰、Sub(左手)で登録された弧月は右腰に生成する。
    /// </summary>
    public bool TrySpawn(TriggerSide sourceSide)
    {
        if (!IsSourceKogetsuSelected(sourceSide))
        {
            Debug.LogWarning($"Kogetsu: Source trigger is not selected ({sourceSide}).", this);
            return false;
        }

        if (swordPrefab == null || scabbardPrefab == null)
        {
            Debug.LogError("Kogetsu: Sword Prefab or Scabbard Prefab is not assigned on KogetsuBehaviour.", this);
            return false;
        }

        if (!ResolveHipsAndAnchors())
            return false;

        Transform waistAnchor = sourceSide == TriggerSide.Main
            ? leftWaistAnchor
            : rightWaistAnchor;

        // 古いセットを先に無効化する。Destroy自体はUnityのフレーム終端だが、
        // Collider/Interactorを含むGameObjectはその場で非アクティブにする。
        DestroyCurrentSet();

        GameObject scabbardInstance = Instantiate(
            scabbardPrefab,
            waistAnchor.position,
            waistAnchor.rotation,
            waistAnchor);
        scabbardInstance.name = "Kogetsu_Scabbard_Runtime";
        scabbardInstance.transform.localScale = Vector3.one;

        KogetsuScabbardSocket socket = scabbardInstance.GetComponentInChildren<KogetsuScabbardSocket>(true);
        if (socket == null || !socket.HasValidReferences)
        {
            Debug.LogError("Kogetsu: Scabbard Prefab needs a configured KogetsuScabbardSocket.", scabbardInstance);
            Destroy(scabbardInstance);
            return false;
        }

        GameObject swordInstance = Instantiate(swordPrefab);
        swordInstance.name = "Kogetsu_Sword_Runtime";
        swordInstance.transform.localScale = Vector3.one;

        KogetsuWeapon weapon = swordInstance.GetComponent<KogetsuWeapon>();
        if (weapon == null || !weapon.Initialize(this, sourceSide))
        {
            Debug.LogError("Kogetsu: Sword Prefab needs a valid KogetsuWeapon and XRGrabInteractable.", swordInstance);
            Destroy(swordInstance);
            Destroy(scabbardInstance);
            return false;
        }

        currentWeapon = weapon;
        currentScabbard = scabbardInstance;
        currentSocket = socket;
        currentSourceSide = sourceSide;
        hasActiveSet = true;
        weapon.IgnoreCollisionsWith(
            playerRoot,
            scabbardInstance.transform);

        Transform socketAttach = socket.SocketAttachTransform;
        Transform gripAttach = weapon.GripAttachTransform;
        // 剣がSocketZoneに入る前にSocketを有効化する
        socket.Initialize(weapon);
        if (socketAttach == null || gripAttach == null)
        {
            Debug.LogError("Kogetsu: GripAttach or SocketAttach is missing.", swordInstance);
            DestroyCurrentSet();
            return false;
        }

        // GripAttach のワールド位置・回転を SocketAttach に一致させる。
        // FBXのモデル原点が異なっていても、アタッチ用Transformを基準に抜き差しできる。
        Quaternion rootRotation = socketAttach.rotation * Quaternion.Inverse(gripAttach.localRotation);
        Vector3 scaledLocalAttach = Vector3.Scale(gripAttach.localPosition, swordInstance.transform.lossyScale);
        Vector3 rootPosition = socketAttach.position - rootRotation * scaledLocalAttach;
        swordInstance.transform.SetPositionAndRotation(rootPosition, rootRotation);

weapon.ResetToDroppedAtSocket();

// TransformとPhysicsの位置情報を同期する
Physics.SyncTransforms();

return true;    }

    public bool CanDealDamage(KogetsuWeapon weapon)
    {
        return weapon != null &&
               weapon == currentWeapon &&
               hasActiveSet &&
               weapon.IsHeld &&
               weapon.CurrentHolderSide.HasValue &&
               weapon.SourceSide == currentSourceSide &&
               IsSourceKogetsuSelected(currentSourceSide);
    }

    public bool IsOwnPlayerTransform(Transform target)
    {
        return playerRoot != null && target != null &&
               (target == playerRoot || target.IsChildOf(playerRoot));
    }

    public bool TryActivateSenku(
        TriggerSide holderSide,
        SenkuTriggerBehaviour senkuBehaviour,
        float extensionLength,
        float damageMultiplier,
        float minimumSwingSpeed)
    {
        if (senkuBehaviour == null || currentWeapon == null || !hasActiveSet)
            return false;

        if (!CanDealDamageSourceOnly())
            return false;

        if (!currentWeapon.IsHeld || currentWeapon.CurrentHolderSide != holderSide)
            return false;

        if (slotManager == null || slotManager.GetSelectedTrigger(holderSide) != senkuBehaviour)
            return false;

        if (!senkuBehaviour.IsSelected)
            return false;

        if (activeSenkuBehaviour != null && activeSenkuBehaviour != senkuBehaviour)
            return false;

        activeSenkuBehaviour = senkuBehaviour;
        currentWeapon.SetSenkuExtension(
            true,
            extensionLength,
            damageMultiplier,
            minimumSwingSpeed);
        return true;
    }

    public void EndSenku(SenkuTriggerBehaviour senkuBehaviour)
    {
        if (senkuBehaviour == null || senkuBehaviour != activeSenkuBehaviour)
            return;

        activeSenkuBehaviour = null;
        senkuBehaviour.CancelFromWeaponManager();

        if (currentWeapon != null)
            currentWeapon.SetSenkuExtension(false, 0f, 1f, 0f);
    }

    public void NotifySourceKogetsuDeselected(TriggerSide sourceSide)
    {
        if (sourceSide == currentSourceSide && activeSenkuBehaviour != null)
            EndSenku(activeSenkuBehaviour);
    }

    public void NotifyWeaponHolderChanged(TriggerSide? newHolderSide)
    {
        if (activeSenkuBehaviour == null)
            return;

        if (!newHolderSide.HasValue || activeSenkuBehaviour.RegisteredSide != newHolderSide.Value)
            EndSenku(activeSenkuBehaviour);
    }

    private bool CanDealDamageSourceOnly()
    {
        return hasActiveSet && currentWeapon != null &&
               currentWeapon.SourceSide == currentSourceSide &&
               IsSourceKogetsuSelected(currentSourceSide);
    }

    private bool IsSourceKogetsuSelected(TriggerSide side)
    {
        if (slotManager == null)
            return false;

        TriggerBehaviour selected = slotManager.GetSelectedTrigger(side);
        return selected is KogetsuTriggerBehaviour && selected.IsSelected;
    }

    private bool ResolveHipsAndAnchors()
    {
        if (ownerAnimator == null)
        {
            Debug.LogError("KogetsuWeaponManager: Animator reference is missing.", this);
            return false;
        }

        hips = ownerAnimator.GetBoneTransform(HumanBodyBones.Hips);
        if (hips == null)
        {
            Debug.LogError("KogetsuWeaponManager: Humanoid Hips bone is unavailable. Check the Animator Avatar is Humanoid.", this);
            return false;
        }

        leftWaistAnchor = GetOrCreateAnchor(
            hips,
            LeftAnchorName,
            leftWaistLocalPosition,
            leftWaistLocalEulerAngles);
        rightWaistAnchor = GetOrCreateAnchor(
            hips,
            RightAnchorName,
            rightWaistLocalPosition,
            rightWaistLocalEulerAngles);

        return leftWaistAnchor != null && rightWaistAnchor != null;
    }

    private static Transform GetOrCreateAnchor(
        Transform parent,
        string anchorName,
        Vector3 localPosition,
        Vector3 localEulerAngles)
    {
        Transform anchor = parent.Find(anchorName);
        if (anchor == null)
        {
            GameObject anchorObject = new GameObject(anchorName);
            anchor = anchorObject.transform;
            anchor.SetParent(parent, false);
        }

        Vector3 parentScale = parent.lossyScale;
        Vector3 inverseParentScale = new Vector3(
            InverseScale(parentScale.x),
            InverseScale(parentScale.y),
            InverseScale(parentScale.z));

        // 位置オフセットと子のワールドスケールを両方補正し、
        // PlayerModel が 0.735 倍などでも腰オフセットをメートル単位で調整できるようにする。
        anchor.localPosition = Vector3.Scale(localPosition, inverseParentScale);
        anchor.localRotation = Quaternion.Euler(localEulerAngles);
        anchor.localScale = inverseParentScale;
        return anchor;
    }


    private static float InverseScale(float value)
    {
        return Mathf.Abs(value) < 0.0001f ? 1f : 1f / value;
    }

    private void DestroyCurrentSet()
    {
        if (currentSocket != null)
            currentSocket.SetSocketActive(false);

        if (activeSenkuBehaviour != null)
            activeSenkuBehaviour.CancelFromWeaponManager();
        activeSenkuBehaviour = null;

        if (currentWeapon != null)
        {
            currentWeapon.CancelSenkuImmediately();
            currentWeapon.transform.SetParent(null, true);
            currentWeapon.gameObject.SetActive(false);
            Destroy(currentWeapon.gameObject);
        }

        if (currentScabbard != null)
        {
            currentScabbard.SetActive(false);
            Destroy(currentScabbard);
        }

        currentWeapon = null;
        currentScabbard = null;
        currentSocket = null;
        hasActiveSet = false;
    }

    [ContextMenu("Debug/Spawn Kogetsu From Main (Right Hand) Slot")]
    private void DebugSpawnFromMain()
    {
        DebugSpawnForSide(TriggerSide.Main);
    }

    [ContextMenu("Debug/Spawn Kogetsu From Sub (Left Hand) Slot")]
    private void DebugSpawnFromSub()
    {
        DebugSpawnForSide(TriggerSide.Sub);
    }

    [ContextMenu("Debug/Spawn Kogetsu From Selected Source (Main First)")]
    private void DebugSpawnFromSelectedSource()
    {
        if (IsSourceKogetsuSelected(TriggerSide.Main))
        {
            DebugSpawnForSide(TriggerSide.Main);
            return;
        }

        DebugSpawnForSide(TriggerSide.Sub);
    }

    private void DebugSpawnForSide(TriggerSide side)
    {
        if (slotManager == null)
        {
            Debug.LogWarning("Kogetsu: TriggerSlotManager is not configured.", this);
            return;
        }

        if (!IsSourceKogetsuSelected(side))
        {
            Debug.LogWarning($"Kogetsu: {side} Kogetsu trigger is not selected.", this);
            return;
        }

        TrySpawn(side);
    }

    private void OnDestroy()
    {
        DestroyCurrentSet();
    }
}
