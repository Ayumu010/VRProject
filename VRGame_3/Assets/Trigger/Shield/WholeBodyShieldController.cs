
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
public sealed class WholeBodyShieldController : MonoBehaviour
{
    [Header("Existing Shield Model")]
    [SerializeField]
    private Transform shieldMesh;

    [Header("Player Position")]
    [SerializeField]
    private Vector3 centerLocalOffset =
        new Vector3(0f, 1f, 0f);

    [Header("Collision")]
    [SerializeField, Min(0.0001f)]
    private float contactOffset = 0.002f;

    private Rigidbody body;

    private MeshFilter meshFilter;

    private MeshRenderer meshRenderer;

    private MeshCollider meshCollider;

    private ShieldSurface surface;

    private Transform playerRoot;

    private ShieldSystemCoordinator coordinator;

    private bool isInitialized;

    public ShieldSurface Surface => surface;

    public bool IsInitialized => isInitialized;

    // ==================================================
    // Awake
    // ==================================================

    private void Awake()
    {
        body = GetComponent<Rigidbody>();

        if (!InitializeMeshReferences())
        {
            enabled = false;
            return;
        }

        body.isKinematic = true;

        body.useGravity = false;

        body.interpolation =
            RigidbodyInterpolation.Interpolate;

        body.collisionDetectionMode =
            CollisionDetectionMode.ContinuousSpeculative;

        surface.Broken += HandleBroken;
    }

    // ==================================================
    // Existing Model References
    // ==================================================

    private bool InitializeMeshReferences()
    {
        // Inspector未設定時はShieldSurfaceから検索
        if (shieldMesh == null)
        {
            ShieldSurface foundSurface =
                GetComponentInChildren<ShieldSurface>(true);

            if (foundSurface != null)
            {
                shieldMesh = foundSurface.transform;
            }
        }

        if (shieldMesh == null)
        {
            Debug.LogError(
                "WholeBodyShieldController: Existing Shield Model is missing.",
                this
            );

            return false;
        }

        // 既存モデルのコンポーネント取得
        meshFilter =
            shieldMesh.GetComponent<MeshFilter>();

        meshRenderer =
            shieldMesh.GetComponent<MeshRenderer>();

        meshCollider =
            shieldMesh.GetComponent<MeshCollider>();

        surface =
            shieldMesh.GetComponent<ShieldSurface>();

        // ----------------------------------------------
        // Validate
        // ----------------------------------------------

        if (meshFilter == null)
        {
            Debug.LogError(
                "WholeBodyShieldController: MeshFilter is missing.",
                shieldMesh
            );

            return false;
        }

        if (meshRenderer == null)
        {
            Debug.LogError(
                "WholeBodyShieldController: MeshRenderer is missing.",
                shieldMesh
            );

            return false;
        }

        if (meshCollider == null)
        {
            Debug.LogError(
                "WholeBodyShieldController: MeshCollider is missing.",
                shieldMesh
            );

            return false;
        }

        if (surface == null)
        {
            Debug.LogError(
                "WholeBodyShieldController: ShieldSurface is missing.",
                shieldMesh
            );

            return false;
        }

        // ----------------------------------------------
        // Generate Mesh If Missing
        // ----------------------------------------------

        if (meshFilter.sharedMesh == null)
        {
            HollowHexagonalSolid generator =
                shieldMesh.GetComponent<HollowHexagonalSolid>();

            if (generator == null)
            {
                Debug.LogError(
                    "WholeBodyShieldController: HollowHexagonalSolid component is missing.",
                    shieldMesh
                );

                return false;
            }

            if (!generator.TryGenerateMesh())
            {
                Debug.LogError(
                    "WholeBodyShieldController: Mesh generation failed.",
                    shieldMesh
                );

                return false;
            }
        }

        // ----------------------------------------------
        // Validate Generated Mesh
        // ----------------------------------------------

        // ----------------------------------------------
        // Generate Mesh If Missing
        // ----------------------------------------------

        if (meshFilter.sharedMesh == null)
        {
            HollowHexagonalSolid generator =
                shieldMesh.GetComponent<HollowHexagonalSolid>();

            if (generator == null)
            {
                Debug.LogError(
                    "WholeBodyShieldController: HollowHexagonalSolid component is missing.",
                    shieldMesh
                );

                return false;
            }

            Debug.Log(
                "WholeBodyShieldController: Generating Mesh...",
                shieldMesh
            );

            // 既存のメッシュ生成メソッドを呼び出す
            generator.GenerateMesh();
        }

        // ----------------------------------------------
        // Validate Mesh
        // ----------------------------------------------

        Mesh existingMesh =
            meshFilter.sharedMesh;

        if (existingMesh == null)
        {
            Debug.LogError(
                "WholeBodyShieldController: Mesh generation failed.",
                shieldMesh
            );

            return false;
        }
        
        // ----------------------------------------------
        // Use Existing Mesh
        // ----------------------------------------------

        // 古いCollider Meshを解除
        meshCollider.sharedMesh = null;

        // 描画モデルと同じMeshを使用
        meshCollider.sharedMesh = existingMesh;

        // 全身シールドは凸形状
        meshCollider.convex = true;

        meshCollider.isTrigger = false;

        meshCollider.contactOffset =
            contactOffset;

        Debug.Log(
            $"WholeBodyShieldController: Existing Mesh Assigned - {existingMesh.name}",
            shieldMesh
        );

        return true;
    }

    // ==================================================
    // Initialize
    // ==================================================

    public bool Initialize(
        Transform ownerRoot,
        ShieldSystemCoordinator systemCoordinator)
    {
        if (!enabled ||
            ownerRoot == null ||
            systemCoordinator == null ||
            body == null ||
            surface == null ||
            meshCollider == null)
        {
            return false;
        }

        playerRoot = ownerRoot;

        coordinator = systemCoordinator;

        transform.SetParent(
            playerRoot,
            true
        );

        surface.ResetSurface();

        meshCollider.enabled = true;

        isInitialized = true;

        ApplyPlayerPoseImmediately();

        Physics.SyncTransforms();

        return true;
    }

    // ==================================================
    // Player Pose
    // ==================================================

    private void ApplyPlayerPoseImmediately()
    {
        if (playerRoot == null)
            return;

        Vector3 position =
            playerRoot.TransformPoint(
                centerLocalOffset
            );

        Quaternion rotation =
            GetPlayerHorizontalRotation();

        transform.SetPositionAndRotation(
            position,
            rotation
        );

        body.position = position;

        body.rotation = rotation;
    }

    private Quaternion GetPlayerHorizontalRotation()
    {
        Vector3 forward =
            Vector3.ProjectOnPlane(
                playerRoot.forward,
                Vector3.up
            );

        if (forward.sqrMagnitude < 0.0001f)
        {
            forward = Vector3.forward;
        }

        return Quaternion.LookRotation(
            forward.normalized,
            Vector3.up
        );
    }

    // ==================================================
    // Physics Update
    // ==================================================

    private void FixedUpdate()
    {
        if (!isInitialized ||
            playerRoot == null)
        {
            return;
        }

        Vector3 targetPosition =
            playerRoot.TransformPoint(
                centerLocalOffset
            );

        Quaternion targetRotation =
            GetPlayerHorizontalRotation();

        body.MovePosition(targetPosition);

        body.MoveRotation(targetRotation);
    }

    // ==================================================
    // Broken
    // ==================================================

    private void HandleBroken(
        ShieldSurface brokenSurface)
    {
        Debug.Log(
            "WholeBodyShield : Broken",
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
            surface.Broken -= HandleBroken;
        }

        if (coordinator != null)
        {
            coordinator.NotifyWholeBodyShieldDestroyed(
                this
            );
        }

        // 既存のMesh AssetはDestroyしない。
        // Prefabから参照している共有アセットのため。
    }

    // ==================================================
    // Validation
    // ==================================================

    private void OnValidate()
    {
        contactOffset = Mathf.Max(
            0.0001f,
            contactOffset
        );
    }
}
