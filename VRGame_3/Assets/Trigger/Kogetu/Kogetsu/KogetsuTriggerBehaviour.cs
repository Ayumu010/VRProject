using UnityEngine;

[CreateAssetMenu(
    fileName = "KogetsuBehaviour",
    menuName = "Game/Triggers/Behaviours/Kogetsu")]
public sealed class KogetsuTriggerBehaviour : TriggerBehaviour
{
    [Header("Weapon Prefabs")]
    [SerializeField]
    private GameObject swordPrefab;

    [SerializeField]
    private GameObject scabbardPrefab;

    [Header("Waist Anchor - Local To Humanoid Hips")]
    [SerializeField]
    private Vector3 leftWaistLocalPosition = new Vector3(-0.16f, -0.08f, -0.08f);

    [SerializeField]
    private Vector3 leftWaistLocalEulerAngles = new Vector3(0f, 0f, 12f);

    [SerializeField]
    private Vector3 rightWaistLocalPosition = new Vector3(0.16f, -0.08f, -0.08f);

    [SerializeField]
    private Vector3 rightWaistLocalEulerAngles = new Vector3(0f, 0f, -12f);

    [System.NonSerialized]
    private KogetsuWeaponManager weaponManager;

    protected override void OnRegistered()
    {
        weaponManager = ResolveWeaponManager();
    }

    protected override void OnInput(
        TriggerControl control,
        bool pressed)
    {
        // Triggerを押した瞬間だけ弧月を生成する。
        if (control != TriggerControl.Trigger || !pressed)
            return;

        RequestSpawn();
    }

    protected override void OnDeselected()
    {
        // 生成元Kogetsuが無効になったら、進行中の旋空だけを即時終了する。
        // SwordとScabbardのGameObjectは破棄しない。
        weaponManager?.NotifySourceKogetsuDeselected(Side);
    }

    /// <summary>
    /// 将来の入力処理から呼び出す弧月生成API。
    /// 現段階では TriggerInputRouter などの入力コードには接続しない。
    /// </summary>
    public bool RequestSpawn()
    {
        if (!IsRegistered || !IsSelected)
        {
            Debug.LogWarning("Kogetsu: Spawn request ignored because this trigger is not selected.", this);
            return false;
        }

        if (weaponManager == null)
            weaponManager = ResolveWeaponManager();

        if (weaponManager == null)
            return false;

        return weaponManager.TrySpawn(Side);
    }

    private KogetsuWeaponManager ResolveWeaponManager()
    {
        if (PlayerRoot == null)
        {
            Debug.LogError("Kogetsu: PlayerRoot is missing in TriggerRuntimeContext.", this);
            return null;
        }

        Animator animator = PlayerRoot.GetComponentInParent<Animator>();
        if (animator == null)
        {
            Debug.LogError("Kogetsu: Humanoid Animator was not found above PlayerRoot.", this);
            return null;
        }

        KogetsuWeaponManager manager = animator.GetComponent<KogetsuWeaponManager>();
        if (manager == null)
            manager = animator.gameObject.AddComponent<KogetsuWeaponManager>();

        manager.Configure(
            Manager,
            animator,
            PlayerRoot,
            swordPrefab,
            scabbardPrefab,
            leftWaistLocalPosition,
            leftWaistLocalEulerAngles,
            rightWaistLocalPosition,
            rightWaistLocalEulerAngles);

        return manager;
    }

    // OnDeselected / OnUnregistered では武器を破棄しない。
    // 武器の永続管理は PlayerModel 側の KogetsuWeaponManager が担当する。
}
