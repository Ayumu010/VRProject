using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;


[CreateAssetMenu(
    fileName = "Asteroid",
    menuName = "Game/Triggers/Behaviours/Asteroid"
)]
public class Asteroid : TriggerBehaviour
{
    // ==================================================
    // Inspector Settings
    // ==================================================

    [Header("Cube")]
    [SerializeField]
    private GameObject asteroidCubePrefab;

    [Header("Cube Spawn Position")]
    [SerializeField, Min(0f)]
    private float spawnClearance = 0.05f;

    [SerializeField]
    private float spawnForwardOffset = 0f;

    [Header("Follow")]
    [SerializeField, Min(0f)]
    private float followSmoothTime = 0.12f;

    [SerializeField, Min(0f)]
    private float followRotationSpeed = 12f;

    [Header("Split Menu")]
    [SerializeField]
    private AsteroidSplitMenu splitMenuPrefab;

    [SerializeField, Min(0f)]
    private float menuHoldSeconds = 0.35f;

    [Header("Menu Position")]
    [SerializeField]
    private Vector3 menuLocalPosition =
        new Vector3(0f, 0.05f, 0.15f);

    [SerializeField]
    private Vector3 menuLocalEulerAngles =
        Vector3.zero;

    [Header("Split Animation")]
    [SerializeField, Min(0f)]
    private float splitSpreadDuration = 0.3f;

    [SerializeField, Min(0f)]
    private float splitStaggerMax = 0.05f;

    [SerializeField, Range(0f, 0.25f)]
    private float splitGapRatio = 0.06f;

    [SerializeField, Range(0f, 0.5f)]
    private float splitRandomOffsetRatio = 0.025f;

    [Header("Grip")]
    [SerializeField, Min(0f)]
    private float scatterHoldSeconds = 0.45f;

    [SerializeField, Min(0f)]
    private float scatterRadius = 0.5f;

    [SerializeField, Min(0f)]
    private float scatterDuration = 0.3f;

    // ==================================================
    // Runtime State
    // ==================================================

    [NonSerialized]
    private List<AsteroidCubePiece> activePieces;

    [NonSerialized]
    private AsteroidCubePiece previewPiece;

    [NonSerialized]
    private AsteroidSplitMenu activeMenu;

    [NonSerialized]
    private bool isSplit;

    // Owner
    [NonSerialized]
    private ulong runtimeOwnerId;

    [NonSerialized]
    private Transform runtimeOwnerRoot;

    [NonSerialized]
    private bool runtimeOwnerInitialized;

    // Trigger
    [NonSerialized]
    private bool triggerHeld;

    [NonSerialized]
    private bool menuOpenAttempted;

    [NonSerialized]
    private float triggerHoldTime;

    // Grip
    [NonSerialized]
    private bool gripPressing;

    [NonSerialized]
    private float gripHoldTime;

    private List<AsteroidCubePiece> Pieces
    {
        get
        {
            if (activePieces == null)
            {
                activePieces =
                    new List<AsteroidCubePiece>();
            }

            return activePieces;
        }
    }

    // ==================================================
    // Registration
    // ==================================================

    protected override void OnRegistered()
    {
        ResetInputState();
    }

    // ==================================================
    // Selected
    // ==================================================

    protected override void OnSelected()
    {
        Debug.Log($"Asteroid Selected : {Side}");
    }

    // ==================================================
    // Deselected
    // ==================================================

    protected override void OnDeselected()
    {
        CloseMenu();

        ResetInputState();

        CleanupRuntimeObjects();

        Debug.Log($"Asteroid Deselected : {Side}");
    }

    // ==================================================
    // Input
    // ==================================================

    protected override void OnInput(
        TriggerControl control,
        bool pressed)
    {
        switch (control)
        {
            case TriggerControl.Trigger:

                if (pressed)
                    HandleTriggerPressed();
                else
                    HandleTriggerReleased();

                break;

            case TriggerControl.Grip:

                if (pressed)
                    HandleGripPressed();
                else
                    HandleGripReleased();

                break;

            case TriggerControl.Primary:

                if (pressed)
                    Debug.Log($"Asteroid Primary : {Side}");

                break;

            case TriggerControl.Secondary:

                if (pressed)
                    Debug.Log($"Asteroid Secondary : {Side}");

                break;
        }
    }

    // ==================================================
    // Owner Information
    // ==================================================

    private bool ResolveOwnerInformation()
    {
        if (PlayerRoot == null)
        {
            Debug.LogError(
                $"Asteroid [{Side}] : PlayerRoot is missing."
            );

            return false;
        }

        runtimeOwnerRoot = PlayerRoot;

        // 現在のUnity実行中におけるローカルOwner ID。
        // 同じPlayerRootから生成したキューブには同じIDを渡す。
        runtimeOwnerId = unchecked(
            (ulong)(uint)runtimeOwnerRoot.GetInstanceID()
        );

        runtimeOwnerInitialized = true;

        return true;
    }

    // ==================================================
    // Create Cube Object
    // ==================================================

    private AsteroidCubePiece CreateCubePieceObject(
        bool attachUsingHandSpawner,
        Vector3? initialWorldPosition = null,
        Quaternion? initialWorldRotation = null)
    {
        if (asteroidCubePrefab == null)
        {
            Debug.LogError(
                "Asteroid : Cube Prefab is missing."
            );

            return null;
        }

        if (HandAttachPoint == null)
        {
            Debug.LogError(
                $"Asteroid [{Side}] : HandAttachPoint is missing."
            );

            return null;
        }

        if (!runtimeOwnerInitialized &&
            !ResolveOwnerInformation())
        {
            return null;
        }

        GameObject instance;

        if (attachUsingHandSpawner)
        {
            // 既存のSpawn管理を利用
            instance = SpawnOnHand(asteroidCubePrefab);
        }
        else
        {
            // 分割用の追加キューブ
            instance = Instantiate(asteroidCubePrefab);
        }

        if (instance == null)
            return null;

        Transform instanceTransform =
            instance.transform;

        // 以降は親子関係を使わずワールド空間で管理
        instanceTransform.SetParent(null, true);

        instanceTransform.rotation =
            initialWorldRotation ?? HandAttachPoint.rotation;

        if (initialWorldPosition.HasValue)
        {
            instanceTransform.position =
                initialWorldPosition.Value;
        }
        else
        {
            PositionCubeBelowHand(instanceTransform);
        }

        AsteroidCubePiece piece =
            instance.GetComponent<AsteroidCubePiece>();

        if (piece == null)
        {
            Debug.LogError(
                "Asteroid : AsteroidCubePiece is missing.",
                instance
            );

            DestroySpawned(instance);

            return null;
        }

        // 発射者情報をBulletControllerへ渡す
        if (!piece.InitializeOwner(
                runtimeOwnerId,
                runtimeOwnerRoot))
        {
            DestroySpawned(instance);

            return null;
        }

        return piece;
    }

    // ==================================================
    // Spawn Position
    // ==================================================

    /// <summary>
    /// キューブのBoundsを使い、手の下に生成する。
    /// </summary>
    private void PositionCubeBelowHand(Transform cube)
    {
        if (cube == null || HandAttachPoint == null)
            return;

        if (!TryCalculateRootLocalBounds(
                cube,
                out Bounds localBounds))
        {
            // Boundsを取得できない場合のFallback
            cube.position =
                HandAttachPoint.position +
                Vector3.down * 0.15f +
                HandAttachPoint.forward * spawnForwardOffset;

            return;
        }

        // キューブのローカルBoundsをワールド空間へ変換
        Vector3 axisX =
            cube.TransformVector(
                Vector3.right * localBounds.extents.x
            );

        Vector3 axisY =
            cube.TransformVector(
                Vector3.up * localBounds.extents.y
            );

        Vector3 axisZ =
            cube.TransformVector(
                Vector3.forward * localBounds.extents.z
            );

        // ワールドY方向に対する投影半径
        float halfHeight =
            Mathf.Abs(Vector3.Dot(axisX, Vector3.up)) +
            Mathf.Abs(Vector3.Dot(axisY, Vector3.up)) +
            Mathf.Abs(Vector3.Dot(axisZ, Vector3.up));

        // キューブのBounds中心を手の下へ配置
        Vector3 desiredBoundsCenter =
            HandAttachPoint.position +
            Vector3.down * (halfHeight + spawnClearance) +
            HandAttachPoint.forward * spawnForwardOffset;

        // Pivotが中心でないモデルにも対応
        cube.position =
            desiredBoundsCenter -
            cube.TransformVector(localBounds.center);
    }

    // ==================================================
    // Trigger Press
    // ==================================================

    private void HandleTriggerPressed()
    {
        // Grip中でもTrigger発射を許可する
        if (!IsSelected || triggerHeld)
            return;

        RemoveDestroyedPieces();

        // ----------------------------------------------
        // 生成済みキューブが存在する場合は発射
        // ----------------------------------------------

        if (Pieces.Count > 0)
        {
            LaunchExistingPieces();
            return;
        }

        // ----------------------------------------------
        // 通常キューブを生成
        // ----------------------------------------------

        if (!CreatePreviewCube())
            return;

        triggerHeld = true;

        triggerHoldTime = 0f;

        menuOpenAttempted = false;

        Debug.Log($"Asteroid Trigger Pressed : {Side}");
    }

    // ==================================================
    // Create Preview Cube
    // ==================================================

    private bool CreatePreviewCube()
    {
        if (!ResolveOwnerInformation())
            return false;

        AsteroidCubePiece piece =
            CreateCubePieceObject(true);

        if (piece == null)
            return false;

        if (!piece.BeginFollowing(
                HandAttachPoint,
                followSmoothTime,
                followRotationSpeed))
        {
            DestroySpawned(piece.gameObject);
            return false;
        }

        Pieces.Add(piece);

        previewPiece = piece;

        isSplit = false;

        Debug.Log(
            $"Asteroid : Preview Cube Created [{Side}]"
        );

        return true;
    }

    // ==================================================
    // Trigger Release
    // ==================================================

    private void HandleTriggerReleased()
    {
        if (!triggerHeld)
            return;

        // スロット切り替え等による強制解除
        if (IsForceReleasingInput)
        {
            CancelTrigger();
            return;
        }

        triggerHeld = false;

        if (activeMenu != null)
        {
            int division =
                activeMenu.SelectedDivisionPerAxis;

            if (division > 1)
            {
                if (SplitPreviewCube(division))
                {
                    Debug.Log(
                        $"Asteroid : Split Completed - " +
                        $"{Pieces.Count} Cubes [{Side}]"
                    );
                }
            }
            else if (division == 1)
            {
                Debug.Log(
                    $"Asteroid : 1 Cube Confirmed [{Side}]"
                );
            }
            else
            {
                Debug.Log(
                    $"Asteroid : No Direction Selected [{Side}]"
                );
            }
        }
        else
        {
            Debug.Log(
                $"Asteroid : Normal Cube Confirmed [{Side}]"
            );
        }

        CloseMenu();

        triggerHoldTime = 0f;

        menuOpenAttempted = false;
    }

    // ==================================================
    // Open Menu
    // ==================================================

    private void OpenMenu()
    {
        if (splitMenuPrefab == null)
        {
            Debug.LogError(
                $"Asteroid [{Side}] : Split Menu Prefab is missing."
            );

            return;
        }

        if (HandAttachPoint == null || MenuAnchor == null)
        {
            Debug.LogError(
                $"Asteroid [{Side}] : HandAttachPoint or MenuAnchor is missing."
            );

            return;
        }

        // カメラ基準の左右方向
        Vector3 viewRight =
            Vector3.ProjectOnPlane(
                MenuAnchor.right,
                Vector3.up
            );

        if (viewRight.sqrMagnitude < 0.0001f)
        {
            Debug.LogError(
                "Asteroid : Invalid view direction."
            );

            return;
        }

        viewRight.Normalize();

        // メニューの基本向き
        Vector3 viewForward =
            Vector3.Cross(
                viewRight,
                Vector3.up
            ).normalized;

        Quaternion menuRotation =
            Quaternion.LookRotation(
                viewForward,
                Vector3.up
            );

        menuRotation *=
            Quaternion.Euler(menuLocalEulerAngles);

        Vector3 menuRight =
            Vector3.ProjectOnPlane(
                menuRotation * Vector3.right,
                Vector3.up
            ).normalized;

        Vector3 menuForward =
            menuRotation * Vector3.forward;

        // 手の現在位置
        Vector3 handPosition =
            HandAttachPoint.position;

        // メニューの表示位置
        Vector3 menuOffset =
            menuRight * menuLocalPosition.x +
            Vector3.up * menuLocalPosition.y +
            menuForward * menuLocalPosition.z;

        Vector3 menuPosition =
            handPosition + menuOffset;

        // 親を設定せずワールド空間に生成
        activeMenu = Instantiate(splitMenuPrefab);

        activeMenu.transform.SetPositionAndRotation(
            menuPosition,
            menuRotation
        );

        // 表示方向と選択方向を一致させる
        activeMenu.Begin(
            handPosition,
            menuRight
        );

        Debug.Log(
            $"Asteroid Menu Opened : {Side}"
        );
    }

    // ==================================================
    // Split Preview Cube
    // ==================================================

    private bool SplitPreviewCube(int divisionCount)
    {
        if (previewPiece == null ||
            HandAttachPoint == null)
        {
            return false;
        }

        if (divisionCount <= 1)
        {
            isSplit = false;
            return true;
        }

        Transform root = previewPiece.transform;

        // ----------------------------------------------
        // 分割前の情報を保存
        // ----------------------------------------------

        Matrix4x4 originalMatrix =
            root.localToWorldMatrix;

        Vector3 originalWorldPosition =
            root.position;

        Vector3 originalScale =
            root.localScale;

        Quaternion originalWorldRotation =
            root.rotation;

        if (!TryCalculateRootLocalBounds(
                root,
                out Bounds localBounds))
        {
            Debug.LogError(
                "Asteroid : Cube Bounds are invalid.",
                root
            );

            return false;
        }

        Vector3 boundsSize = localBounds.size;

        // 分割前のキューブのBounds中心（ワールド座標）
        Vector3 originalBoundsCenterWorld =
            originalMatrix.MultiplyPoint3x4(
                localBounds.center
            );

        // HMD基準の水平方向
        Vector3 splitForward =
            MenuAnchor != null
                ? Vector3.ProjectOnPlane(
                    MenuAnchor.forward,
                    Vector3.up
                )
                : Vector3.forward;

        if (splitForward.sqrMagnitude < 0.0001f)
        {
            splitForward = Vector3.forward;
        }

        splitForward.Normalize();

        // HMD基準の右方向
        Vector3 splitRight =
            Vector3.Cross(
                Vector3.up,
                splitForward
            ).normalized;

        // 上方向はワールドの真上
        Vector3 splitUp = Vector3.up;

        // 元のBoundsの各軸のワールドサイズ
        Vector3 worldSize = new Vector3(
            originalMatrix.MultiplyVector(
                Vector3.right * boundsSize.x
            ).magnitude,

            originalMatrix.MultiplyVector(
                Vector3.up * boundsSize.y
            ).magnitude,

            originalMatrix.MultiplyVector(
                Vector3.forward * boundsSize.z
            ).magnitude
        );

        // 1区画あたりのワールドサイズ
        Vector3 cellWorldSize =
            worldSize / divisionCount;

        int totalCount =
            divisionCount *
            divisionCount *
            divisionCount;

        List<AsteroidCubePiece> splitPieces =
            new List<AsteroidCubePiece>(totalCount);

        List<AsteroidCubePiece> createdClones =
            new List<AsteroidCubePiece>();

        int createdCount = 0;

        float gapRatio =
            Mathf.Clamp(splitGapRatio, 0f, 0.25f);

        float randomRatio =
            Mathf.Clamp01(splitRandomOffsetRatio);

        // ----------------------------------------------
        // N x N x Nで配置
        // ----------------------------------------------

        for (int x = 0; x < divisionCount; x++)
        {
            for (int y = 0; y < divisionCount; y++)
            {
                for (int z = 0; z < divisionCount; z++)
                {
                    AsteroidCubePiece piece;

                    // 最初の1個は元のキューブを利用
                    if (createdCount == 0)
                    {
                        piece = previewPiece;
                    }
                    else
                    {
                        piece = CreateCubePieceObject(
                            false,
                            originalWorldPosition,
                            originalWorldRotation
                        );

                        if (piece == null)
                        {
                            RestoreFailedSplit(
                                createdClones,
                                originalWorldPosition,
                                originalWorldRotation,
                                originalScale
                            );

                            return false;
                        }

                        createdClones.Add(piece);
                    }

                    Transform pieceTransform =
                        piece.transform;

                    // ワールド空間で管理
                    pieceTransform.SetParent(null, true);

                    pieceTransform.rotation =
                        originalWorldRotation;

                    // ----------------------------------
                    // 分割数に応じて縮小
                    // ----------------------------------

                    pieceTransform.localScale =
                        originalScale *
                        ((1f - gapRatio) / divisionCount);

                    // ----------------------------------
                    // 分割アニメーションの開始位置
                    // ----------------------------------

                    pieceTransform.position =
                        originalWorldPosition;

                    // 現在位置から手への追従を開始
                    if (!piece.BeginFollowing(
                            HandAttachPoint,
                            followSmoothTime,
                            followRotationSpeed))
                    {
                        RestoreFailedSplit(
                            createdClones,
                            originalWorldPosition,
                            originalWorldRotation,
                            originalScale
                        );

                        return false;
                    }

                    // ----------------------------------
                    // 格子状の最終位置を計算
                    // ----------------------------------

                    // 分割位置をHMD基準のワールド座標で計算
                    Vector3 cellOffset = new Vector3(
                        ((x + 0.5f) / divisionCount - 0.5f)
                            * worldSize.x,

                        ((y + 0.5f) / divisionCount - 0.5f)
                            * worldSize.y,

                        ((z + 0.5f) / divisionCount - 0.5f)
                            * worldSize.z
                    );

                    // キューブ間に隙間を作る
                    cellOffset *= 1f + gapRatio;

                    // ランダムな位置ずれ
                    Vector3 randomOffset = new Vector3(
                        Random.Range(
                            -cellWorldSize.x * randomRatio,
                            cellWorldSize.x * randomRatio
                        ),

                        Random.Range(
                            -cellWorldSize.y * randomRatio,
                            cellWorldSize.y * randomRatio
                        ),

                        Random.Range(
                            -cellWorldSize.z * randomRatio,
                            cellWorldSize.z * randomRatio
                        )
                    );

                    // HMD基準の配置方向へ変換
                    Vector3 desiredCenter =
                        originalBoundsCenterWorld +

                        splitRight * (cellOffset.x + randomOffset.x) +

                        splitUp * (cellOffset.y + randomOffset.y) +

                        splitForward * (cellOffset.z + randomOffset.z);


                    // Pivotが中心以外の場合の補正
                    Vector3 desiredRootPosition =
                        desiredCenter -
                        pieceTransform.TransformVector(
                            localBounds.center
                        );

                    // 手基準の相対位置へ変換
                    Vector3 targetLocalPosition =
                        HandAttachPoint.InverseTransformPoint(
                            desiredRootPosition
                        );

                    // ----------------------------------
                    // 分割アニメーション開始
                    // ----------------------------------

                    float randomDelay =
                        Random.Range(
                            0f,
                            splitStaggerMax
                        );

                    piece.AnimateFollowOffset(
                        targetLocalPosition,
                        splitSpreadDuration,
                        randomDelay
                    );

                    splitPieces.Add(piece);

                    createdCount++;
                }
            }
        }

        // ----------------------------------------------
        // 管理リスト更新
        // ----------------------------------------------

        Pieces.Clear();

        Pieces.AddRange(splitPieces);

        isSplit = divisionCount > 1;

        return true;
    }

    // ==================================================
    // Split Recovery
    // ==================================================

    private void RestoreFailedSplit(
        List<AsteroidCubePiece> createdClones,
        Vector3 originalWorldPosition,
        Quaternion originalWorldRotation,
        Vector3 originalScale)
    {
        foreach (AsteroidCubePiece piece in createdClones)
        {
            if (piece != null)
            {
                DestroySpawned(piece.gameObject);
            }
        }

        if (previewPiece != null)
        {
            Transform root = previewPiece.transform;

            root.SetParent(null, true);

            root.position = originalWorldPosition;
            root.rotation = originalWorldRotation;
            root.localScale = originalScale;

            previewPiece.BeginFollowing(
                HandAttachPoint,
                followSmoothTime,
                followRotationSpeed
            );
        }

        Pieces.Clear();

        if (previewPiece != null)
            Pieces.Add(previewPiece);

        isSplit = false;
    }

    // ==================================================
    // Calculate Bounds
    // ==================================================

    private static bool TryCalculateRootLocalBounds(
        Transform root,
        out Bounds result)
    {
        result = new Bounds();

        if (root == null)
            return false;

        // RootのBoxColliderを優先
        BoxCollider rootBox =
            root.GetComponent<BoxCollider>();

        if (rootBox != null)
        {
            result = new Bounds(
                rootBox.center,
                rootBox.size
            );

            return result.size.sqrMagnitude > 0.000001f;
        }

        bool found = false;

        // RendererからBounds取得
        Renderer[] renderers =
            root.GetComponentsInChildren<Renderer>(true);

        foreach (Renderer renderer in renderers)
        {
            EncapsulateLocalBounds(
                root,
                renderer.transform,
                renderer.localBounds,
                ref found,
                ref result
            );
        }

        // Rendererがない場合はColliderから取得
        if (!found)
        {
            Collider[] colliders =
                root.GetComponentsInChildren<Collider>(true);

            foreach (Collider collider in colliders)
            {
                EncapsulateWorldBounds(
                    root,
                    collider.bounds,
                    ref found,
                    ref result
                );
            }
        }

        return found &&
               result.size.sqrMagnitude > 0.000001f;
    }

    private static void EncapsulateLocalBounds(
        Transform root,
        Transform source,
        Bounds bounds,
        ref bool found,
        ref Bounds result)
    {
        Vector3 min = bounds.min;
        Vector3 max = bounds.max;

        for (int x = 0; x < 2; x++)
        {
            for (int y = 0; y < 2; y++)
            {
                for (int z = 0; z < 2; z++)
                {
                    Vector3 localPoint = new Vector3(
                        x == 0 ? min.x : max.x,
                        y == 0 ? min.y : max.y,
                        z == 0 ? min.z : max.z
                    );

                    Vector3 worldPoint =
                        source.TransformPoint(localPoint);

                    Vector3 rootPoint =
                        root.InverseTransformPoint(worldPoint);

                    AddBoundsPoint(
                        rootPoint,
                        ref found,
                        ref result
                    );
                }
            }
        }
    }

    private static void EncapsulateWorldBounds(
        Transform root,
        Bounds bounds,
        ref bool found,
        ref Bounds result)
    {
        Vector3 min = bounds.min;
        Vector3 max = bounds.max;

        for (int x = 0; x < 2; x++)
        {
            for (int y = 0; y < 2; y++)
            {
                for (int z = 0; z < 2; z++)
                {
                    Vector3 worldPoint = new Vector3(
                        x == 0 ? min.x : max.x,
                        y == 0 ? min.y : max.y,
                        z == 0 ? min.z : max.z
                    );

                    Vector3 rootPoint =
                        root.InverseTransformPoint(worldPoint);

                    AddBoundsPoint(
                        rootPoint,
                        ref found,
                        ref result
                    );
                }
            }
        }
    }

    private static void AddBoundsPoint(
        Vector3 point,
        ref bool found,
        ref Bounds result)
    {
        if (!found)
        {
            result = new Bounds(
                point,
                Vector3.zero
            );

            found = true;
        }
        else
        {
            result.Encapsulate(point);
        }
    }

    // ==================================================
    // Launch
    // ==================================================

    private void LaunchExistingPieces()
    {
        RemoveDestroyedPieces();

        if (Pieces.Count == 0)
            return;

        Vector3 direction =
            HandAttachPoint != null
                ? HandAttachPoint.forward
                : runtimeOwnerRoot != null
                    ? runtimeOwnerRoot.forward
                    : Vector3.forward;

        int requestedCount = Pieces.Count;

        List<AsteroidCubePiece> launchedPieces =
            new List<AsteroidCubePiece>();

        foreach (AsteroidCubePiece piece in Pieces)
        {
            if (piece == null)
                continue;

            // 追従中・浮遊中・散開中のすべてから発射可能
            if (piece.RequestLaunch(direction))
            {
                launchedPieces.Add(piece);
            }
        }

        // 発射要求に成功したものだけ管理から除外
        foreach (AsteroidCubePiece piece in launchedPieces)
        {
            Pieces.Remove(piece);
        }

        Debug.Log(
            $"Asteroid : Launch Requested " +
            $"{launchedPieces.Count}/{requestedCount} [{Side}]"
        );

        if (Pieces.Count == 0)
        {
            ReleasePieceOwnership();
        }
        else
        {
            if (previewPiece == null ||
                !Pieces.Contains(previewPiece))
            {
                previewPiece = Pieces[0];
            }
        }
    }

    // ==================================================
    // Grip Press
    // ==================================================

    private void HandleGripPressed()
    {
        if (!IsSelected ||
            gripPressing ||
            triggerHeld)
        {
            return;
        }

        RemoveDestroyedPieces();

        if (Pieces.Count == 0)
            return;

        gripPressing = true;

        gripHoldTime = 0f;

        // 押した瞬間に追従を解除して浮遊状態にする
        foreach (AsteroidCubePiece piece in Pieces)
        {
            if (piece == null)
                continue;

            piece.DetachAndHold();
        }

        Debug.Log(
            $"Asteroid Grip Pressed : {Side}"
        );
    }

    // ==================================================
    // Grip Release
    // ==================================================

    private void HandleGripReleased()
    {
        if (!gripPressing)
            return;

        if (IsForceReleasingInput)
        {
            gripPressing = false;
            gripHoldTime = 0f;
            return;
        }

        gripPressing = false;

        bool shouldScatter =
            isSplit &&
            gripHoldTime >= scatterHoldSeconds;

        if (shouldScatter)
        {
            ScatterPieces();
        }
        else
        {
            HoldPieces();
        }

        gripHoldTime = 0f;
    }

    // ==================================================
    // Hold
    // ==================================================

    private void HoldPieces()
    {
        Debug.Log(
            $"Asteroid : Float {Pieces.Count} Cubes [{Side}]"
        );

        foreach (AsteroidCubePiece piece in Pieces)
        {
            if (piece == null)
                continue;

            // その場に浮遊させる
            piece.DropAtCurrentPosition();
        }

        // 重要：
        // 所有権・管理リストは解除しない。
        // その後のTrigger発射を可能にする。
    }

    // ==================================================
    // Scatter
    // ==================================================

    private void ScatterPieces()
    {
        Debug.Log(
            $"Asteroid : Scatter {Pieces.Count} Cubes [{Side}]"
        );

        foreach (AsteroidCubePiece piece in Pieces)
        {
            if (piece == null)
                continue;

            piece.Scatter(
                scatterRadius,
                scatterDuration
            );
        }

        // 重要：
        // 散開後もPiecesに保持する。
        // その後のTrigger発射を可能にする。
    }

    // ==================================================
    // Release Ownership
    // ==================================================

    private void ReleasePieceOwnership()
    {
        Pieces.Clear();

        previewPiece = null;

        isSplit = false;

        runtimeOwnerInitialized = false;
        runtimeOwnerId = 0;
        runtimeOwnerRoot = null;
    }

    // ==================================================
    // Tick
    // ==================================================

    protected override void OnTick(float deltaTime)
    {
        if (!IsSelected)
            return;

        // Trigger長押し
        if (triggerHeld)
        {
            triggerHoldTime += deltaTime;

            if (!menuOpenAttempted &&
                triggerHoldTime >= menuHoldSeconds)
            {
                menuOpenAttempted = true;

                OpenMenu();
            }

            if (activeMenu != null &&
                HandAttachPoint != null)
            {
                activeMenu.UpdateSelection(
                    HandAttachPoint.position
                );
            }
        }

        // Grip長押し
        if (gripPressing)
        {
            gripHoldTime += deltaTime;
        }
    }

    // ==================================================
    // Remove Destroyed Pieces
    // ==================================================

    private void RemoveDestroyedPieces()
    {
        Pieces.RemoveAll(
            piece => piece == null
        );

        if (Pieces.Count == 0)
        {
            previewPiece = null;
            isSplit = false;

            runtimeOwnerInitialized = false;
            runtimeOwnerId = 0;
            runtimeOwnerRoot = null;
        }
        else if (previewPiece == null)
        {
            previewPiece = Pieces[0];
        }
    }

    // ==================================================
    // Cleanup
    // ==================================================

    private void CleanupRuntimeObjects()
    {
        CloseMenu();

        foreach (AsteroidCubePiece piece in Pieces)
        {
            if (piece == null)
                continue;

            DestroySpawned(piece.gameObject);
        }

        ReleasePieceOwnership();
    }

    // ==================================================
    // Menu
    // ==================================================

    private void CloseMenu()
    {
        if (activeMenu == null)
            return;

        Destroy(activeMenu.gameObject);

        activeMenu = null;

        Debug.Log(
            $"Asteroid Menu Closed : {Side}"
        );
    }

    private void CancelTrigger()
    {
        CloseMenu();

        triggerHeld = false;

        triggerHoldTime = 0f;

        menuOpenAttempted = false;

        Debug.Log(
            $"Asteroid Trigger Canceled : {Side}"
        );
    }

    // ==================================================
    // Reset
    // ==================================================

    private void ResetInputState()
    {
        triggerHeld = false;
        triggerHoldTime = 0f;
        menuOpenAttempted = false;

        gripPressing = false;
        gripHoldTime = 0f;
    }

    // ==================================================
    // Unregistered
    // ==================================================

    protected override void OnUnregistered()
    {
        ResetInputState();

        CleanupRuntimeObjects();

        Debug.Log(
            $"Asteroid Unregistered : {Side}"
        );
    }
}