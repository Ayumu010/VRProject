using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Transformers;

/// <summary>
/// Kogetsu専用の衝突制限Grab Transformer。
/// XRGeneralGrabTransformerが計算したPoseを受け取り、武器のBox/Capsule/Sphere Colliderで
/// 移動・回転を検査してからtargetPoseへ返す。
/// </summary>
[DisallowMultipleComponent]
public sealed class KogetsuCollisionGrabTransformer : XRBaseGrabTransformer
{
    public enum CollisionState
    {
        Free,
        Blocked,
        Sliding,
        Recovering
    }

    private enum ShapeKind
    {
        Box,
        Sphere,
        Capsule
    }

    private struct ShapeData
    {
        public ShapeKind kind;
        public Vector3 colliderPosition;
        public Quaternion rotation;
        public Vector3 center;
        public Vector3 halfExtents;
        public Vector3 pointA;
        public Vector3 pointB;
        public float radius;
    }

    [Header("References")]
    [SerializeField]
    private XRGrabInteractable grabInteractable;

    [SerializeField]
    private Rigidbody weaponBody;

    [Tooltip("物理的な移動制限に使用するCollider。掴み判定用grabInteractable.collidersとは別に設定する。")]
    [SerializeField]
    private Collider[] collisionColliders;

    [Header("Collision Filtering")]
    [SerializeField]
    private LayerMask blockingLayers = ~0;

    [SerializeField]
    private Collider[] ignoredColliders;

    [Header("Contact")]
    [SerializeField, Min(0f)]
    private float skinWidth = 0.005f;

    [SerializeField, Min(0f)]
    private float contactReleaseMargin = 0.01f;

    [SerializeField]
    private bool enableSurfaceSliding = true;

    [SerializeField, Range(1, 8)]
    private int maximumSlideIterations = 3;

    [Header("Sweep Resolution")]
    [SerializeField, Min(0.001f)]
    private float maximumLinearStep = 0.02f;

    [SerializeField, Range(0.5f, 45f)]
    private float maximumAngularStep = 5f;

    [SerializeField, Range(1, 32)]
    private int maximumSubsteps = 12;

    [Header("Rotation / Recovery")]
    [SerializeField]
    private bool enableRotationBlocking = true;

    [SerializeField]
    private bool enablePenetrationRecovery = true;

    [SerializeField, Min(0.001f)]
    private float maximumRecoveryDistancePerFrame = 0.05f;

    [SerializeField, Range(1, 8)]
    private int maximumRecoveryIterations = 4;

    [SerializeField, Range(2, 12)]
    private int rotationBinarySearchIterations = 6;

    [Header("Debug")]
    [SerializeField]
    private bool debugDraw;

    private const int QueryBufferSize = 64;
    private readonly Collider[] overlapBuffer = new Collider[QueryBufferSize];
    private readonly RaycastHit[] castBuffer = new RaycastHit[QueryBufferSize];

    private Transform ignoredPlayerRoot;
    private Transform ignoredScabbardRoot;

    private Pose resolvedPose;
    private Pose lastSafePose;
    private bool hasResolvedPose;

    private CollisionState state = CollisionState.Free;
    private Collider contactCollider;
    private Vector3 contactNormal;
    private bool contactWasSliding;

    private bool collisionThisPass;
    private bool slidingThisPass;
    private bool warnedUnsupportedCollider;
    private bool warnedCastBufferOverflow;
    private bool warnedOverlapBufferOverflow;
    private bool warnedNonUnitRootScale;

    // DynamicとOnBeforeRenderが同じフレーム内で呼ばれるため、両Phase合計の上限を管理する。
    private int budgetFrame = -1;
    private int recoveryHandledFrame = -1;
    private float linearBudgetUsed;
    private float angularBudgetUsed;
    private float recoveryBudgetUsed;

    public CollisionState State => state;
    public bool IsCollisionBlocked => state != CollisionState.Free;
    public Vector3 ContactNormal => contactNormal;
    public Collider ContactCollider => contactCollider;
    public Pose ResolvedPose => hasResolvedPose ? resolvedPose : new Pose(transform.position, transform.rotation);
    public Pose LastSafePose => lastSafePose;

    // XRGrabInteractableのSingle Grab Transformerとして使用する。
    protected override RegistrationMode registrationMode => RegistrationMode.Single;

    private void Awake()
    {
        CacheReferences();
    }

    public override void OnLink(XRGrabInteractable linkedGrabInteractable)
    {
        base.OnLink(linkedGrabInteractable);

        if (grabInteractable == null)
            grabInteractable = linkedGrabInteractable;

        CacheReferences();
    }

    public override void OnGrab(XRGrabInteractable linkedGrabInteractable)
    {
        base.OnGrab(linkedGrabInteractable);
        CacheReferences();
        ResetCollisionState();

        resolvedPose = new Pose(linkedGrabInteractable.transform.position, linkedGrabInteractable.transform.rotation);
        lastSafePose = resolvedPose;
        hasResolvedPose = true;
    }

    public override void OnGrabCountChanged(
        XRGrabInteractable linkedGrabInteractable,
        Pose targetPose,
        Vector3 localScale)
    {
        base.OnGrabCountChanged(linkedGrabInteractable, targetPose, localScale);

        // 弧月はSingle Selectを前提とするが、保持Interactorが切り替わった場合に古い接触状態を残さない。
        if (linkedGrabInteractable.interactorsSelecting.Count == 1)
        {
            ResetCollisionState();
            resolvedPose = new Pose(linkedGrabInteractable.transform.position, linkedGrabInteractable.transform.rotation);
            lastSafePose = resolvedPose;
            hasResolvedPose = true;
        }
    }

    public override void OnUnlink(XRGrabInteractable unlinkedGrabInteractable)
    {
        ResetCollisionState();
        base.OnUnlink(unlinkedGrabInteractable);
    }

    /// <summary>
    /// Managerからプレイヤーと鞘のルートを受け取る。
    /// これらは毎フレーム検索せず、対象Colliderを衝突クエリから除外する。
    /// </summary>
    public void SetIgnoredRoots(Transform playerRoot, Transform scabbardRoot)
    {
        ignoredPlayerRoot = playerRoot;
        ignoredScabbardRoot = scabbardRoot;
    }

    /// <summary>
    /// 納刀・手放し・再取得時に接触状態を初期化する。
    /// </summary>
    public void ResetCollisionState()
    {
        hasResolvedPose = false;
        resolvedPose = new Pose(transform.position, transform.rotation);
        lastSafePose = resolvedPose;
        state = CollisionState.Free;
        contactCollider = null;
        contactNormal = Vector3.zero;
        contactWasSliding = false;
        collisionThisPass = false;
        slidingThisPass = false;
        budgetFrame = -1;
        recoveryHandledFrame = -1;
        linearBudgetUsed = 0f;
        angularBudgetUsed = 0f;
        recoveryBudgetUsed = 0f;
    }

    public override void Process(
        XRGrabInteractable linkedGrabInteractable,
        XRInteractionUpdateOrder.UpdatePhase updatePhase,
        ref Pose targetPose,
        ref Vector3 localScale)
    {
        if (updatePhase != XRInteractionUpdateOrder.UpdatePhase.Dynamic &&
            updatePhase != XRInteractionUpdateOrder.UpdatePhase.OnBeforeRender)
        {
            return;
        }

        CacheReferences();

        if (linkedGrabInteractable == null || linkedGrabInteractable.interactorsSelecting.Count == 0)
            return;

        // XRSocketInteractorの納刀PoseはSocket側の標準処理を優先する。
        if (linkedGrabInteractable.interactorsSelecting[0] is XRSocketInteractor)
            return;

        ResetFrameBudgetsIfNeeded();

        // 同一フレームのDynamicで復旧した場合、OnBeforeRenderで通常追従を再開させない。
        if (recoveryHandledFrame == Time.frameCount)
        {
            if (hasResolvedPose)
                targetPose = resolvedPose;
            return;
        }

        if (!hasResolvedPose)
        {
            resolvedPose = new Pose(linkedGrabInteractable.transform.position, linkedGrabInteractable.transform.rotation);
            lastSafePose = resolvedPose;
            hasResolvedPose = true;
        }

        Pose workingPose = resolvedPose;

        // Colliderを有効化した瞬間など、現在位置がすでに重なっている場合は先に段階的に復旧する。
        if (enablePenetrationRecovery && TryRecoverExistingPenetration(ref workingPose))
        {
            resolvedPose = workingPose;
            targetPose = resolvedPose;
            recoveryHandledFrame = Time.frameCount;
            return;
        }

        collisionThisPass = false;
        slidingThisPass = false;

        Pose startPose = workingPose;
        Vector3 requestedDelta = targetPose.position - startPose.position;
        float remainingLinearBudget = Mathf.Max(
            0f,
            maximumLinearStep * maximumSubsteps - linearBudgetUsed);
        float remainingAngularBudget = Mathf.Max(
            0f,
            maximumAngularStep * maximumSubsteps - angularBudgetUsed);

        // 大きなフレーム落ちや瞬間移動時にサンプル上限を超えて一気に移動しない。
        // Dynamic / OnBeforeRenderの両方を合算したフレーム上限にする。
        Vector3 limitedTargetPosition = startPose.position +
            Vector3.ClampMagnitude(requestedDelta, remainingLinearBudget);
        Quaternion limitedTargetRotation = Quaternion.RotateTowards(
            startPose.rotation,
            targetPose.rotation,
            remainingAngularBudget);

        float limitedDistance = Vector3.Distance(startPose.position, limitedTargetPosition);
        float limitedAngle = Quaternion.Angle(startPose.rotation, limitedTargetRotation);
        if (limitedDistance <= 0.0001f && limitedAngle <= 0.001f)
        {
            if (!HasPreviousContactWithinReleaseMargin(startPose))
            {
                state = CollisionState.Free;
                contactCollider = null;
                contactNormal = Vector3.zero;
                contactWasSliding = false;
            }

            targetPose = resolvedPose;
            return;
        }

        int requestedSubsteps = Mathf.Max(
            Mathf.CeilToInt(limitedDistance / maximumLinearStep),
            Mathf.CeilToInt(limitedAngle / maximumAngularStep));
        int substeps = Mathf.Clamp(requestedSubsteps, 1, maximumSubsteps);

        for (int step = 1; step <= substeps; step++)
        {
            float t = step / (float)substeps;
            Vector3 substepPosition = Vector3.Lerp(startPose.position, limitedTargetPosition, t);
            Quaternion substepRotation = Quaternion.Slerp(startPose.rotation, limitedTargetRotation, t);

            SolveLinearSegment(ref workingPose, substepPosition);

            if (enableRotationBlocking)
                SolveRotationSegment(ref workingPose, substepRotation);
            else
                workingPose.rotation = substepRotation;
        }

        resolvedPose = workingPose;

        linearBudgetUsed += Vector3.Distance(startPose.position, resolvedPose.position);
        angularBudgetUsed += Quaternion.Angle(startPose.rotation, resolvedPose.rotation);

        if (collisionThisPass)
        {
            state = slidingThisPass ? CollisionState.Sliding : CollisionState.Blocked;
            contactWasSliding = slidingThisPass;
        }
        else if (HasPreviousContactWithinReleaseMargin(workingPose))
        {
            state = contactWasSliding ? CollisionState.Sliding : CollisionState.Blocked;
        }
        else
        {
            state = CollisionState.Free;
            contactCollider = null;
            contactNormal = Vector3.zero;
            contactWasSliding = false;
        }

        // ここで返したPoseがXRIの最終目標となる。
        // XRIのThrow SmoothingもTransformer処理後のPoseを使う。
        targetPose = resolvedPose;

        // Castと回転サンプルで安全確認済みのPoseを保存する。
        if (state != CollisionState.Recovering)
            lastSafePose = resolvedPose;
    }

    private void ResetFrameBudgetsIfNeeded()
    {
        if (budgetFrame == Time.frameCount)
            return;

        budgetFrame = Time.frameCount;
        linearBudgetUsed = 0f;
        angularBudgetUsed = 0f;
        recoveryBudgetUsed = 0f;
    }

    private void CacheReferences()
    {
        if (grabInteractable == null)
            grabInteractable = GetComponent<XRGrabInteractable>();

        if (weaponBody == null)
            weaponBody = GetComponent<Rigidbody>();

        if (collisionColliders == null || collisionColliders.Length == 0)
            collisionColliders = GetComponentsInChildren<Collider>(true);

        if (!warnedNonUnitRootScale)
        {
            Vector3 scale = transform.lossyScale;
            if (Mathf.Abs(scale.x - 1f) > 0.001f ||
                Mathf.Abs(scale.y - 1f) > 0.001f ||
                Mathf.Abs(scale.z - 1f) > 0.001f)
            {
                Debug.LogWarning(
                    "KogetsuCollisionGrabTransformer: Weapon root scale should be (1,1,1). " +
                    "Non-unit root scale with rotated child colliders may not match primitive cast shapes exactly.",
                    this);
                warnedNonUnitRootScale = true;
            }
        }
    }

    private void SolveLinearSegment(ref Pose pose, Vector3 targetPosition)
    {
        Vector3 remainingTarget = targetPosition;

        for (int iteration = 0; iteration < maximumSlideIterations; iteration++)
        {
            Vector3 delta = remainingTarget - pose.position;
            float distance = delta.magnitude;
            if (distance <= 0.0001f)
                return;

            Vector3 direction = delta / distance;
            if (!TryGetNearestCastHit(
                    pose,
                    direction,
                    distance,
                    out float hitDistance,
                    out Vector3 hitNormal,
                    out Collider hitCollider))
            {
                pose.position = remainingTarget;
                return;
            }

            collisionThisPass = true;
            RecordContact(hitCollider, hitNormal, -direction);

            float safeDistance = Mathf.Max(0f, hitDistance - skinWidth);
            pose.position += direction * safeDistance;

            Vector3 remainingMovement = remainingTarget - pose.position;
            if (!enableSurfaceSliding || remainingMovement.sqrMagnitude <= 0.000001f)
                return;

            Vector3 projectedMovement = Vector3.ProjectOnPlane(remainingMovement, contactNormal);
            if (projectedMovement.sqrMagnitude <= 0.000001f)
                return;

            if (projectedMovement.sqrMagnitude < remainingMovement.sqrMagnitude * 0.99f)
                slidingThisPass = true;

            remainingTarget = pose.position + projectedMovement;
        }
    }

    private void SolveRotationSegment(ref Pose pose, Quaternion targetRotation)
    {
        float totalAngle = Quaternion.Angle(pose.rotation, targetRotation);
        if (totalAngle <= 0.001f)
            return;

        Quaternion startRotation = pose.rotation;
        int steps = Mathf.Max(1, Mathf.CeilToInt(totalAngle / maximumAngularStep));

        for (int step = 1; step <= steps; step++)
        {
            float t = step / (float)steps;
            Quaternion candidateRotation = Quaternion.Slerp(startRotation, targetRotation, t);
            Pose candidatePose = new Pose(pose.position, candidateRotation);

            if (TryFindDeepestPenetration(
                    candidatePose,
                    out Vector3 penetrationDirection,
                    out _,
                    out Collider penetrationCollider,
                    out _))
            {
                float low = (step - 1) / (float)steps;
                float high = step / (float)steps;

                for (int search = 0; search < rotationBinarySearchIterations; search++)
                {
                    float middle = (low + high) * 0.5f;
                    Quaternion middleRotation = Quaternion.Slerp(startRotation, targetRotation, middle);
                    Pose middlePose = new Pose(pose.position, middleRotation);

                    if (TryFindDeepestPenetration(
                            middlePose,
                            out _,
                            out _,
                            out _,
                            out _))
                    {
                        high = middle;
                    }
                    else
                    {
                        low = middle;
                    }
                }

                pose.rotation = Quaternion.Slerp(startRotation, targetRotation, low);
                collisionThisPass = true;
                RecordContact(penetrationCollider, penetrationDirection, -pose.rotation * Vector3.forward);
                return;
            }

            pose.rotation = candidateRotation;
        }
    }

    private bool TryGetNearestCastHit(
        Pose rootPose,
        Vector3 direction,
        float maxDistance,
        out float nearestDistance,
        out Vector3 nearestNormal,
        out Collider nearestCollider)
    {
        bool found = false;
        bool bufferOverflow = false;
        nearestDistance = maxDistance;
        nearestNormal = -direction;
        nearestCollider = null;

        if (collisionColliders == null)
            return false;

        for (int i = 0; i < collisionColliders.Length; i++)
        {
            Collider movingCollider = collisionColliders[i];
            if (!IsUsableMovingCollider(movingCollider))
                continue;

            if (!TryBuildShape(movingCollider, rootPose, 0f, out ShapeData shape))
                continue;

            int hitCount = CastShape(shape, direction, maxDistance);
            if (hitCount >= castBuffer.Length)
                bufferOverflow = true;

            for (int hitIndex = 0; hitIndex < hitCount; hitIndex++)
            {
                RaycastHit hit = castBuffer[hitIndex];
                if (ShouldIgnore(movingCollider, hit.collider))
                    continue;

                if (!found || hit.distance < nearestDistance)
                {
                    found = true;
                    nearestDistance = hit.distance;
                    nearestNormal = hit.normal.sqrMagnitude > 0.0001f ? hit.normal.normalized : -direction;
                    nearestCollider = hit.collider;
                }
            }
        }

        // NonAllocバッファが満杯なら、未取得の最短Hitが存在する可能性がある。
        // すり抜けを優先しないよう、その場で停止する保守的な処理にする。
        if (bufferOverflow)
        {
            if (!warnedCastBufferOverflow)
            {
                Debug.LogWarning(
                    "KogetsuCollisionGrabTransformer: Cast result buffer was full. Movement was conservatively blocked. " +
                    "Increase QueryBufferSize if this occurs repeatedly.",
                    this);
                warnedCastBufferOverflow = true;
            }

            nearestDistance = 0f;
            nearestNormal = -direction;
            return true;
        }

        return found;
    }

    private int CastShape(ShapeData shape, Vector3 direction, float maxDistance)
    {
        switch (shape.kind)
        {
            case ShapeKind.Box:
                return Physics.BoxCastNonAlloc(
                    shape.center,
                    shape.halfExtents,
                    direction,
                    castBuffer,
                    shape.rotation,
                    maxDistance,
                    blockingLayers.value,
                    QueryTriggerInteraction.Ignore);

            case ShapeKind.Sphere:
                return Physics.SphereCastNonAlloc(
                    shape.center,
                    shape.radius,
                    direction,
                    castBuffer,
                    maxDistance,
                    blockingLayers.value,
                    QueryTriggerInteraction.Ignore);

            case ShapeKind.Capsule:
                return Physics.CapsuleCastNonAlloc(
                    shape.pointA,
                    shape.pointB,
                    shape.radius,
                    direction,
                    castBuffer,
                    maxDistance,
                    blockingLayers.value,
                    QueryTriggerInteraction.Ignore);

            default:
                return 0;
        }
    }

    private bool TryRecoverExistingPenetration(ref Pose pose)
    {
        bool moved = false;
        float remainingRecovery = Mathf.Max(0f, maximumRecoveryDistancePerFrame - recoveryBudgetUsed);

        for (int iteration = 0; iteration < maximumRecoveryIterations; iteration++)
        {
            if (!TryFindDeepestPenetration(
                    pose,
                    out Vector3 direction,
                    out float distance,
                    out Collider blocker,
                    out bool queryOverflow))
            {
                if (moved)
                {
                    state = CollisionState.Free;
                    contactCollider = null;
                    contactNormal = Vector3.zero;
                    contactWasSliding = false;
                    lastSafePose = pose;
                }

                return moved;
            }

            if (queryOverflow && (blocker == null || direction.sqrMagnitude <= 0.0001f || distance <= 0f))
            {
                state = CollisionState.Recovering;
                RecordContact(blocker, direction, Vector3.up);
                return true;
            }

            if (direction.sqrMagnitude <= 0.0001f || remainingRecovery <= 0.0001f)
            {
                state = CollisionState.Recovering;
                RecordContact(blocker, direction, Vector3.up);
                return true;
            }

            float correction = Mathf.Min(distance + skinWidth, remainingRecovery);
            pose.position += direction.normalized * correction;
            remainingRecovery -= correction;
            recoveryBudgetUsed += correction;
            moved = true;
            RecordContact(blocker, direction, Vector3.up);

            if (remainingRecovery <= 0.0001f)
                break;
        }

        state = CollisionState.Recovering;
        return true;
    }

    private bool TryFindDeepestPenetration(
        Pose rootPose,
        out Vector3 deepestDirection,
        out float deepestDistance,
        out Collider deepestCollider,
        out bool bufferOverflow)
    {
        deepestDirection = Vector3.zero;
        deepestDistance = 0f;
        deepestCollider = null;
        bufferOverflow = false;

        if (collisionColliders == null)
            return false;

        for (int movingIndex = 0; movingIndex < collisionColliders.Length; movingIndex++)
        {
            Collider movingCollider = collisionColliders[movingIndex];
            if (!IsUsableMovingCollider(movingCollider))
                continue;

            if (!TryBuildShape(movingCollider, rootPose, 0f, out ShapeData shape))
                continue;

            int overlapCount = OverlapShape(shape);
            if (overlapCount >= overlapBuffer.Length)
                bufferOverflow = true;

            for (int overlapIndex = 0; overlapIndex < overlapCount; overlapIndex++)
            {
                Collider other = overlapBuffer[overlapIndex];
                if (ShouldIgnore(movingCollider, other))
                    continue;

                if (Physics.ComputePenetration(
                        movingCollider,
                        shape.colliderPosition,
                        shape.rotation,
                        other,
                        other.transform.position,
                        other.transform.rotation,
                        out Vector3 direction,
                        out float distance))
                {
                    if (distance > deepestDistance)
                    {
                        deepestDistance = distance;
                        deepestDirection = direction;
                        deepestCollider = other;
                    }
                }
            }
        }

        if (bufferOverflow && !warnedOverlapBufferOverflow)
        {
            Debug.LogWarning(
                "KogetsuCollisionGrabTransformer: Overlap result buffer was full. " +
                "Penetration recovery will stop until the query result count is reduced.",
                this);
            warnedOverlapBufferOverflow = true;
        }

        // バッファが満杯で、取得済みColliderからMTDを計算できなかった場合も、
        // 未取得Colliderへの侵入を避けるためpenetration扱いにする。
        return deepestCollider != null || bufferOverflow;
    }

    private int OverlapShape(ShapeData shape)
    {
        switch (shape.kind)
        {
            case ShapeKind.Box:
                return Physics.OverlapBoxNonAlloc(
                    shape.center,
                    shape.halfExtents,
                    overlapBuffer,
                    shape.rotation,
                    blockingLayers.value,
                    QueryTriggerInteraction.Ignore);

            case ShapeKind.Sphere:
                return Physics.OverlapSphereNonAlloc(
                    shape.center,
                    shape.radius,
                    overlapBuffer,
                    blockingLayers.value,
                    QueryTriggerInteraction.Ignore);

            case ShapeKind.Capsule:
                return Physics.OverlapCapsuleNonAlloc(
                    shape.pointA,
                    shape.pointB,
                    shape.radius,
                    overlapBuffer,
                    blockingLayers.value,
                    QueryTriggerInteraction.Ignore);

            default:
                return 0;
        }
    }

    private bool HasPreviousContactWithinReleaseMargin(Pose rootPose)
    {
        if (contactCollider == null ||
            !contactCollider.enabled ||
            !contactCollider.gameObject.activeInHierarchy)
        {
            return false;
        }

        for (int i = 0; i < collisionColliders.Length; i++)
        {
            Collider movingCollider = collisionColliders[i];
            if (!IsUsableMovingCollider(movingCollider))
                continue;

            if (!TryBuildShape(movingCollider, rootPose, contactReleaseMargin, out ShapeData expandedShape))
                continue;

            int count = OverlapShape(expandedShape);
            if (count >= overlapBuffer.Length)
                return true;

            for (int j = 0; j < count; j++)
            {
                if (overlapBuffer[j] == contactCollider &&
                    !ShouldIgnore(movingCollider, contactCollider))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private bool TryBuildShape(Collider collider, Pose rootPose, float margin, out ShapeData shape)
    {
        shape = default;
        if (collider == null)
            return false;

        Transform colliderTransform = collider.transform;
        Vector3 rootScale = transform.lossyScale;
        Vector3 localOrigin = transform.InverseTransformPoint(colliderTransform.position);

        shape.colliderPosition = rootPose.position +
            rootPose.rotation * Vector3.Scale(localOrigin, rootScale);
        Quaternion localRotation = Quaternion.Inverse(transform.rotation) * colliderTransform.rotation;
        shape.rotation = rootPose.rotation * localRotation;

        Vector3 signedScale = colliderTransform.lossyScale;
        Vector3 absoluteScale = Abs(signedScale);

        if (collider is BoxCollider box)
        {
            shape.kind = ShapeKind.Box;
            shape.center = shape.colliderPosition +
                shape.rotation * Vector3.Scale(box.center, signedScale);
            shape.halfExtents = Vector3.Scale(box.size * 0.5f, absoluteScale) +
                Vector3.one * margin;
            return true;
        }

        if (collider is SphereCollider sphere)
        {
            shape.kind = ShapeKind.Sphere;
            shape.center = shape.colliderPosition +
                shape.rotation * Vector3.Scale(sphere.center, signedScale);
            float scale = Mathf.Max(absoluteScale.x, Mathf.Max(absoluteScale.y, absoluteScale.z));
            shape.radius = sphere.radius * scale + margin;
            return true;
        }

        if (collider is CapsuleCollider capsule)
        {
            shape.kind = ShapeKind.Capsule;
            shape.center = shape.colliderPosition +
                shape.rotation * Vector3.Scale(capsule.center, signedScale);

            Vector3 localAxis;
            float axisScale;
            float perpendicularScale;
            switch (capsule.direction)
            {
                case 0:
                    localAxis = Vector3.right;
                    axisScale = absoluteScale.x;
                    perpendicularScale = Mathf.Max(absoluteScale.y, absoluteScale.z);
                    break;
                case 1:
                    localAxis = Vector3.up;
                    axisScale = absoluteScale.y;
                    perpendicularScale = Mathf.Max(absoluteScale.x, absoluteScale.z);
                    break;
                default:
                    localAxis = Vector3.forward;
                    axisScale = absoluteScale.z;
                    perpendicularScale = Mathf.Max(absoluteScale.x, absoluteScale.y);
                    break;
            }

            float originalRadius = capsule.radius * perpendicularScale;
            float worldHeight = Mathf.Max(capsule.height * axisScale, originalRadius * 2f);
            float halfSegment = Mathf.Max(0f, worldHeight * 0.5f - originalRadius);
            Vector3 worldAxis = shape.rotation * localAxis;

            shape.pointA = shape.center - worldAxis * halfSegment;
            shape.pointB = shape.center + worldAxis * halfSegment;
            shape.radius = originalRadius + margin;
            return true;
        }

        if (!warnedUnsupportedCollider)
        {
            Debug.LogWarning(
                "KogetsuCollisionGrabTransformer: Only BoxCollider, SphereCollider, and CapsuleCollider " +
                "are supported as moving collision shapes. Unsupported collider: " + collider.GetType().Name,
                collider);
            warnedUnsupportedCollider = true;
        }

        return false;
    }

    private bool IsUsableMovingCollider(Collider collider)
    {
        return collider != null &&
               collider.enabled &&
               collider.gameObject.activeInHierarchy &&
               !collider.isTrigger;
    }

    private bool ShouldIgnore(Collider movingCollider, Collider other)
    {
        if (other == null ||
            other == movingCollider ||
            !other.enabled ||
            !other.gameObject.activeInHierarchy ||
            other.isTrigger)
        {
            return true;
        }

        if ((blockingLayers.value & (1 << other.gameObject.layer)) == 0)
            return true;

        if (weaponBody != null && other.attachedRigidbody == weaponBody)
            return true;

        Transform otherTransform = other.transform;
        if (otherTransform == transform || otherTransform.IsChildOf(transform))
            return true;

        if (ignoredPlayerRoot != null &&
            (otherTransform == ignoredPlayerRoot || otherTransform.IsChildOf(ignoredPlayerRoot)))
        {
            return true;
        }

        if (ignoredScabbardRoot != null &&
            (otherTransform == ignoredScabbardRoot || otherTransform.IsChildOf(ignoredScabbardRoot)))
        {
            return true;
        }

        if (ignoredColliders != null)
        {
            for (int i = 0; i < ignoredColliders.Length; i++)
            {
                if (ignoredColliders[i] == other)
                    return true;
            }
        }

        return false;
    }

    private void RecordContact(Collider collider, Vector3 normal, Vector3 fallbackNormal)
    {
        contactCollider = collider;

        if (normal.sqrMagnitude > 0.0001f)
            contactNormal = normal.normalized;
        else if (fallbackNormal.sqrMagnitude > 0.0001f)
            contactNormal = fallbackNormal.normalized;
        else
            contactNormal = Vector3.up;
    }

    private static Vector3 Abs(Vector3 value)
    {
        return new Vector3(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));
    }

    private void OnValidate()
    {
        skinWidth = Mathf.Max(0f, skinWidth);
        contactReleaseMargin = Mathf.Max(skinWidth, contactReleaseMargin);
        maximumSlideIterations = Mathf.Clamp(maximumSlideIterations, 1, 8);
        maximumLinearStep = Mathf.Max(0.001f, maximumLinearStep);
        maximumAngularStep = Mathf.Clamp(maximumAngularStep, 0.5f, 45f);
        maximumSubsteps = Mathf.Clamp(maximumSubsteps, 1, 32);
        maximumRecoveryDistancePerFrame = Mathf.Max(0.001f, maximumRecoveryDistancePerFrame);
        maximumRecoveryIterations = Mathf.Clamp(maximumRecoveryIterations, 1, 8);
        rotationBinarySearchIterations = Mathf.Clamp(rotationBinarySearchIterations, 2, 12);
    }

    private void OnDrawGizmosSelected()
    {
        if (!debugDraw || contactNormal.sqrMagnitude < 0.0001f)
            return;

        Gizmos.color = state == CollisionState.Recovering ? Color.magenta : Color.yellow;
        Vector3 origin = hasResolvedPose ? resolvedPose.position : transform.position;
        Gizmos.DrawLine(origin, origin + contactNormal * 0.25f);
        Gizmos.DrawSphere(origin + contactNormal * 0.25f, 0.015f);
    }
}
