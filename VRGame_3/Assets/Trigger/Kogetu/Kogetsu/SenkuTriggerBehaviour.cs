using UnityEngine;

[CreateAssetMenu(
    fileName = "SenkuBehaviour",
    menuName = "Game/Triggers/Behaviours/Senku")]
public sealed class SenkuTriggerBehaviour : TriggerBehaviour
{
    [Header("Senku Settings")]
    [SerializeField, Min(0.01f)]
    private float extensionLength = 5f;

    [SerializeField, Min(0.01f)]
    private float activeDuration = 0.4f;

    [SerializeField, Min(0f)]
    private float cooldown = 1.5f;

    [SerializeField, Min(0.01f)]
    private float damageMultiplier = 1.5f;

    [Tooltip("0にすると通常攻撃側の下限速度を使わず、微小な移動速度だけを要求します。")]
    [SerializeField, Min(0f)]
    private float minimumSwingSpeed = 0f;

    [System.NonSerialized]
    private float nextActivationTime;

    [System.NonSerialized]
    private float activeTimeRemaining;

    [System.NonSerialized]
    private bool isActive;

    public bool IsActive => isActive;
    public TriggerSide RegisteredSide => Side;
    public float ExtensionLength => extensionLength;
    public float ActiveDuration => activeDuration;
    public float Cooldown => cooldown;
    public float DamageMultiplier => damageMultiplier;
    public float MinimumSwingSpeed => minimumSwingSpeed;

    /// <summary>
    /// 将来の入力処理から呼び出す旋空発動API。
    /// 発動条件は、現在この手で旋空が選択され、同じ手が弧月を保持していること。
    /// </summary>
    public bool TryActivate()
    {
        if (!IsRegistered || !IsSelected || isActive || Manager == null)
            return false;

        if (Manager.GetSelectedTrigger(Side) != this)
            return false;

        if (Time.time < nextActivationTime)
            return false;

        KogetsuWeaponManager weaponManager = ResolveWeaponManager();
        if (weaponManager == null)
            return false;

        if (!weaponManager.TryActivateSenku(
                Side,
                this,
                extensionLength,
                damageMultiplier,
                minimumSwingSpeed))
        {
            return false;
        }

        isActive = true;
        activeTimeRemaining = activeDuration;
        nextActivationTime = Time.time + cooldown;
        return true;
    }

    protected override void OnTick(float deltaTime)
    {
        if (!isActive)
            return;

        if (!IsSelected || Manager == null || Manager.GetSelectedTrigger(Side) != this)
        {
            Deactivate();
            return;
        }

        activeTimeRemaining -= deltaTime;
        if (activeTimeRemaining <= 0f)
            Deactivate();
    }

    protected override void OnDeselected()
    {
        Deactivate();
    }

    protected override void OnUnregistered()
    {
        Deactivate();
    }

    private KogetsuWeaponManager ResolveWeaponManager()
    {
        if (PlayerRoot == null)
            return null;

        Animator animator = PlayerRoot.GetComponentInParent<Animator>();
        return animator != null ? animator.GetComponent<KogetsuWeaponManager>() : null;
    }

    private void Deactivate()
    {
        if (!isActive)
            return;

        isActive = false;
        activeTimeRemaining = 0f;

        KogetsuWeaponManager weaponManager = ResolveWeaponManager();
        if (weaponManager != null)
            weaponManager.EndSenku(this);
    }

    /// <summary>
    /// WeaponManager が再生成・保持手変更・生成元解除で旋空を終了した場合の同期。
    /// Manager を呼び返さず、実行状態だけを解除する。
    /// </summary>
    internal void CancelFromWeaponManager()
    {
        isActive = false;
        activeTimeRemaining = 0f;
    }

    private void OnValidate()
    {
        extensionLength = Mathf.Max(0.01f, extensionLength);
        activeDuration = Mathf.Max(0.01f, activeDuration);
        cooldown = Mathf.Max(0f, cooldown);
        damageMultiplier = Mathf.Max(0.01f, damageMultiplier);
        minimumSwingSpeed = Mathf.Max(0f, minimumSwingSpeed);
    }
}
