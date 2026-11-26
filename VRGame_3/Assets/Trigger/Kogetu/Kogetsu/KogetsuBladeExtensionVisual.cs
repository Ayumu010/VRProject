using UnityEngine;

/// <summary>
/// 刀身先端からローカルZ方向へ伸びる旋空用表示と物理Colliderを管理する。
/// 攻撃判定は KogetsuBladeDamage が担当し、このColliderは環境との衝突制限専用。
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
            UpdateExtensionCollider();
            return;
        }

        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();
        physicalCollider = GetComponent<BoxCollider>();

        // TrailAnchorはSenkuExtensionVisualの兄弟なので、刀全体のRigidbodyルートから探す。
        if (senkuTrail == null)
        {
            Rigidbody weaponBody = GetComponentInParent<Rigidbody>();
            Transform searchRoot = weaponBody != null ? weaponBody.transform : transform.root;
            senkuTrail = searchRoot.GetComponentInChildren<TrailRenderer>(true);
        }

        if (physicalCollider != null)
        {
            physicalCollider.isTrigger = false;
            physicalCollider.enabled = false;
        }

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
        RefreshBladePose();
        UpdateVisibility();
    }

    /// <summary>
    /// 旋空の延長距離を変更する。
    /// 0から正の値へ変わるときだけ前回のTrailを消去する。
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
            // 既に発生した残像は消去せず、Emissionだけを止めてTime経過で消す。
            senkuTrail.emitting = false;
        }

        extensionLength = newLength;

        // Colliderを有効化する前に、刀身方向に沿ったTransformと形状を確定する。
        RefreshBladePose();
        RebuildMesh();
        UpdateVisibility();
    }

    /// <summary>
    /// 剣速に応じてTrailの発生を切り替える。
    /// </summary>
    public void SetTrailEmitting(bool emitting)
    {
        Initialize();

        if (senkuTrail == null)
            return;

        senkuTrail.emitting = emitting && extensionLength > 0f;
    }

    private void LateUpdate()
    {
        RefreshBladePose();
    }

    private void RefreshBladePose()
    {
        if (bladeBase == null || bladeTip == null)
            return;

        Vector3 bladeVector = bladeTip.position - bladeBase.position;
        if (bladeVector.sqrMagnitude < 0.0001f)
            return;

        Vector3 direction = bladeVector.normalized;
        Vector3 up = bladeBase.up;

        if (Mathf.Abs(Vector3.Dot(direction, up)) > 0.98f)
            up = bladeBase.right;

        Quaternion bladeRotation = Quaternion.LookRotation(direction, up);

        // 延長メッシュとBoxColliderの基準位置は通常刀身の先端。
        transform.SetPositionAndRotation(bladeTip.position, bladeRotation);

        // Trailは延長後の実効先端に配置する。
        if (senkuTrail != null)
        {
            Vector3 effectiveTip = bladeTip.position + direction * extensionLength;
            senkuTrail.transform.SetPositionAndRotation(effectiveTip, bladeRotation);
        }
    }

    private void RebuildMesh()
    {
        if (generatedMesh == null)
            return;

        generatedMesh.Clear();
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
        UpdateExtensionCollider();

        if (meshRenderer == null)
            return;

        bool hasMaterial = meshRenderer.sharedMaterial != null;
        meshRenderer.enabled = extensionLength > 0f && hasMaterial;

        if (extensionLength > 0f && !hasMaterial && !warnedAboutMaterial)
        {
            Debug.LogWarning(
                "KogetsuBladeExtensionVisual: Assign an Extension Material.",
                this);
            warnedAboutMaterial = true;
        }
    }

    /// <summary>
    /// 表示Meshと同じ長さ・幅のBoxColliderを生成する。
    /// 通常時は無効、旋空中のみ有効にする。
    /// </summary>
    private void UpdateExtensionCollider()
    {
        if (physicalCollider == null)
            physicalCollider = GetComponent<BoxCollider>();

        if (physicalCollider == null)
            return;

        physicalCollider.isTrigger = false;
        physicalCollider.center = new Vector3(0f, 0f, extensionLength * 0.5f);
        physicalCollider.size = new Vector3(
            extensionWidth,
            extensionThickness,
            Mathf.Max(extensionLength, 0.001f));
        physicalCollider.enabled = extensionLength > 0.0001f;
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

        if (!Application.isPlaying)
        {
            if (physicalCollider == null)
                physicalCollider = GetComponent<BoxCollider>();

            if (physicalCollider != null)
            {
                physicalCollider.isTrigger = false;
                physicalCollider.enabled = false;
            }
        }
    }
}
