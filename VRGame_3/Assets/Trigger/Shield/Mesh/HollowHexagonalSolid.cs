
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]

public class HollowHexagonalSolid : MonoBehaviour
{
    [Header("外形寸法")]
    [Min(0.01f)]
    public float radius = 1.0f;

    [Min(0.01f)]
    public float prismHeight = 1.0f;

    [Min(0.01f)]
    public float topPyramidHeight = 0.5f;

    [Min(0.01f)]
    public float bottomPyramidHeight = 0.5f;

    [Header("板厚")]
    [Min(0.001f)]
    public float thickness = 0.03f;

    [Header("生成設定")]
    public bool generateCollider = true;

    [Header("生成情報")]
    [SerializeField]
    private Mesh generatedMesh;
    [NonSerialized]
    private bool ownsGeneratedMesh;
    private const float Epsilon = 0.00001f;

    private struct FacePlane
    {
        public Vector3 normal;
        public float distance;
    }

    private readonly List<Vector3> outerVertices = new();
    private readonly List<Vector3> innerVertices = new();

    private readonly List<int[]> faces = new();
    private readonly List<FacePlane> planes = new();

    [ContextMenu("Generate Mesh")]
    public void GenerateMesh()
    {
        TryGenerateMesh();
    }

    /// <summary>
    /// メッシュの生成を試みる。
    /// 成功した場合はtrueを返す。
    /// </summary>
    public bool TryGenerateMesh()
    {
        if (radius <= 0 ||
            prismHeight <= 0 ||
            topPyramidHeight <= 0 ||
            bottomPyramidHeight <= 0 ||
            thickness <= 0)
        {
            Debug.LogError(
                "寸法には正の値を設定してください。",
                this
            );

            return false;
        }

        ClearData();

        try
        {
            CreateVertices();
            CreateFaces();
            CreateFacePlanes();
            CreateInnerVertices();
            BuildMesh();

            MeshFilter filter = GetComponent<MeshFilter>();

            return generatedMesh != null &&
                   filter != null &&
                   filter.sharedMesh == generatedMesh;
        }
        catch (Exception e)
        {
            Debug.LogError(
                "中空六角形メッシュの生成に失敗しました。\n" + e,
                this
            );

            return false;
        }
    }
    private void ClearData()
    {
        outerVertices.Clear();
        innerVertices.Clear();
        faces.Clear();
        planes.Clear();
    }

    // 外側の頂点を作成
    private void CreateVertices()
    {
        float halfHeight = prismHeight * 0.5f;

        for (int i = 0; i < 6; i++)
        {
            float angle = Mathf.Deg2Rad * (i * 60f);

            float x = radius * Mathf.Cos(angle);
            float y = radius * Mathf.Sin(angle);

            outerVertices.Add(
                new Vector3(x, y, -halfHeight)
            );
        }

        for (int i = 0; i < 6; i++)
        {
            float angle = Mathf.Deg2Rad * (i * 60f);

            float x = radius * Mathf.Cos(angle);
            float y = radius * Mathf.Sin(angle);

            outerVertices.Add(
                new Vector3(x, y, halfHeight)
            );
        }

        // 下部六角錐の頂点
        outerVertices.Add(
            new Vector3(
                0,
                0,
                -halfHeight - bottomPyramidHeight
            )
        );

        // 上部六角錐の頂点
        outerVertices.Add(
            new Vector3(
                0,
                0,
                halfHeight + topPyramidHeight
            )
        );
    }

    // 外側の18面を作成
    private void CreateFaces()
    {
        // 六角柱の側面6枚
        for (int i = 0; i < 6; i++)
        {
            int next = (i + 1) % 6;

            faces.Add(new int[]
            {
                i,
                next,
                6 + next,
                6 + i
            });
        }

        // 上部六角錐の側面6枚
        int topApex = 13;

        for (int i = 0; i < 6; i++)
        {
            int next = (i + 1) % 6;

            faces.Add(new int[]
            {
                6 + i,
                6 + next,
                topApex
            });
        }

        // 下部六角錐の側面6枚
        int bottomApex = 12;

        for (int i = 0; i < 6; i++)
        {
            int next = (i + 1) % 6;

            faces.Add(new int[]
            {
                next,
                i,
                bottomApex
            });
        }
    }

    // 各面の外向き法線と平面を計算
    private void CreateFacePlanes()
    {
        foreach (int[] face in faces)
        {
            Vector3 a = outerVertices[face[0]];
            Vector3 b = outerVertices[face[1]];
            Vector3 c = outerVertices[face[2]];

            Vector3 normal = Vector3.Cross(
                b - a,
                c - a
            ).normalized;

            Vector3 center = Vector3.zero;

            foreach (int index in face)
            {
                center += outerVertices[index];
            }

            center /= face.Length;

            // 法線を外向きに統一
            if (Vector3.Dot(normal, center) < 0)
            {
                normal = -normal;
            }

            planes.Add(new FacePlane
            {
                normal = normal,
                distance = Vector3.Dot(normal, a)
            });
        }
    }

    // 内側の頂点を作成
    // 各面を厚み分だけ内側にオフセットした
    // 半空間の交差領域へ頂点を移動する
    private void CreateInnerVertices()
    {
        for (int i = 0; i < outerVertices.Count; i++)
        {
            Vector3 point = outerVertices[i];

            // 射影反復法
            // すべての面の内側制約を満たすまで調整
            for (int iteration = 0; iteration < 256; iteration++)
            {
                float maxViolation = 0f;

                foreach (FacePlane plane in planes)
                {
                    float limit = plane.distance - thickness;

                    float current =
                        Vector3.Dot(plane.normal, point);

                    float violation = current - limit;

                    if (violation > 0f)
                    {
                        point -= plane.normal * violation;

                        maxViolation = Mathf.Max(
                            maxViolation,
                            violation
                        );
                    }
                }

                if (maxViolation < 0.000001f)
                    break;
            }

            // すべての面の制約を確認
            foreach (FacePlane plane in planes)
            {
                float violation =
                    Vector3.Dot(plane.normal, point)
                    - (plane.distance - thickness);

                if (violation > 0.001f)
                {
                    throw new Exception(
                        "板厚が大きすぎるか、内側形状を生成できません。"
                    );
                }
            }

            if (Vector3.Distance(point, outerVertices[i]) < Epsilon)
            {
                throw new Exception(
                    "内側頂点が生成できません。板厚を確認してください。"
                );
            }

            innerVertices.Add(point);
        }

        // 内側面の退化を確認
        foreach (int[] face in faces)
        {
            Vector3 a = innerVertices[face[0]];
            Vector3 b = innerVertices[face[1]];
            Vector3 c = innerVertices[face[2]];

            float area = Vector3.Cross(
                b - a,
                c - a
            ).magnitude * 0.5f;

            if (area < Epsilon)
            {
                throw new Exception(
                    "内側面が潰れています。板厚を小さくしてください。"
                );
            }
        }
    }

    // Meshを組み立てる
    private void BuildMesh()
    {
        List<Vector3> vertices = new();
        List<int> triangles = new();

        // 外側の頂点
        vertices.AddRange(outerVertices);

        // 内側の頂点
        int innerOffset = outerVertices.Count;

        vertices.AddRange(innerVertices);

        // 外側18面
        foreach (int[] face in faces)
        {
            AddFaceTriangles(
                triangles,
                face,
                false,
                innerOffset
            );
        }

        // 内側18面
        foreach (int[] face in faces)
        {
            AddFaceTriangles(
                triangles,
                face,
                true,
                innerOffset
            );
        }

        // 各辺の端面を1回ずつ生成
        HashSet<long> usedEdges = new();

        foreach (int[] face in faces)
        {
            for (int i = 0; i < face.Length; i++)
            {
                int a = face[i];
                int b = face[(i + 1) % face.Length];

                int min = Mathf.Min(a, b);
                int max = Mathf.Max(a, b);

                long key = ((long)min << 32) | (uint)max;

                if (!usedEdges.Add(key))
                    continue;

                int ia = a + innerOffset;
                int ib = b + innerOffset;

                // 端面の四角形を2三角形に分割
                triangles.Add(a);
                triangles.Add(b);
                triangles.Add(ib);

                triangles.Add(a);
                triangles.Add(ib);
                triangles.Add(ia);
            }
        }

        Mesh mesh = new Mesh();

        mesh.name = "HollowHexagonalSolid";

        if (vertices.Count > 65535)
        {
            mesh.indexFormat = IndexFormat.UInt32;
        }

        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);

        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        // ----------------------------------------------
        // Mesh References
        // ----------------------------------------------

        MeshFilter meshFilter =
            GetComponent<MeshFilter>();

        if (meshFilter == null)
        {
            DestroyGeneratedMesh(mesh);

            throw new InvalidOperationException(
                "MeshFilterが見つかりません。"
            );
        }

        MeshCollider meshCollider =
            GetComponent<MeshCollider>();

        if (generateCollider && meshCollider == null)
        {
            meshCollider =
                gameObject.AddComponent<MeshCollider>();
        }

        // ----------------------------------------------
        // Previous Mesh
        // ----------------------------------------------

        Mesh previousMesh = generatedMesh;

        bool shouldDestroyPreviousMesh =
            ownsGeneratedMesh;

        // 自分が生成した古いMeshだけ参照を解除する
        if (shouldDestroyPreviousMesh &&
            previousMesh != null)
        {
            if (meshFilter.sharedMesh == previousMesh)
            {
                meshFilter.sharedMesh = null;
            }

            if (meshCollider != null &&
                meshCollider.sharedMesh == previousMesh)
            {
                meshCollider.sharedMesh = null;
            }
        }

        // ----------------------------------------------
        // Assign New Mesh
        // ----------------------------------------------

        generatedMesh = mesh;

        ownsGeneratedMesh = true;

        meshFilter.sharedMesh = generatedMesh;

        if (generateCollider &&
            meshCollider != null)
        {
            meshCollider.sharedMesh = null;
            meshCollider.sharedMesh = generatedMesh;
        }

        // ----------------------------------------------
        // Release Previous Mesh
        // ----------------------------------------------

        if (shouldDestroyPreviousMesh &&
            previousMesh != null)
        {
            DestroyGeneratedMesh(previousMesh);
        }

        Debug.Log(
            $"中空六角形を生成しました。頂点数: {vertices.Count}, " +
            $"三角形数: {triangles.Count / 3}",
            this
        );
    }

    // 三角形を追加
    private void AddFaceTriangles(
        List<int> triangles,
        int[] face,
        bool reverse,
        int innerOffset)
    {
        for (int i = 1; i < face.Length - 1; i++)
        {
            int a = face[0];
            int b = face[i];
            int c = face[i + 1];

            if (reverse)
            {
                triangles.Add(a + innerOffset);
                triangles.Add(c + innerOffset);
                triangles.Add(b + innerOffset);
            }
            else
            {
                triangles.Add(a);
                triangles.Add(b);
                triangles.Add(c);
            }
        }
    }

    private void OnDestroy()
    {
        if (!ownsGeneratedMesh ||
            generatedMesh == null)
        {
            return;
        }

        Mesh mesh = generatedMesh;

        MeshFilter meshFilter =
            GetComponent<MeshFilter>();

        MeshCollider meshCollider =
            GetComponent<MeshCollider>();

        // Meshへの参照を先に解除
        if (meshFilter != null &&
            meshFilter.sharedMesh == mesh)
        {
            meshFilter.sharedMesh = null;
        }

        if (meshCollider != null &&
            meshCollider.sharedMesh == mesh)
        {
            meshCollider.sharedMesh = null;
        }

        DestroyGeneratedMesh(mesh);

        generatedMesh = null;

        ownsGeneratedMesh = false;
    }

    private static void DestroyGeneratedMesh(Mesh mesh)
    {
        if (mesh == null)
            return;

        if (Application.isPlaying)
        {
            UnityEngine.Object.Destroy(mesh);
            return;
        }

#if UNITY_EDITOR
        // Assetとして保存されているMeshは破棄しない
        if (UnityEditor.EditorUtility.IsPersistent(mesh))
            return;
#endif

        UnityEngine.Object.DestroyImmediate(mesh);
    }
}
