using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
public class AsteroidCubePiece : MonoBehaviour
{
    // ==================================================
    // Inspector
    // ==================================================

    [Header("Floating Drift")]
    [SerializeField, Min(0f)]
    private float driftAmplitude = 0.012f;

    [SerializeField, Min(0f)]
    private float driftFrequency = 0.35f;

    [SerializeField, Min(0f)]
    private float hoverSpringStrength = 8f;

    [SerializeField, Min(0f)]
    private float hoverDamping = 3f;

    [SerializeField, Min(0f)]
    private float hoverLinearDamping = 0.15f;

    [SerializeField, Min(0f)]
    private float hoverAngularDamping = 1f;

    // ==================================================
    // Components
    // ==================================================

    private Rigidbody body;
    private AsteroidBulletController bulletController;

    // ==================================================
    // Original Rigidbody Settings
    // ==================================================

    private float originalLinearDamping;
    private float originalAngularDamping;

    private CollisionDetectionMode originalCollisionDetectionMode;
    private RigidbodyInterpolation originalInterpolation;

    private bool originalPhysicsCached;

    // ==================================================
    // Owner
    // ==================================================

    private bool ownerInitialized;

    // ==================================================
    // Following
    // ==================================================

    private Transform followTarget;

    // 手の回転に影響されないワールド空間の相対位置
    private Vector3 followWorldOffset;

    // 追従中の回転は初期状態を維持する
    private Quaternion followWorldRotation;

    private Vector3 followVelocity;

    private float followSmoothTime;

    private bool followOffsetAnimating;

    private Vector3 followOffsetStart;
    private Vector3 followOffsetTarget;

    private float followOffsetElapsed;
    private float followOffsetDelay;
    private float followOffsetDuration;

    // ==================================================
    // Floating / Scatter
    // ==================================================

    private bool scatterAnimating;
    private bool driftActive;

    private Vector3 hoverTargetPosition;

    private Vector3 scatterStartPosition;
    private Vector3 scatterTargetPosition;

    private float scatterElapsed;
    private float scatterDuration;

    private float driftElapsed;
    private Vector3 driftPhase;

    // ==================================================
    // Runtime State
    // ==================================================

    public bool IsFollowing { get; private set; }

    public bool IsFloating { get; private set; }

    public bool IsScattering => scatterAnimating;

    public bool LaunchFlag { get; private set; }

    public Vector3 LaunchDirection { get; private set; }

    public Rigidbody Body => body;

    public AsteroidBulletController BulletController =>
        bulletController;

    // ==================================================
    // Awake
    // ==================================================

    private void Awake()
    {
        body = GetComponent<Rigidbody>();

        bulletController =
            GetComponent<AsteroidBulletController>();

        CacheOriginalPhysics();

        if (bulletController == null)
        {
            Debug.LogError(
                "AsteroidCubePiece : AsteroidBulletController is missing.",
                this
            );
        }
    }

    private void CacheOriginalPhysics()
    {
        if (originalPhysicsCached || body == null)
            return;

        originalLinearDamping = body.linearDamping;
        originalAngularDamping = body.angularDamping;

        originalCollisionDetectionMode =
            body.collisionDetectionMode;

        originalInterpolation = body.interpolation;

        originalPhysicsCached = true;
    }

    private void RestoreOriginalDamping()
    {
        CacheOriginalPhysics();

        if (body == null)
            return;

        body.linearDamping = originalLinearDamping;
        body.angularDamping = originalAngularDamping;

        body.collisionDetectionMode =
            originalCollisionDetectionMode;

        body.interpolation = originalInterpolation;
    }

    // ==================================================
    // Owner Initialization
    // ==================================================

    public bool InitializeOwner(
        ulong ownerId,
        Transform ownerRoot)
    {
        if (ownerRoot == null)
        {
            Debug.LogError(
                "AsteroidCubePiece : Owner Root is missing.",
                this
            );

            return false;
        }

        if (bulletController == null)
        {
            bulletController =
                GetComponent<AsteroidBulletController>();
        }

        if (bulletController == null)
        {
            Debug.LogError(
                "AsteroidCubePiece : AsteroidBulletController is missing.",
                this
            );

            return false;
        }

        bulletController.InitializeOwner(
            ownerId,
            ownerRoot
        );

        ownerInitialized = true;

        return true;
    }

    // ==================================================
    // Begin Following
    // ==================================================

    /// <summary>
    /// 手の位置に追従する。
    /// 手の回転は追従位置に反映しない。
    /// </summary>
    public bool BeginFollowing(
        Transform target,
        float smoothTime,
        float rotationSpeed)
    {
        if (target == null)
        {
            Debug.LogError(
                "AsteroidCubePiece : Follow Target is missing.",
                this
            );

            return false;
        }

        if (body == null)
            body = GetComponent<Rigidbody>();

        if (body == null)
            return false;

        // ワールド空間で管理する
        transform.SetParent(null, true);

        StopScatter();
        StopFollowOffsetAnimation();

        followTarget = target;

        // 手の位置から見たワールド空間の相対位置
        // Transform.InverseTransformPointは使用しない。
        followWorldOffset =
            transform.position - target.position;

        // 手の回転に追従させず、現在のワールド回転を維持
        followWorldRotation = transform.rotation;

        followSmoothTime = Mathf.Max(0f, smoothTime);

        followVelocity = Vector3.zero;

        IsFollowing = true;
        IsFloating = false;

        RestoreOriginalDamping();

        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;

        body.isKinematic = true;
        body.useGravity = false;

        LaunchFlag = false;
        LaunchDirection = Vector3.zero;

        return true;
    }

    /// <summary>
    /// 旧呼び出しとの互換用。
    /// </summary>
    public bool BeginFollowing()
    {
        Transform target = transform.parent;

        if (target == null)
            return false;

        return BeginFollowing(
            target,
            0.12f,
            12f
        );
    }

    // ==================================================
    // Follow Offset Animation
    // ==================================================

    /// <summary>
    /// 手からのワールド空間相対位置をアニメーションで変更する。
    /// targetWorldOffsetは、手の位置からのワールド空間の差分。
    /// </summary>
    public void AnimateFollowOffset(
        Vector3 targetWorldOffset,
        float duration,
        float delay)
    {
        if (!IsFollowing || followTarget == null)
            return;

        followOffsetStart = followWorldOffset;
        followOffsetTarget = targetWorldOffset;

        followOffsetElapsed = 0f;
        followOffsetDelay = Mathf.Max(0f, delay);
        followOffsetDuration = Mathf.Max(0f, duration);

        if (followOffsetDuration <= 0f &&
            followOffsetDelay <= 0f)
        {
            followWorldOffset = followOffsetTarget;
            followOffsetAnimating = false;
            return;
        }

        followOffsetAnimating = true;
    }

    private void UpdateFollowOffsetAnimation(float deltaTime)
    {
        if (!followOffsetAnimating)
            return;

        if (followOffsetDelay > 0f)
        {
            followOffsetDelay -= deltaTime;
            return;
        }

        followOffsetElapsed += deltaTime;

        float t = followOffsetDuration <= 0f
            ? 1f
            : Mathf.Clamp01(
                followOffsetElapsed / followOffsetDuration
            );

        // SmoothStep
        float easedT = t * t * (3f - 2f * t);

        followWorldOffset = Vector3.Lerp(
            followOffsetStart,
            followOffsetTarget,
            easedT
        );

        if (t >= 1f)
        {
            followWorldOffset = followOffsetTarget;
            followOffsetAnimating = false;
        }
    }

    private void StopFollowOffsetAnimation()
    {
        followOffsetAnimating = false;

        followOffsetElapsed = 0f;
        followOffsetDelay = 0f;
        followOffsetDuration = 0f;
    }

    // ==================================================
    // Late Update
    // ==================================================

    private void LateUpdate()
    {
        if (!IsFollowing)
            return;

        if (followTarget == null)
        {
            DetachAndHold();
            return;
        }

        float deltaTime = Time.deltaTime;

        UpdateFollowOffsetAnimation(deltaTime);

        // 手の位置だけを追従する。
        // 手が回転しても、下方向や分割配置は回転しない。
        Vector3 targetPosition =
            followTarget.position + followWorldOffset;

        if (followSmoothTime <= 0f)
        {
            transform.position = targetPosition;
            followVelocity = Vector3.zero;
        }
        else
        {
            transform.position = Vector3.SmoothDamp(
                transform.position,
                targetPosition,
                ref followVelocity,
                followSmoothTime,
                Mathf.Infinity,
                deltaTime
            );
        }

        // 回転は手に追従させない
        transform.rotation = followWorldRotation;
    }

    // ==================================================
    // Detach / Hold
    // ==================================================

    /// <summary>
    /// 手への追従を解除し、その場で浮遊させる。
    /// </summary>
    public void DetachAndHold()
    {
        if (transform.parent != null)
        {
            transform.SetParent(null, true);
        }

        IsFollowing = false;
        IsFloating = true;

        followTarget = null;
        followVelocity = Vector3.zero;

        StopFollowOffsetAnimation();
        StopScatter();

        if (body == null)
            body = GetComponent<Rigidbody>();

        if (body == null)
            return;

        RestoreOriginalDamping();

        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;

        body.isKinematic = true;
        body.useGravity = false;
    }

    // ==================================================
    // Drop / Hold
    // ==================================================

    /// <summary>
    /// 現在位置に浮遊したまま保持する。
    /// </summary>
    public void DropAtCurrentPosition()
    {
        DetachAndHold();

        LaunchFlag = false;
        LaunchDirection = Vector3.zero;
    }

    // ==================================================
    // Scatter
    // ==================================================

    /// <summary>
    /// 散開先へ物理的に移動し、完了後も小さく漂う。
    /// RigidbodyはDynamic、UseGravityはOFF。
    /// </summary>
    public void Scatter(
        float positionRadius,
        float duration)
    {
        DetachAndHold();

        if (body == null)
            return;

        if (positionRadius <= 0f)
            return;

        // 有効な非Trigger Colliderを確認
        Collider[] colliders =
            GetComponentsInChildren<Collider>(true);

        bool hasSolidCollider = false;

        foreach (Collider col in colliders)
        {
            if (col != null &&
                col.enabled &&
                !col.isTrigger)
            {
                hasSolidCollider = true;
                break;
            }
        }

        if (!hasSolidCollider)
        {
            Debug.LogWarning(
                "AsteroidCubePiece : Scatter後に衝突で押せるようにするには、" +
                "有効なIs Trigger OFFのColliderが必要です。",
                this
            );
        }

        // 物理演算で押せるようDynamicにする
        body.isKinematic = false;
        body.useGravity = false;

        body.linearDamping = hoverLinearDamping;
        body.angularDamping = hoverAngularDamping;

        body.collisionDetectionMode =
            CollisionDetectionMode.ContinuousDynamic;

        body.interpolation =
            RigidbodyInterpolation.Interpolate;

        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;

        body.WakeUp();

        Vector3 currentPosition = body.position;

        scatterStartPosition = currentPosition;

        scatterTargetPosition =
            currentPosition +
            Random.insideUnitSphere * positionRadius;

        hoverTargetPosition = currentPosition;

        scatterElapsed = 0f;
        scatterDuration = Mathf.Max(0f, duration);

        // キューブごとに違う漂い方にする
        driftPhase = new Vector3(
            Random.Range(0f, Mathf.PI * 2f),
            Random.Range(0f, Mathf.PI * 2f),
            Random.Range(0f, Mathf.PI * 2f)
        );

        driftElapsed = Random.Range(0f, 10f);

        scatterAnimating = scatterDuration > 0f;
        driftActive = true;

        IsFollowing = false;
        IsFloating = true;

        followTarget = null;

        LaunchFlag = false;
        LaunchDirection = Vector3.zero;
    }

    private void FixedUpdate()
    {
        if (!driftActive ||
            body == null ||
            body.isKinematic)
        {
            return;
        }

        float deltaTime = Time.fixedDeltaTime;

        driftElapsed += deltaTime;

        // ----------------------------------------------
        // 散開中：目標位置を滑らかに移動させる
        // ----------------------------------------------

        if (scatterAnimating)
        {
            scatterElapsed += deltaTime;

            float t = scatterDuration <= 0f
                ? 1f
                : Mathf.Clamp01(
                    scatterElapsed / scatterDuration
                );

            // Ease Out Cubic
            float easedT =
                1f - Mathf.Pow(1f - t, 3f);

            hoverTargetPosition = Vector3.Lerp(
                scatterStartPosition,
                scatterTargetPosition,
                easedT
            );

            if (t >= 1f)
            {
                hoverTargetPosition =
                    scatterTargetPosition;

                scatterAnimating = false;
            }
        }

        // ----------------------------------------------
        // 散開後：小さく漂う目標位置を作る
        // ----------------------------------------------

        float angularFrequency =
            driftFrequency * Mathf.PI * 2f;

        Vector3 driftOffset = new Vector3(
            Mathf.Sin(
                driftElapsed * angularFrequency +
                driftPhase.x
            ),

            Mathf.Sin(
                driftElapsed * angularFrequency * 0.83f +
                driftPhase.y
            ),

            Mathf.Sin(
                driftElapsed * angularFrequency * 1.17f +
                driftPhase.z
            )
        ) * driftAmplitude;

        Vector3 desiredPosition =
            hoverTargetPosition + driftOffset;

        // ----------------------------------------------
        // 弱い復元力で目標位置へ近づける
        //
        // 衝突で押された場合は、その位置から
        // ゆっくり元の浮遊位置へ戻る。
        // ----------------------------------------------

        Vector3 positionError =
            desiredPosition - body.position;

        Vector3 acceleration =
            positionError * hoverSpringStrength -
            body.linearVelocity * hoverDamping;

        body.AddForce(
            acceleration,
            ForceMode.Acceleration
        );
    }

    private void StopScatter()
    {
        scatterAnimating = false;
        driftActive = false;

        scatterElapsed = 0f;
        scatterDuration = 0f;
        driftElapsed = 0f;
    }

    // ==================================================
    // Launch
    // ==================================================

    /// <summary>
    /// 追従中・浮遊中・散開中のどの状態からでも発射可能。
    /// </summary>
    public bool RequestLaunch(Vector3 direction)
    {
        if (LaunchFlag)
            return true;

        if (!ownerInitialized)
        {
            Debug.LogError(
                "AsteroidCubePiece : Owner is not initialized.",
                this
            );

            return false;
        }

        if (bulletController == null)
            return false;

        if (direction.sqrMagnitude < 0.0001f)
        {
            direction = transform.forward;
        }

        LaunchDirection = direction.normalized;

        // 追従・漂い・散開を停止
        DetachAndHold();

        LaunchFlag = true;

        bulletController.RequestFire(
            LaunchDirection
        );

        Debug.Log(
            $"AsteroidCubePiece : Launch Requested - {name}",
            this
        );

        return true;
    }
}