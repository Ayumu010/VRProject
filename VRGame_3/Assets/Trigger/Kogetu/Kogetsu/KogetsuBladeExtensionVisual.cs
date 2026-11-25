using UnityEngine;

/// <summary>
/// 刀身先端からローカルZ方向へ伸びる矩形断面メッシュを生成する旋空用表示。
/// Extension Material は発光/半透明など任意のMaterialを割り当てる。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
[RequireComponent(typeof(BoxCollider))]
public sealed class KogetsuBladeExtensionVisual : MonoBehaviour
{
    [SerializeField]
    private Transform bladeBase;

    [SerializeField]
    private Transform bladeTip;

    [SerializeField, Min(0.001f)]
    private float extensionWidth = 0.025f;

    [SerializeField, Min(0.001f)]
    private float extensionThickness = 0.012f;

    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private BoxCollider physicalCollider;
    private Mesh generatedMesh;
    private float extensionLength;
    private bool warnedAboutMaterial;

    private void Awake()
    {
        Initialize();
    }

    public void Initialize()
    {
        if (meshFilter == null)
            meshFilter = GetComponent<MeshFilter>();

        if (meshRenderer == null)
            meshRenderer = GetComponent<MeshRenderer>();

        if (physicalCollider == null)
            physicalCollider = GetComponent<BoxCollider>();

        if (physicalCollider != null)
            physicalCollider.isTrigger = false;

        if (generatedMesh == null)
        {
            generatedMesh = new Mesh
            {
                name = "Kogetsu_SenkuExtension_Runtime"
            };
            generatedMesh.MarkDynamic();
            meshFilter.sharedMesh = generatedMesh;
        }

        UpdateVisibility();
    }

    public void SetExtensionLength(float length)
    {
        Initialize();
        extensionLength = Mathf.Max(0f, length);
        RebuildMesh();
        UpdateVisibility();
    }

    private void LateUpdate()
    {
        if (bladeBase == null || bladeTip == null)
            return;

        Vector3 direction = (bladeTip.position - bladeBase.position).normalized;
        if (direction.sqrMagnitude < 0.0001f)
            return;

        Vector3 up = bladeBase.up;
        if (Mathf.Abs(Vector3.Dot(direction, up)) > 0.98f)
            up = bladeBase.right;

        transform.SetPositionAndRotation(
            bladeTip.position,
            Quaternion.LookRotation(direction, up));
    }

    private void RebuildMesh()
    {
        if (generatedMesh == null)
            return;

        generatedMesh.Clear();

        if (physicalCollider != null)
        {
            float colliderLength = Mathf.Max(0.001f, extensionLength);
            physicalCollider.center = new Vector3(0f, 0f, extensionLength > 0f ? extensionLength * 0.5f : 0f);
            physicalCollider.size = new Vector3(extensionWidth, extensionThickness, colliderLength);
            physicalCollider.isTrigger = false;
            physicalCollider.enabled = true;
        }

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

        bool hasMaterial = meshRenderer.sharedMaterial != null;
        meshRenderer.enabled = extensionLength > 0f && hasMaterial;

        if (physicalCollider != null)
        {
            physicalCollider.isTrigger = false;
            physicalCollider.enabled = true;
        }

        if (extensionLength > 0f && !hasMaterial && !warnedAboutMaterial)
        {
            Debug.LogWarning("KogetsuBladeExtensionVisual: Assign an Extension Material to display Senku.", this);
            warnedAboutMaterial = true;
        }
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

        if (physicalCollider == null)
            physicalCollider = GetComponent<BoxCollider>();

        if (physicalCollider != null)
        {
            physicalCollider.isTrigger = false;
            physicalCollider.center = new Vector3(0f, 0f, extensionLength > 0f ? extensionLength * 0.5f : 0f);
            physicalCollider.size = new Vector3(extensionWidth, extensionThickness, Mathf.Max(0.001f, extensionLength));
            physicalCollider.enabled = true;
        }
    }
}
