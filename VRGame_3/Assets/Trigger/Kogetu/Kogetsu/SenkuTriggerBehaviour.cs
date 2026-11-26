using UnityEngine;

[CreateAssetMenu(
    fileName = "SenkuBehaviour",
    menuName = "Game/Triggers/Behaviours/Senku")]
public sealed class SenkuTriggerBehaviour : TriggerBehaviour
{
    private enum RuntimeState
    {
        Idle,
        Armed,
        Active,
        Cooldown
    }

    [Header("Senku Settings")]
    [SerializeField, Min(0.01f)]
    private float extensionLength = 5f;

    [SerializeField, Min(0.01f)]
    private float activationWindow = 0.7f;

    [SerializeField, Min(0.01f)]
    private float activeDuration = 0.4f;

    [SerializeField, Min(0f)]
    private float cooldown = 1.5f;

    [SerializeField, Min(0.01f)]
    private float damageMultiplier = 1.5f;

    [SerializeField, Min(0f)]
    private float minimumSwingSpeed = 1.5f;

    [System.NonSerialized]
    private RuntimeState runtimeState;

    [System.NonSerialized]
    private float stateTimeRemaining;

    public bool IsArmed => runtimeState == RuntimeState.Armed;
    public bool IsActive => runtimeState == RuntimeState.Active;
    public bool IsInCooldown => runtimeState == RuntimeState.Cooldown;

    public TriggerSide RegisteredSide => Side;

    public float ExtensionLength => extensionLength;
    public float ActivationWindow => activationWindow;
    public float ActiveDuration => activeDuration;
    public float Cooldown => cooldown;
    public float DamageMultiplier => damageMultiplier;
    public float MinimumSwingSpeed => minimumSwingSpeed;

    /// <summary>
    /// 旋空の待機を開始する。
    /// この時点では刀身を延長しない。
    /// </summary>
    public bool TryArm()
    {
        if (!IsRegistered ||
            !IsSelected ||
            runtimeState != RuntimeState.Idle ||
            Manager == null)
        {
            return false;
        }

        if (Manager.GetSelectedTrigger(Side) != this)
            return false;

        KogetsuWeaponManager weaponManager = ResolveWeaponManager();

        if (weaponManager == null)
            return false;

        if (!weaponManager.TryArmSenku(Side, this))
            return false;

        runtimeState = RuntimeState.Armed;
        stateTimeRemaining = activationWindow;

        return true;
    }

    /// <summary>
    /// 既存のデバッグコードとの互換性を維持する。
    /// 現在は即時発動ではなく Armed 開始となる。
    /// </summary>
    public bool TryActivate()
    {
        return TryArm();
    }

    protected override void OnInput(
        TriggerControl control,
        bool pressed)
    {
        if (control != TriggerControl.Trigger || !pressed)
            return;

        TryArm();
    }

    protected override void OnTick(float deltaTime)
    {
        switch (runtimeState)
        {
            case RuntimeState.Armed:
                {
                    if (!IsSelected ||
                        Manager == null ||
                        Manager.GetSelectedTrigger(Side) != this)
                    {
                        CancelForLifecycle();
                        return;
                    }

                    stateTimeRemaining -= deltaTime;

                    if (stateTimeRemaining <= 0f)
                    {
                        runtimeState = RuntimeState.Idle;
                        stateTimeRemaining = 0f;

                        KogetsuWeaponManager weaponManager =
                            ResolveWeaponManager();

                        weaponManager?.CancelArmedSenku(this);
                    }

                    break;
                }

            case RuntimeState.Active:
                {
                    if (!IsSelected ||
                        Manager == null ||
                        Manager.GetSelectedTrigger(Side) != this)
                    {
                        CancelForLifecycle();
                        return;
                    }

                    stateTimeRemaining -= deltaTime;

                    if (stateTimeRemaining <= 0f)
                    {
                        runtimeState = RuntimeState.Cooldown;
                        stateTimeRemaining = cooldown;

                        KogetsuWeaponManager weaponManager =
                            ResolveWeaponManager();

                        weaponManager?.EndSenku(this);
                    }

                    break;
                }

            case RuntimeState.Cooldown:
                {
                    stateTimeRemaining -= deltaTime;

                    if (stateTimeRemaining <= 0f)
                    {
                        runtimeState = RuntimeState.Idle;
                        stateTimeRemaining = 0f;
                    }

                    break;
                }
        }
    }

    /// <summary>
    /// WeaponManager から Active 開始を通知された場合に呼ぶ。
    /// </summary>
    internal bool BeginActiveFromManager()
    {
        if (runtimeState != RuntimeState.Armed)
            return false;

        runtimeState = RuntimeState.Active;
        stateTimeRemaining = activeDuration;

        return true;
    }

    protected override void OnDeselected()
    {
        CancelForLifecycle();
    }

    protected override void OnUnregistered()
    {
        CancelForLifecycle();
    }

    private void CancelForLifecycle()
    {
        if (runtimeState != RuntimeState.Armed &&
            runtimeState != RuntimeState.Active)
        {
            return;
        }

        bool wasActive = runtimeState == RuntimeState.Active;

        KogetsuWeaponManager weaponManager =
            ResolveWeaponManager();

        if (weaponManager != null)
        {
            weaponManager.CancelSenku(this, wasActive);
        }
        else
        {
            CancelFromWeaponManager(wasActive);
        }
    }

    private KogetsuWeaponManager ResolveWeaponManager()
    {
        if (PlayerRoot == null)
            return null;

        Animator animator =
            PlayerRoot.GetComponentInParent<Animator>();

        return animator != null
            ? animator.GetComponent<KogetsuWeaponManager>()
            : null;
    }

    /// <summary>
    /// WeaponManager 側から強制キャンセルされた場合の状態同期。
    /// </summary>
    internal void CancelFromWeaponManager(bool enterCooldown)
    {
        bool wasActive = runtimeState == RuntimeState.Active;
        bool wasArmed = runtimeState == RuntimeState.Armed;

        if (!wasActive && !wasArmed)
            return;

        if (enterCooldown && wasActive)
        {
            runtimeState = RuntimeState.Cooldown;
            stateTimeRemaining = cooldown;
        }
        else
        {
            runtimeState = RuntimeState.Idle;
            stateTimeRemaining = 0f;
        }
    }

    private void OnValidate()
    {
        extensionLength = Mathf.Max(0.01f, extensionLength);
        activationWindow = Mathf.Max(0.01f, activationWindow);
        activeDuration = Mathf.Max(0.01f, activeDuration);
        cooldown = Mathf.Max(0f, cooldown);
        damageMultiplier = Mathf.Max(0.01f, damageMultiplier);
        minimumSwingSpeed = Mathf.Max(0f, minimumSwingSpeed);
    }
}