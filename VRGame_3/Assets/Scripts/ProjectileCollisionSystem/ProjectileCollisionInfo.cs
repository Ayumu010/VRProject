using UnityEngine;

/// <summary>
/// ProjectileCollisionSystem が Receiver に渡す接触情報。
/// </summary>
public readonly struct ProjectileCollisionInfo
{
    /// <summary>接触を検知した側のシステム。</summary>
    public ProjectileCollisionSystem Self { get; }

    /// <summary>接触相手側のCollider。</summary>
    public Collider OtherCollider { get; }

    /// <summary>相手が弾なら相手側のシステム。弾以外ならnull。</summary>
    public ProjectileCollisionSystem OtherProjectile { get; }

    /// <summary>接触相手のGameObject。</summary>
    public GameObject OtherObject => OtherCollider != null ? OtherCollider.gameObject : null;

    public bool IsOtherProjectile => OtherProjectile != null;

    internal ProjectileCollisionInfo(
        ProjectileCollisionSystem self,
        Collider otherCollider,
        ProjectileCollisionSystem otherProjectile)
    {
        Self = self;
        OtherCollider = otherCollider;
        OtherProjectile = otherProjectile;
    }

    public bool TryGetOtherOwnerId(out ulong ownerId)
    {
        if (OtherProjectile != null && OtherProjectile.IsInitialized)
        {
            ownerId = OtherProjectile.OwnerId;
            return true;
        }

        ownerId = default;
        return false;
    }
}
