
using UnityEngine;
using UnityEngine.ProBuilder;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
public sealed class ShieldController : MonoBehaviour
{

    [Header("Shield Mesh")]
    [SerializeField]
    private Transform shieldVisual;

    [Header("Concentration")]
    [SerializeField, Range(0.05f, 1f)]
    private float concentratedAreaRatio = 0.25f;

    [SerializeField, Min(1f)]
    private float concentratedThicknessMultiplier = 2f;

    private Rigidbody body;

    private MeshFilter meshFilter;

    private MeshCollider shieldCollider;

    private ShieldSurface surface;

    private Vector3 normalVisualScale;

    private Transform followTarget;

    private Vector3 followPositionOffset;

    private Quaternion followRotationOffset;

    private Vector3 followVelocity;

    private float followSmoothTime;

    private float followRotationSpeed;

    private Vector3 fixedWorldPosition;

    private Quaternion fixedWorldRotation;

    private bool isInitialized;

    public ShieldMovementMode MovementMode { get; private set; }

    public bool IsConcentrated { get; private set; }

    public bool IsInitialized => isInitialized;

    public ShieldSurface Surface => surface;

    // ==================================================
    // Awake
    // ==================================================

    private void Awake()
    {
        body = GetComponent<Rigidbody>();

        InitializeMeshReferences();

        if (!enabled)
            return;

        body.isKinematic = true;
        body.useGravity = false;

        body.interpolation =
            RigidbodyInterpolation.Interpolate;

        body.collisionDetectionMode =
            CollisionDetectionMode.ContinuousSpeculative;

        surface.Broken += HandleSurfaceBroken;
    }

    // ==================================================
    // Mesh References
    // ==================================================

    private void InitializeMeshReferences()
    {
        if (shieldVisual == null)
        {
            ShieldSurface foundSurface =
                GetComponentInChildren<ShieldSurface>(true);

            if (foundSurface != null)
            {
                shieldVisual = foundSurface.transform;
            }
        }

        if (shieldVisual == null)
        {
            ReportConfigurationError(
                "ShieldVisual is missing."
            );

            return;
        }

        meshFilter =
            shieldVisual.GetComponent<MeshFilter>();

        shieldCollider =
            shieldVisual.GetComponent<MeshCollider>();

        surface =
            shieldVisual.GetComponent<ShieldSurface>();

        if (meshFilter == null ||
            shieldCollider == null ||
            surface == null)
        {
            ReportConfigurationError(
                "ShieldMesh requires MeshFilter, MeshCollider and ShieldSurface on the same GameObject."
            );

            return;
        }

        // ----------------------------------------------
        // Generate Unity Mesh From ProBuilder Data
        // ----------------------------------------------

        if (meshFilter.sharedMesh == null)
        {
            ProBuilderMesh proBuilderMesh =
                shieldVisual.GetComponent<ProBuilderMesh>();

            if (proBuilderMesh == null)
            {
                ReportConfigurationError(
                    "ProBuilderMesh component is missing."
                );

                return;
            }

            try
            {
                // ProBuilderの形状データからUnity Meshを構築
                proBuilderMesh.ToMesh();

                // 頂点属性・法線などを更新
                proBuilderMesh.Refresh();
            }
            catch (System.Exception e)
            {
                ReportConfigurationError(
                    "ProBuilder Mesh Generation Failed: " + e.Message
                );

                return;
            }
        }

        // ----------------------------------------------
        // Validate Generated Mesh
        // ----------------------------------------------

        if (meshFilter.sharedMesh == null)
        {
            ReportConfigurationError(
                "ShieldVisual MeshFilter SharedMesh is still missing after ProBuilder generation."
            );

            return;
        }

        Debug.Log(
            $"ShieldController : ProBuilder Mesh Generated - {meshFilter.sharedMesh.name}",
            shieldVisual
        );
        
        // 描画と衝突判定に同じMeshを使用
        shieldCollider.sharedMesh =
            meshFilter.sharedMesh;

        shieldCollider.convex = true;

        shieldCollider.isTrigger = false;

        normalVisualScale =
            shieldVisual.localScale;
    }

    private void ReportConfigurationError(string message)
    {
        Debug.LogError(
            $"ShieldController : {message}",
            this
        );

        enabled = false;
    }

    // ==================================================
    // Initialize
    // ==================================================

    public bool Initialize(
        Transform target,
        Transform ownerRoot,
        Vector3 positionOffset,
        Vector3 rotationOffset,
        float smoothTime,
        float rotationSpeed)
    {
        if (!enabled ||
            target == null ||
            ownerRoot == null ||
            body == null ||
            surface == null)
        {
            return false;
        }

        transform.SetParent(
            ownerRoot,
            true
        );

        followTarget = target;

        followPositionOffset = positionOffset;

        followRotationOffset =
            Quaternion.Euler(rotationOffset);

        followSmoothTime = Mathf.Max(
            0f,
            smoothTime
        );

        followRotationSpeed = Mathf.Max(
            0f,
            rotationSpeed
        );

        followVelocity = Vector3.zero;

        surface.ResetSurface();

        IsConcentrated = false;

        ApplyConcentrationScale(false);

        MovementMode = ShieldMovementMode.Follow;

        isInitialized = true;

        ApplyFollowPoseImmediately();

        Physics.SyncTransforms();

        return true;
    }

    // ==================================================
    // Follow
    // ==================================================

    private void ApplyFollowPoseImmediately()
    {
        if (followTarget == null)
            return;

        Vector3 position =
            followTarget.TransformPoint(
                followPositionOffset
            );

        Quaternion rotation =
            followTarget.rotation *
            followRotationOffset;

        transform.SetPositionAndRotation(
            position,
            rotation
        );

        body.position = position;
        body.rotation = rotation;

        fixedWorldPosition = position;
        fixedWorldRotation = rotation;
    }

    // ==================================================
    // Movement Mode
    // ==================================================

    public bool ToggleMovementMode()
    {
        ShieldMovementMode nextMode =
            MovementMode == ShieldMovementMode.Follow
                ? ShieldMovementMode.Fixed
                : ShieldMovementMode.Follow;

        return SetMovementMode(nextMode);
    }

    public bool SetMovementMode(ShieldMovementMode mode)
    {
        if (!isInitialized)
            return false;

        if (mode == ShieldMovementMode.Follow &&
            followTarget == null)
        {
            return false;
        }

        if (MovementMode == mode)
            return true;

        if (mode == ShieldMovementMode.Fixed)
        {
            fixedWorldPosition = transform.position;

            fixedWorldRotation = transform.rotation;
        }
        else
        {
            followVelocity = Vector3.zero;
        }

        MovementMode = mode;

        return true;
    }

    // ==================================================
    // Concentration
    // ==================================================

    public bool ToggleConcentration()
    {
        return SetConcentrated(!IsConcentrated);
    }

    public bool SetConcentrated(bool concentrated)
    {
        if (!isInitialized ||
            surface == null ||
            surface.IsBroken)
        {
            return false;
        }

        if (IsConcentrated == concentrated)
            return true;

        float multiplier = concentrated
            ? 1f / concentratedAreaRatio
            : 1f;

        if (!surface.SetDurabilityMultiplier(multiplier))
            return false;

        IsConcentrated = concentrated;

        ApplyConcentrationScale(concentrated);

        Physics.SyncTransforms();

        return true;
    }

    private void ApplyConcentrationScale(bool concentrated)
    {
        // シールド面の縦横の縮小率
        float planeScale = concentrated
            ? Mathf.Sqrt(concentratedAreaRatio)
            : 1f;

        // シールドの厚さの倍率
        float thicknessScale = concentrated
            ? concentratedThicknessMultiplier
            : 1f;

        // X・Z方向を縮小し、Y方向の厚さを増やす
        shieldVisual.localScale = new Vector3(
            normalVisualScale.x * planeScale,
            normalVisualScale.y * thicknessScale,
            normalVisualScale.z * planeScale
        );
    }
    
    // ==================================================
    // Fixed Update
    // ==================================================

    private void FixedUpdate()
    {
        if (!isInitialized ||
            body == null)
        {
            return;
        }

        switch (MovementMode)
        {
            case ShieldMovementMode.Follow:
                UpdateFollowing();
                break;

            case ShieldMovementMode.Fixed:
                UpdateFixedPosition();
                break;
        }
    }

    // ==================================================
    // Follow Update
    // ==================================================

    private void UpdateFollowing()
    {
        if (followTarget == null)
        {
            SetMovementMode(
                ShieldMovementMode.Fixed
            );

            return;
        }

        Vector3 targetPosition =
            followTarget.TransformPoint(
                followPositionOffset
            );

        Quaternion targetRotation =
            followTarget.rotation *
            followRotationOffset;

        Vector3 nextPosition;

        if (followSmoothTime <= 0f)
        {
            nextPosition = targetPosition;

            followVelocity = Vector3.zero;
        }
        else
        {
            nextPosition = Vector3.SmoothDamp(
                body.position,
                targetPosition,
                ref followVelocity,
                followSmoothTime,
                Mathf.Infinity,
                Time.fixedDeltaTime
            );
        }

        float rotationT =
            followRotationSpeed <= 0f
                ? 1f
                : 1f - Mathf.Exp(
                    -followRotationSpeed *
                    Time.fixedDeltaTime
                );

        Quaternion nextRotation =
            Quaternion.Slerp(
                body.rotation,
                targetRotation,
                rotationT
            );

        body.MovePosition(nextPosition);

        body.MoveRotation(nextRotation);
    }

    // ==================================================
    // Fixed Position
    // ==================================================

    private void UpdateFixedPosition()
    {
        body.MovePosition(
            fixedWorldPosition
        );

        body.MoveRotation(
            fixedWorldRotation
        );
    }

    // ==================================================
    // Broken
    // ==================================================

    private void HandleSurfaceBroken(ShieldSurface brokenSurface)
    {
        Debug.Log(
            "ShieldController : Broken",
            this
        );

        Destroy(gameObject);
    }

    // ==================================================
    // Cleanup
    // ==================================================

    private void OnDestroy()
    {
        if (surface != null)
        {
            surface.Broken -= HandleSurfaceBroken;
        }
    }
}
