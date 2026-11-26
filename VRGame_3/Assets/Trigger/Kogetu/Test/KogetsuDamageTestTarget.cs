
using UnityEngine;

/// <summary>
/// 弧月のダメージ判定を確認するための一時的なテスト対象。
/// </summary>
[DisallowMultipleComponent]
public sealed class KogetsuDamageTestTarget :
    MonoBehaviour, IKogetsuDamageReceiver
{
    [Header("Health")]
    [SerializeField, Min(1f)]
    private float maxHealth = 100f;

    private float currentHealth;

    public float CurrentHealth => currentHealth;

    private void Awake()
    {
        currentHealth = maxHealth;
    }

    public void ReceiveKogetsuDamage(KogetsuDamageInfo damageInfo)
    {
        if (currentHealth <= 0f)
            return;

        float damage = Mathf.Max(0f, damageInfo.Damage);

        if (damage <= 0f)
            return;

        float previousHealth = currentHealth;
        currentHealth = Mathf.Max(0f, currentHealth - damage);

        Debug.Log(
            $"[Kogetsu Damage Test] {name}: " +
            $"{previousHealth:F1} -> {currentHealth:F1} HP " +
            $"(Damage: {damage:F1}, Senku: {damageInfo.IsSenku})",
            this);
    }

    private void OnValidate()
    {
        maxHealth = Mathf.Max(1f, maxHealth);
    }
}
