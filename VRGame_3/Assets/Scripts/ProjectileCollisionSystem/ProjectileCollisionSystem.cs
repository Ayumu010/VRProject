using System.Collections.Generic;
using System.Threading;
using UnityEngine;

/// <summary>
/// Projectile の物理衝突を検知し、同じOwnerのProjectile同士は
/// Physics.IgnoreCollisionで物理衝突自体を無効化する共通コンポーネント。
/// 衝突後の処理は同じGameObject上の IProjectileCollisionReceiver に委譲する。
///
/// この版は OnCollisionEnter を使用するため、ProjectileのColliderは Is Trigger OFF にする。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
public sealed class ProjectileCollisionSystem : MonoBehaviour
{
    private static readonly Dictionary<ulong, HashSet<ProjectileCollisionSystem>>
        ActiveProjectilesByOwner = new Dictionary<ulong, HashSet<ProjectileCollisionSystem>>();

    private readonly HashSet<long> handledProjectileSpawnIds = new HashSet<long>();
    private readonly HashSet<int> handledOtherColliderIds = new HashSet<int>();

    private static long nextSpawnId;
    private long spawnId;

    private IProjectileCollisionReceiver receiver;
    private Rigidbody ownRigidbody;
    private Collider[] childColliders;
    private Transform ownerRoot;
    private ulong ownerId;
    private bool isInitialized;
    private bool isRegistered;
    private bool warnedAboutUninitializedOwner;
    private bool warnedAboutMissingReceiver;

    [Header("Debug")]
    [SerializeField]
    private bool logIgnoredSameOwnerCollision;

    public bool IsInitialized => isInitialized;
    public ulong OwnerId => ownerId;

    private void Awake()
    {
        ownRigidbody = GetComponent<Rigidbody>();
        childColliders = GetComponentsInChildren<Collider>(true);
        CacheReceiver();
        ValidateSolidCollider();
    }

    private void OnEnable()
    {
        AssignNewSpawnId();
        ClearHandledContacts();
        warnedAboutUninitializedOwner = false;
    }

    private void OnDisable()
    {
        UnregisterFromOwnerRegistry();
        isInitialized = false;
        ownerId = default;
        ownerRoot = null;
        ClearHandledContacts();
    }

    /// <summary>
    /// 弾Prefabを生成した直後、Launchより前に必ず呼び出す。
    /// ownerIdは発射者ごとに一意で、同じプレイヤーの全弾に同じ値を渡す。
    /// ownerRootは発射者のルート。発射者自身のColliderも自動的に無視する。
    /// </summary>
    public void Initialize(ulong ownerId, Transform ownerRoot)
    {
        UnregisterFromOwnerRegistry();

        this.ownerId = ownerId;
        this.ownerRoot = ownerRoot;
        isInitialized = true;

        AssignNewSpawnId();
        ClearHandledContacts();
        warnedAboutUninitializedOwner = false;

        RegisterToOwnerRegistry();
    }

    /// <summary>
    /// ProjectileController.Launch内で、自分のColliderを有効化した直後に呼ぶ。
    /// Colliderが有効になったタイミングで、同じOwnerの既存弾とのIgnoreCollisionを設定する。
    /// </summary>
    public void RefreshOwnerCollisionIgnores()
    {
        if (!isInitialized)
        {
            Debug.LogError(
                "ProjectileCollisionSystem: Launch前にInitialize(ownerId, ownerRoot)を呼び出してください。",
                this);
            return;
        }

        RegisterToOwnerRegistry();
        IgnoreOwnerRootCollisions();

        if (!ActiveProjectilesByOwner.TryGetValue(ownerId, out HashSet<ProjectileCollisionSystem> sameOwnerProjectiles))
        {
            return;
        }

        foreach (ProjectileCollisionSystem other in sameOwnerProjectiles)
        {
            if (other == null || other == this || !other.isInitialized)
            {
                continue;
            }

            IgnoreProjectilePair(this, other);
        }
    }

    /// <summary>
    /// 既存の通常Colliderによる衝突を検知する。
    /// </summary>
    private void OnCollisionEnter(Collision collision)
    {
        if (collision == null || collision.contactCount == 0)
        {
            return;
        }

        ContactPoint contact = collision.GetContact(0);
        Collider thisCollider = contact.thisCollider;
        Collider otherCollider = contact.otherCollider;

        if (otherCollider == null)
        {
            otherCollider = collision.collider;
        }

        if (otherCollider == null)
        {
            return;
        }

        ProjectileCollisionSystem otherProjectile =
            otherCollider.GetComponentInParent<ProjectileCollisionSystem>();

        if (otherProjectile == this)
        {
            return;
        }

        if (otherProjectile != null)
        {
            // 同じOwnerの弾は、通常はLaunch時のIgnoreCollisionでここには来ない。
            // 初期化順などによる漏れに対する保険。
            if (isInitialized && otherProjectile.isInitialized && ownerId == otherProjectile.ownerId)
            {
                IgnoreProjectilePair(this, otherProjectile);

                if (logIgnoredSameOwnerCollision)
                {
                    Debug.Log(
                        $"ProjectileCollisionSystem: Same-owner collision ignored. OwnerId={ownerId}",
                        this);
                }

                return;
            }

            if (!isInitialized || !otherProjectile.isInitialized)
            {
                WarnAboutUninitializedOwner(otherProjectile);
                // Ownerが不明な弾同士を誤って同一扱いしない。
                // 通常の衝突として両方へ通知する。
            }

            NotifyProjectilePair(otherProjectile, thisCollider, otherCollider);
            return;
        }

        // ownerRoot内のColliderは通常RefreshOwnerCollisionIgnoresで事前に無視される。
        // 念のため接触した場合も通知しない。
        if (IsColliderUnderOwnerRoot(otherCollider.transform))
        {
            if (thisCollider != null && thisCollider.enabled && otherCollider.enabled)
            {
                Physics.IgnoreCollision(thisCollider, otherCollider, true);
            }

            return;
        }

        int otherColliderId = otherCollider.GetInstanceID();
        if (!handledOtherColliderIds.Add(otherColliderId))
        {
            return;
        }

        NotifyReceiver(new ProjectileCollisionInfo(this, otherCollider, null));
    }

    private void NotifyProjectilePair(
        ProjectileCollisionSystem otherProjectile,
        Collider thisCollider,
        Collider otherCollider)
    {
        // OnCollisionEnterは両方のRigidbody側で呼ばれるため、
        // 先に処理した側が双方の通知を行い、後続のcallbackはHashSetで抑止する。
        bool notifyThis = handledProjectileSpawnIds.Add(otherProjectile.spawnId);
        bool notifyOther = otherProjectile.handledProjectileSpawnIds.Add(spawnId);

        if (!notifyThis && !notifyOther)
        {
            return;
        }

        if (notifyThis)
        {
            NotifyReceiver(
                new ProjectileCollisionInfo(this, otherCollider, otherProjectile));
        }

        if (notifyOther)
        {
            otherProjectile.NotifyReceiver(
                new ProjectileCollisionInfo(otherProjectile, thisCollider, this));
        }
    }

    private void NotifyReceiver(ProjectileCollisionInfo collisionInfo)
    {
        if (receiver == null)
        {
            if (!warnedAboutMissingReceiver)
            {
                Debug.LogError(
                    "ProjectileCollisionSystem: 同じGameObjectにIProjectileCollisionReceiverの実装がありません。",
                    this);
                warnedAboutMissingReceiver = true;
            }

            return;
        }

        receiver.OnProjectileCollision(collisionInfo);
    }

    private void CacheReceiver()
    {
        MonoBehaviour[] components = GetComponents<MonoBehaviour>();

        for (int i = 0; i < components.Length; i++)
        {
            if (components[i] is IProjectileCollisionReceiver foundReceiver)
            {
                receiver = foundReceiver;
                return;
            }
        }

        Debug.LogError(
            "ProjectileCollisionSystem: 同じGameObjectにIProjectileCollisionReceiverの実装を追加してください。",
            this);
        warnedAboutMissingReceiver = true;
    }

    private void ValidateSolidCollider()
    {
        if (childColliders == null)
        {
            return;
        }

        for (int i = 0; i < childColliders.Length; i++)
        {
            Collider candidate = childColliders[i];
            if (candidate != null && !candidate.isTrigger)
            {
                return;
            }
        }

        Debug.LogError(
            "ProjectileCollisionSystem: OnCollisionEnterを使用するため、Is Trigger OFFのColliderが必要です。",
            this);
    }

    private void RegisterToOwnerRegistry()
    {
        if (!isInitialized || isRegistered)
        {
            return;
        }

        if (!ActiveProjectilesByOwner.TryGetValue(ownerId, out HashSet<ProjectileCollisionSystem> projectiles))
        {
            projectiles = new HashSet<ProjectileCollisionSystem>();
            ActiveProjectilesByOwner.Add(ownerId, projectiles);
        }

        projectiles.Add(this);
        isRegistered = true;
    }

    private void UnregisterFromOwnerRegistry()
    {
        if (!isRegistered)
        {
            return;
        }

        if (ActiveProjectilesByOwner.TryGetValue(ownerId, out HashSet<ProjectileCollisionSystem> projectiles))
        {
            projectiles.Remove(this);

            if (projectiles.Count == 0)
            {
                ActiveProjectilesByOwner.Remove(ownerId);
            }
        }

        isRegistered = false;
    }

    private static void IgnoreProjectilePair(
        ProjectileCollisionSystem first,
        ProjectileCollisionSystem second)
    {
        if (first == null || second == null)
        {
            return;
        }

        Collider[] firstColliders = first.GetActiveSolidColliders();
        Collider[] secondColliders = second.GetActiveSolidColliders();

        for (int i = 0; i < firstColliders.Length; i++)
        {
            for (int j = 0; j < secondColliders.Length; j++)
            {
                Collider a = firstColliders[i];
                Collider b = secondColliders[j];

                if (a == null || b == null || a == b)
                {
                    continue;
                }

                Physics.IgnoreCollision(a, b, true);
            }
        }
    }

    private Collider[] GetActiveSolidColliders()
    {
        if (childColliders == null)
        {
            return new Collider[0];
        }

        List<Collider> result = new List<Collider>(childColliders.Length);

        for (int i = 0; i < childColliders.Length; i++)
        {
            Collider candidate = childColliders[i];

            if (candidate == null || candidate.isTrigger || !candidate.enabled ||
                !candidate.gameObject.activeInHierarchy)
            {
                continue;
            }

            // このProjectileのRigidbodyに属するColliderだけを対象にする。
            if (candidate.attachedRigidbody != ownRigidbody)
            {
                continue;
            }

            result.Add(candidate);
        }

        return result.ToArray();
    }

    private void IgnoreOwnerRootCollisions()
    {
        if (ownerRoot == null)
        {
            return;
        }

        Collider[] ownerColliders = ownerRoot.GetComponentsInChildren<Collider>(true);
        Collider[] projectileColliders = GetActiveSolidColliders();

        for (int i = 0; i < projectileColliders.Length; i++)
        {
            Collider projectileCollider = projectileColliders[i];

            for (int j = 0; j < ownerColliders.Length; j++)
            {
                Collider ownerCollider = ownerColliders[j];

                if (ownerCollider == null || !ownerCollider.enabled ||
                    !ownerCollider.gameObject.activeInHierarchy ||
                    ownerCollider == projectileCollider ||
                    ownerCollider.GetComponentInParent<ProjectileCollisionSystem>() == this)
                {
                    continue;
                }

                Physics.IgnoreCollision(projectileCollider, ownerCollider, true);
            }
        }
    }

    private bool IsColliderUnderOwnerRoot(Transform target)
    {
        if (ownerRoot == null || target == null)
        {
            return false;
        }

        return target == ownerRoot || target.IsChildOf(ownerRoot);
    }

    private void WarnAboutUninitializedOwner(ProjectileCollisionSystem otherProjectile)
    {
        if (warnedAboutUninitializedOwner)
        {
            return;
        }

        Debug.LogError(
            "ProjectileCollisionSystem: 弾同士が接触しましたが、どちらかのOwner IDが未設定です。" +
            " Instantiate直後、Launch前に両方の弾へInitialize(ownerId, ownerRoot)を呼び出してください。" +
            $" Self initialized: {isInitialized}, Other initialized: {otherProjectile.isInitialized}",
            this);

        warnedAboutUninitializedOwner = true;
    }

    private void AssignNewSpawnId()
    {
        spawnId = Interlocked.Increment(ref nextSpawnId);
    }

    private void ClearHandledContacts()
    {
        handledProjectileSpawnIds.Clear();
        handledOtherColliderIds.Clear();
    }
}
