using UnityEngine;

/// <summary>
/// Read-only runtime context supplied to a TestDummyTriggerAction.
/// </summary>
public sealed class TestDummyContext
{
    private readonly TestDummyShooter shooter;

    internal TestDummyContext(TestDummyShooter shooter)
    {
        this.shooter = shooter;
    }

    public TestDummyShooter Shooter => shooter;
    public Transform OwnerRoot => shooter != null ? shooter.transform : null;
    public ulong OwnerId => shooter != null ? shooter.OwnerId : 0UL;
    public Transform Target => shooter != null ? shooter.CurrentTarget : null;
    public Transform Muzzle => shooter != null ? shooter.Muzzle : null;
    public GameObject DefaultProjectilePrefab =>
        shooter != null ? shooter.DefaultProjectilePrefab : null;

    public bool TryGetAimDirection(out Vector3 direction)
    {
        if (shooter != null)
        {
            return shooter.TryGetAimDirection(out direction);
        }

        direction = Vector3.zero;
        return false;
    }

    public bool SpawnProjectile(GameObject prefab, Vector3 direction)
    {
        return shooter != null && shooter.SpawnProjectile(prefab, direction);
    }
}
