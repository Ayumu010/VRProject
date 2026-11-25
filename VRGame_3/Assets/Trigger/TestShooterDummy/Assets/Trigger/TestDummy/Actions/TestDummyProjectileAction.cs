using UnityEngine;

[CreateAssetMenu(
    fileName = "TestDummyProjectileAction",
    menuName = "Game/Test Dummy/Actions/Projectile Volley"
)]
public sealed class TestDummyProjectileAction : TestDummyTriggerAction
{
    [Header("Projectile")]
    [SerializeField] private GameObject projectilePrefabOverride;
    [SerializeField, Min(1)] private int projectileCount = 1;

    [Header("Spread")]
    [SerializeField, Range(0f, 180f)] private float horizontalSpreadAngle = 0f;

    public override void Execute(TestDummyContext context)
    {
        if (context == null)
        {
            Debug.LogError("TestDummyProjectileAction: Context is null.", this);
            return;
        }

        GameObject prefab = projectilePrefabOverride != null
            ? projectilePrefabOverride
            : context.DefaultProjectilePrefab;

        if (prefab == null)
        {
            Debug.LogError("TestDummyProjectileAction: Projectile Prefab is missing.", this);
            return;
        }

        if (!context.TryGetAimDirection(out Vector3 aimDirection))
        {
            Debug.LogWarning("TestDummyProjectileAction: 有効なTargetがありません。", this);
            return;
        }

        Vector3 up = Mathf.Abs(Vector3.Dot(aimDirection, Vector3.up)) > 0.98f
            ? Vector3.forward
            : Vector3.up;
        Quaternion baseRotation = Quaternion.LookRotation(aimDirection, up);
        int count = Mathf.Max(1, projectileCount);

        for (int i = 0; i < count; i++)
        {
            float yaw = count <= 1
                ? 0f
                : Mathf.Lerp(
                    -horizontalSpreadAngle * 0.5f,
                    horizontalSpreadAngle * 0.5f,
                    i / (float)(count - 1));

            Vector3 direction = baseRotation *
                (Quaternion.Euler(0f, yaw, 0f) * Vector3.forward);

            context.SpawnProjectile(prefab, direction);
        }
    }

    private void OnValidate()
    {
        projectileCount = Mathf.Max(1, projectileCount);
        horizontalSpreadAngle = Mathf.Clamp(horizontalSpreadAngle, 0f, 180f);
    }
}
