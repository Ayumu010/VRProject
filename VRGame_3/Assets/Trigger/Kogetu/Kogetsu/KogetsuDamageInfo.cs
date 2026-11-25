using UnityEngine;

/// <summary>
/// 弧月の近接攻撃が対象へ与えるダメージ情報。
/// </summary>
public readonly struct KogetsuDamageInfo
{
    public KogetsuWeapon Weapon { get; }
    public Transform PlayerRoot { get; }
    public TriggerSide SourceSide { get; }
    public TriggerSide HolderSide { get; }
    public Vector3 HitPoint { get; }
    public Vector3 HitDirection { get; }
    public float Damage { get; }
    public float SwingSpeed { get; }
    public bool IsSenku { get; }

    public KogetsuDamageInfo(
        KogetsuWeapon weapon,
        Transform playerRoot,
        TriggerSide sourceSide,
        TriggerSide holderSide,
        Vector3 hitPoint,
        Vector3 hitDirection,
        float damage,
        float swingSpeed,
        bool isSenku)
    {
        Weapon = weapon;
        PlayerRoot = playerRoot;
        SourceSide = sourceSide;
        HolderSide = holderSide;
        HitPoint = hitPoint;
        HitDirection = hitDirection;
        Damage = damage;
        SwingSpeed = swingSpeed;
        IsSenku = isSenku;
    }
}
