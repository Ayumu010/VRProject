
using UnityEngine;

/// <summary>
/// 刀身先端からローカルZ方向へ伸びる旋空用表示。
/// 攻撃判定は KogetsuBladeDamage が担当し、
/// このコンポーネントのColliderは物理判定に使用しない。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
[RequireComponent(typeof(BoxCollider))]
public sealed class KogetsuBladeExtensionVisual : MonoBehaviour
{
    [Header("Blade Points")]
    [SerializeField]
    private Transform bladeBase;

    [SerializeField]
    private Transform bladeTip;

    [Header("Extension Shape")]
    [SerializeField, Min(0.001f)]
    private float extensionWidth = 0.025f;

    [SerializeField, Min(0.001f)]
    private float extensionThickness = 0.012f;

    [Header("Senku Trail")]
    [SerializeField]
    private TrailRenderer senkuTrail;

    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private BoxCollider physicalCollider;
    private Mesh generatedMesh;

    private float extensionLength;
    private bool warnedAboutMaterial;
    private bool initialized;

    private void Awake()
    {
        Initialize();
    }

    public void Initialize()
    {
        if (initialized)
        {
            DisableExtensionCollider();
            return;
        }

        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();
        physicalCollider = GetComponent<BoxCollider>();

        if (senkuTrail == null)
            senkuTrail = GetComponentInChildren<TrailRenderer>(true);

        DisableExtensionCollider();

        generatedMesh = new Mesh
        {
            name = "Kogetsu_SenkuExtension_Runtime"
        };

        generatedMesh.MarkDynamic();
        meshFilter.sharedMesh = generatedMesh;

        if (senkuTrail != null)
        {
            senkuTrail.emitting = false;
            senkuTrail.Clear();
        }

        initialized = true;
        UpdateVisibility();
    }

    /// <summary>
    /// 旋空の延長距離を変更する。
    /// 0から正の値へ変わるときだけ、前回のTrailを消去する。
    /// </summary>
    public void SetExtensionLength(float length)
    {
        Initialize();

        float newLength = Mathf.Max(0f, length);
        bool wasInactive = extensionLength <= 0f;
        bool willBeActive = newLength > 0f;

        if (wasInactive && willBeActive && senkuTrail != null)
        {
            senkuTrail.emitting = false;
            senkuTrail.Clear();
        }

        if (!willBeActive && senkuTrail != null)
        {
            // 残像は消去せず、Emissionだけを停止する。
            senkuTrail.emitting = false;
        }

        extensionLength = newLength;

        RebuildMesh();
        UpdateVisibility();
    }

    /// <summary>
    /// 剣速に応じてTrailの発生を切り替える。
    /// TrailRenderer.Timeによる残像の消失はUnity側に任せる。
    /// </summary>
    public void SetTrailEmitting(bool emitting)
    {
        Initialize();

        if (senkuTrail == null)
            return;

        senkuTrail.emitting =
            emitting && extensionLength > 0f;
    }

    private void LateUpdate()
    {
        if (bladeBase == null || bladeTip == null)
            return;

        Vector3 direction =
            (bladeTip.position - bladeBase.position).normalized;

        if (direction.sqrMagnitude < 0.0001f)
            return;

        Vector3 up = bladeBase.up;

        if (Mathf.Abs(Vector3.Dot(direction, up)) > 0.98f)
            up = bladeBase.right;

        Quaternion bladeRotation =
            Quaternion.LookRotation(direction, up);

        // 延長メッシュの基準位置は通常の刀身先端。
        transform.SetPositionAndRotation(
            bladeTip.position,
            bladeRotation);

        // Trailは延長後の実効先端に配置する。
        if (senkuTrail != null)
        {
            Vector3 effectiveTip =
                bladeTip.position + direction * extensionLength;

            senkuTrail.transform.SetPositionAndRotation(
                effectiveTip,
                bladeRotation);
        }
    }

    private void RebuildMesh()
    {
        if (generatedMesh == null)
            return;

        generatedMesh.Clear();
        DisableExtensionCollider();

        if (extensionLength <= 0f)
            return;

        float halfWidth = extensionWidth * 0.5f;
        float halfThickness = extensionThickness * 0.5f;

        Vector3[] vertices =
        {
            new Vector3(-halfWidth, -halfThickness, 0f),
            new Vector3( halfWidth, -halfThickness, 0f),
            new Vector3( halfWidth,  halfThickness, 0f),
            new Vector3(-halfWidth,  halfThickness, 0f),

            new Vector3(-halfWidth, -halfThickness, extensionLength),
            new Vector3( halfWidth, -halfThickness, extensionLength),
            new Vector3( halfWidth,  halfThickness, extensionLength),
            new Vector3(-halfWidth,  halfThickness, extensionLength)
        };

        int[] triangles =
        {
            // Near cap
            0, 2, 1, 0, 3, 2,

            // Far cap
            4, 5, 6, 4, 6, 7,

            // Bottom
            0, 1, 5, 0, 5, 4,

            // Top
            3, 7, 6, 3, 6, 2,

            // Left
            0, 4, 7, 0, 7, 3,

            // Right
            1, 2, 6, 1, 6, 5
        };

        generatedMesh.vertices = vertices;
        generatedMesh.triangles = triangles;
        generatedMesh.RecalculateNormals();
        generatedMesh.RecalculateBounds();
    }

    private void UpdateVisibility()
    {
        if (meshRenderer == null)
            return;

        bool hasMaterial =
            meshRenderer.sharedMaterial != null;

        meshRenderer.enabled =
            extensionLength > 0f && hasMaterial;

        DisableExtensionCollider();

        if (extensionLength > 0f &&
            !hasMaterial &&
            !warnedAboutMaterial)
        {
            Debug.LogWarning(
                "KogetsuBladeExtensionVisual: Assign an Extension Material.",
                this);

            warnedAboutMaterial = true;
        }
    }

    private void DisableExtensionCollider()
    {
        if (physicalCollider == null)
            physicalCollider = GetComponent<BoxCollider>();

        if (physicalCollider != null)
        {
            physicalCollider.isTrigger = false;
            physicalCollider.enabled = false;
        }
    }

    private void OnDisable()
    {
        if (senkuTrail != null)
            senkuTrail.emitting = false;
    }

    private void OnDestroy()
    {
        if (generatedMesh != null)
        {
            if (Application.isPlaying)
                Destroy(generatedMesh);
            else
                DestroyImmediate(generatedMesh);
        }
    }

    private void OnValidate()
    {
        extensionWidth = Mathf.Max(0.001f, extensionWidth);
        extensionThickness = Mathf.Max(0.001f, extensionThickness);

        DisableExtensionCollider();
    }
}
