
using System;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(MeshCollider))]
public sealed class ShieldSurface : MonoBehaviour
{
    [Header("Durability")]
    [SerializeField, Min(1f)]
    private float baseMaxDurability = 100f;

    [Header("Collision")]
    [SerializeField, Min(0.0001f)]
    private float contactOffset = 0.002f;

    private MeshCollider shieldCollider;

    private float durabilityMultiplier = 1f;
    private float damageTaken;

    public float MaxDurability =>
        baseMaxDurability * durabilityMultiplier;

    public float CurrentDurability { get; private set; }

    public bool IsBroken { get; private set; }

    public event Action<ShieldSurface> Broken;

    public MeshCollider ShieldCollider => shieldCollider;

    // ==================================================
    // Awake
    // ==================================================

    private void Awake()
    {
        shieldCollider = GetComponent<MeshCollider>();

        shieldCollider.convex = true;
        shieldCollider.isTrigger = false;

        shieldCollider.contactOffset =
            contactOffset;

        ResetSurface();
    }

    // ==================================================
    // Validation
    // ==================================================

    private void OnValidate()
    {
        baseMaxDurability = Mathf.Max(
            1f,
            baseMaxDurability
        );

        contactOffset = Mathf.Max(
            0.0001f,
            contactOffset
        );

        MeshCollider collider =
            GetComponent<MeshCollider>();

        if (collider != null)
        {
            collider.convex = true;
            collider.isTrigger = false;
            collider.contactOffset = contactOffset;
        }
    }

    // ==================================================
    // Reset
    // ==================================================

    public void ResetSurface()
    {
        if (shieldCollider == null)
            shieldCollider = GetComponent<MeshCollider>();

        IsBroken = false;

        durabilityMultiplier = 1f;
        damageTaken = 0f;

        CurrentDurability = MaxDurability;

        if (shieldCollider != null)
            shieldCollider.enabled = true;
    }

    // ==================================================
    // Durability
    // ==================================================

    public bool SetDurabilityMultiplier(float multiplier)
    {
        if (IsBroken ||
            !isActiveAndEnabled ||
            multiplier <= 0f)
        {
            return false;
        }

        durabilityMultiplier = multiplier;

        CurrentDurability = Mathf.Max(
            0f,
            MaxDurability - damageTaken
        );

        if (CurrentDurability <= 0f)
        {
            BreakShield();
            return false;
        }

        return true;
    }

    // ==================================================
    // Projectile Hit
    // ==================================================

    public bool ReceiveProjectileHit(
        float damage,
        ProjectileCollisionSystem projectile = null)
    {
        if (IsBroken ||
            !isActiveAndEnabled ||
            shieldCollider == null ||
            !shieldCollider.enabled ||
            damage <= 0f)
        {
            return false;
        }

        damageTaken += damage;

        CurrentDurability = Mathf.Max(
            0f,
            MaxDurability - damageTaken
        );

        Debug.Log(
            $"Shield Hit : {CurrentDurability:F1}/" +
            $"{MaxDurability:F1}",
            this
        );

        if (CurrentDurability <= 0f)
        {
            BreakShield();
        }

        return true;
    }

    // ==================================================
    // Break
    // ==================================================

    private void BreakShield()
    {
        if (IsBroken)
            return;

        IsBroken = true;

        if (shieldCollider != null)
            shieldCollider.enabled = false;

        Debug.Log(
            "Shield Broken",
            this
        );

        Broken?.Invoke(this);
    }
}
