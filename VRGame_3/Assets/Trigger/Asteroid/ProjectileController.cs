using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(TrailRenderer))]
[RequireComponent(typeof(ProjectileCollisionSystem))]
public class ProjectileController : MonoBehaviour, IProjectileCollisionReceiver
{
    [Header("References")]
    [SerializeField] private Rigidbody rb;
    [SerializeField] private Collider hitCollider;
    [SerializeField] private TrailRenderer trailRenderer;
    [SerializeField] private GameObject projectileVisual;
    [SerializeField] private ProjectileCollisionSystem collisionSystem;

    [Header("Flight Settings")]
    [SerializeField] private float speed = 20f;
    [SerializeField] private float maxFlightTime = 5f;

    [Header("Shield Interaction")]
    [SerializeField, Min(0f)]
    private float shieldDamage = 10f;

    [Header("Trail Scale")]
    [SerializeField] private Transform trailScaleSource;

    private float initialTrailWidth;
    private float initialTrailTime;
    private float initialObjectScale;
    private Renderer[] projectileRenderers;

    private bool isFlying;
    private bool hasLaunched;
    private float flightTimer;

    private void Awake()
    {
        if (rb == null)
            rb = GetComponent<Rigidbody>();

        if (hitCollider == null)
            hitCollider = GetComponent<Collider>();

        if (trailRenderer == null)
            trailRenderer = GetComponent<TrailRenderer>();

        if (collisionSystem == null)
            collisionSystem = GetComponent<ProjectileCollisionSystem>();

        trailRenderer.emitting = false;
        rb.isKinematic = true;
        hitCollider.enabled = false;

        if (trailScaleSource == null)
            trailScaleSource = transform;

        initialTrailWidth = trailRenderer.widthMultiplier;
        initialTrailTime = trailRenderer.time;

        initialObjectScale = Mathf.Max(
            GetScaleMagnitude(trailScaleSource.lossyScale),
            0.0001f
        );

        if (projectileVisual != null)
        {
            projectileRenderers =
                projectileVisual.GetComponentsInChildren<Renderer>(true);

            if (projectileRenderers.Length == 0)
            {
                Debug.LogWarning(
                    "ProjectileVisualにRendererが見つかりません。",
                    this
                );
            }
        }

        float headCurveValue = trailRenderer.widthCurve.Evaluate(0f);

        if (headCurveValue <= 0.0001f)
        {
            Debug.LogWarning(
                "Trail RendererのWidth CurveのTime 0側が0です。弾側の先端幅を合わせるには、開始値を0より大きくしてください。",
                this
            );
        }
    }

    /// <summary>
    /// 弾を発射する。
    /// ignoredCollidersには発射元Cubeやプレイヤーなど、個別に無視したいColliderを渡す。
    /// ProjectileCollisionSystem.Initializeは、この関数を呼ぶ前に済ませること。
    /// </summary>
    public void Launch(
        Vector3 direction,
        Collider[] ignoredColliders = null
    )
    {
        if (hasLaunched)
        {
            Debug.LogWarning("この弾は既に発射されています。", this);
            return;
        }

        if (direction.sqrMagnitude < 0.000001f)
        {
            Debug.LogError("発射方向がゼロです。", this);
            return;
        }

        if (collisionSystem == null || !collisionSystem.IsInitialized)
        {
            Debug.LogError(
                "ProjectileCollisionSystemが初期化されていません。Launchより前にInitialize(ownerId, ownerRoot)を呼び出してください。",
                this
            );
            return;
        }

        direction.Normalize();

        hasLaunched = true;
        isFlying = true;
        flightTimer = 0f;

        if (projectileVisual != null)
            projectileVisual.SetActive(true);

        rb.isKinematic = false;

        rb.collisionDetectionMode =
            hitCollider is BoxCollider ||
            hitCollider is SphereCollider ||
            hitCollider is CapsuleCollider
                ? CollisionDetectionMode.ContinuousDynamic
                : CollisionDetectionMode.ContinuousSpeculative;

        rb.angularVelocity = Vector3.zero;

        hitCollider.enabled = true;

        // 同じOwnerの既存弾、および発射者自身のColliderとの衝突を無視する。
        // 自分のColliderを有効化した後に呼ぶ必要がある。
        collisionSystem.RefreshOwnerCollisionIgnores();

        // 既存仕様：Cubeなど、呼び出し側から渡されたColliderも無視する。
        if (ignoredColliders != null)
        {
            foreach (Collider other in ignoredColliders)
            {
                if (other == null)
                    continue;

                if (!other.enabled || !other.gameObject.activeInHierarchy)
                    continue;

                if (other == hitCollider)
                    continue;

                Physics.IgnoreCollision(hitCollider, other, true);

                Debug.Log(
                    $"Projectile : Ignore Collision With {other.name}",
                    other
                );
            }
        }

        trailRenderer.emitting = false;
        trailRenderer.Clear();
        trailRenderer.emitting = true;

        rb.linearVelocity = direction * speed;
    }

    private void Update()
    {
        if (!isFlying)
            return;

        flightTimer += Time.deltaTime;

        if (flightTimer >= maxFlightTime)
        {
            StopFlight();
        }
    }

    private void LateUpdate()
    {
        if (trailRenderer == null)
            return;

        float currentScale = GetScaleMagnitude(trailScaleSource.lossyScale);
        float scaleRatio = currentScale / initialObjectScale;

        trailRenderer.time = Mathf.Max(
            0.01f,
            initialTrailTime * scaleRatio
        );

        if (projectileVisual == null || !projectileVisual.activeInHierarchy)
            return;

        float currentVisualWidth = GetProjectileVisualWidth();

        if (currentVisualWidth <= 0.0001f)
            return;

        float headCurveValue = trailRenderer.widthCurve.Evaluate(0f);

        if (headCurveValue <= 0.0001f)
            return;

        trailRenderer.widthMultiplier = currentVisualWidth / headCurveValue;
    }

    private float GetScaleMagnitude(Vector3 scale)
    {
        return Mathf.Max(
            Mathf.Abs(scale.x),
            Mathf.Abs(scale.y),
            Mathf.Abs(scale.z)
        );
    }

    private float GetProjectileVisualWidth()
    {
        if (projectileRenderers == null || projectileRenderers.Length == 0)
            return 0f;

        bool hasBounds = false;
        Bounds combinedBounds = new Bounds();

        foreach (Renderer renderer in projectileRenderers)
        {
            if (renderer == null)
                continue;

            if (!hasBounds)
            {
                combinedBounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                combinedBounds.Encapsulate(renderer.bounds);
            }
        }

        if (!hasBounds)
            return 0f;

        return Mathf.Max(
            combinedBounds.size.x,
            combinedBounds.size.y,
            combinedBounds.size.z
        );
    }

    /// <summary>
    /// ProjectileCollisionSystemから衝突通知を受け取る。
    /// 実際の停止・軌跡のフェード待ち・破棄は既存のStopFlightに任せる。
    /// </summary>

    public void OnProjectileCollision(
        ProjectileCollisionInfo collisionInfo)
    {
        if (!isFlying)
            return;

        string otherName =
            collisionInfo.OtherObject != null
                ? collisionInfo.OtherObject.name
                : "Unknown";

        Debug.Log(
            $"Projectile Collision : {otherName}",
            this
        );

        ShieldSurface shield =
            collisionInfo.OtherCollider != null
                ? collisionInfo.OtherCollider
                    .GetComponentInParent<ShieldSurface>()
                : null;

        if (shield != null)
        {
            shield.ReceiveProjectileHit(
                shieldDamage,
                collisionInfo.Self
            );
        }

        StopFlight();
    }

    /// <summary>
    /// 弾の移動を停止し、軌跡が消えてから破棄する。
    /// </summary>
    private void StopFlight()
    {
        if (!isFlying)
            return;

        Debug.Log("Projectile : StopFlight Called", this);

        isFlying = false;

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.isKinematic = true;

        hitCollider.enabled = false;
        trailRenderer.emitting = false;

        if (projectileVisual != null)
            projectileVisual.SetActive(false);

        StartCoroutine(DestroyAfterTrailFade());
    }

    private IEnumerator DestroyAfterTrailFade()
    {
        while (trailRenderer != null && trailRenderer.positionCount > 0)
        {
            yield return null;
        }

        Destroy(gameObject);
    }
}
