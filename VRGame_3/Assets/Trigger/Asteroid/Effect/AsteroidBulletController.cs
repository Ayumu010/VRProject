using System.Collections;
using UnityEngine;
using Effekseer;

public class AsteroidBulletController : MonoBehaviour
{
    [Header("Cube Visual")]
    [SerializeField]
    private Transform cubeVisual;

    [SerializeField, Min(0f)]
    private float minScale = 0.1f;

    [SerializeField, Min(0f)]
    private float scaleDuration = 0.5f;

    [Header("Cube Effect")]
    [SerializeField]
    private EffekseerEmitter cubeEffect;

    [SerializeField, Min(1)]
    private int cubeInitializeFrames = 3;

    [Header("Flash Effect")]
    [SerializeField]
    private EffekseerEmitter flashEffect;

    [SerializeField, Min(0f)]
    private float flashDuration = 0.5f;

    [Header("Projectile")]
    [SerializeField]
    private GameObject projectilePrefab;

    [SerializeField]
    private Transform projectileSpawnPoint;

    [Header("Debug")]
    [SerializeField]
    private bool debugAutoFire = true;

    [SerializeField, Min(0f)]
    private float debugWaitAfterReady = 2f;

    private Vector3 maxScale;

    private bool scaleAnimationFinished;
    private bool fireRequested;
    private bool sequenceFinished;

    // Playerから受け取る発射者情報
    private ulong ownerId;
    private Transform ownerRoot;
    private bool ownerInitialized;

    private void Awake()
    {
        if (cubeVisual != null)
        {
            maxScale = cubeVisual.localScale;
        }
    }

    /// <summary>
    /// Cubeを生成したPlayerから発射者情報を受け取る
    /// </summary>
    public void InitializeOwner(
        ulong ownerId,
        Transform ownerRoot)
    {
        if (ownerRoot == null)
        {
            Debug.LogError(
                "OwnerRootが設定されていません。",
                this
            );

            ownerInitialized = false;
            return;
        }

        this.ownerId = ownerId;
        this.ownerRoot = ownerRoot;
        ownerInitialized = true;
    }

    private void Start()
    {
        if (!ValidateSettings())
        {
            enabled = false;
            return;
        }

        cubeVisual.localScale = maxScale * minScale;

        StartCoroutine(AnimateCubeScale());
        StartCoroutine(Sequence());
    }

    /// <summary>
    /// Inspector設定の確認
    /// </summary>
    private bool ValidateSettings()
    {
        if (cubeVisual == null)
        {
            Debug.LogError(
                "CubeVisualが設定されていません。",
                this
            );
            return false;
        }

        if (cubeVisual == transform ||
            !cubeVisual.IsChildOf(transform))
        {
            Debug.LogError(
                "CubeVisualにはAsteroidBulletの子オブジェクトを設定してください。",
                this
            );
            return false;
        }

        if (cubeEffect == null)
        {
            Debug.LogError(
                "CubeEffectが設定されていません。",
                this
            );
            return false;
        }

        if (flashEffect == null)
        {
            Debug.LogError(
                "FlashEffectが設定されていません。",
                this
            );
            return false;
        }

        if (projectilePrefab == null)
        {
            Debug.LogError(
                "ProjectilePrefabが設定されていません。",
                this
            );
            return false;
        }

        // ProjectileControllerの存在確認
        if (!projectilePrefab.TryGetComponent<ProjectileController>(
            out _))
        {
            Debug.LogError(
                "ProjectilePrefabのRootにProjectileControllerがありません。",
                this
            );
            return false;
        }

        // ProjectileCollisionSystemの存在確認
        if (!projectilePrefab.TryGetComponent<ProjectileCollisionSystem>(
            out _))
        {
            Debug.LogError(
                "ProjectilePrefabのRootにProjectileCollisionSystemがありません。",
                this
            );
            return false;
        }

        return true;
    }

    /// <summary>
    /// Cubeの拡大アニメーション
    /// </summary>
    private IEnumerator AnimateCubeScale()
    {
        Vector3 startScale = maxScale * minScale;

        if (scaleDuration <= 0f)
        {
            cubeVisual.localScale = maxScale;
            scaleAnimationFinished = true;
            yield break;
        }

        float elapsed = 0f;

        while (elapsed < scaleDuration)
        {
            elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(
                elapsed / scaleDuration
            );

            cubeVisual.localScale = Vector3.Lerp(
                startScale,
                maxScale,
                t
            );

            yield return null;
        }

        cubeVisual.localScale = maxScale;
        scaleAnimationFinished = true;

        Debug.Log(
            "Asteroid : Cube Scale Completed",
            this
        );
    }

    /// <summary>
    /// 外部から発射を要求する
    /// </summary>
    public void RequestFire()
    {
        if (sequenceFinished)
            return;

        fireRequested = true;
    }

    /// <summary>
    /// 発射方向を指定して発射を要求する。
    /// 既存のRequestFire()はそのまま維持する。
    /// </summary>
    public void RequestFire(Vector3 direction)
    {
        if (sequenceFinished)
            return;

        if (direction.sqrMagnitude > 0.0001f)
        {
            direction.Normalize();

            Vector3 up = Vector3.up;

            // 発射方向と上方向がほぼ平行の場合の補正
            if (Mathf.Abs(Vector3.Dot(direction, up)) > 0.999f)
            {
                up = Vector3.forward;
            }

            Quaternion rotation =
                Quaternion.LookRotation(direction, up);

            // 既存Sequenceが参照する位置の回転を変更
            if (projectileSpawnPoint != null)
            {
                projectileSpawnPoint.rotation = rotation;
            }
            else
            {
                transform.rotation = rotation;
            }
        }

        fireRequested = true;
    }

    /// <summary>
    /// Projectileを生成して発射する
    /// </summary>
    private bool SpawnAndLaunchProjectile(
        Vector3 spawnPosition,
        Quaternion spawnRotation
    )
    {
        // 発射者情報の確認
        if (!ownerInitialized || ownerRoot == null)
        {
            Debug.LogError(
                "発射者情報が初期化されていません。" +
                "InitializeOwner(ownerId, ownerRoot)を呼び出してください。",
                this
            );

            return false;
        }

        // AsteroidBullet自身のワールドスケールを取得
        Vector3 projectileScale = transform.lossyScale;

        // Projectileを生成
        GameObject projectileObject = Instantiate(
            projectilePrefab,
            spawnPosition,
            spawnRotation
        );

        // AsteroidBulletと同じスケールに設定
        projectileObject.transform.localScale =
            projectileScale;

        // ProjectileCollisionSystemを取得
        if (!projectileObject.TryGetComponent<ProjectileCollisionSystem>(
            out ProjectileCollisionSystem collisionSystem))
        {
            Debug.LogError(
                "生成したProjectileにProjectileCollisionSystemがありません。",
                projectileObject
            );

            Destroy(projectileObject);
            return false;
        }

        // ProjectileControllerを取得
        if (!projectileObject.TryGetComponent<ProjectileController>(
            out ProjectileController projectileController))
        {
            Debug.LogError(
                "生成したProjectileにProjectileControllerがありません。",
                projectileObject
            );

            Destroy(projectileObject);
            return false;
        }

        // 発射者情報をProjectileへ引き継ぐ
        // 必ずLaunchより前に実行する
        collisionSystem.Initialize(
            ownerId,
            ownerRoot
        );

        // 発射方向を取得
        Vector3 direction =
            spawnRotation * Vector3.forward;

        // 発射元AsteroidBulletのColliderを取得
        Collider[] sourceColliders =
            GetComponentsInChildren<Collider>(true);

        // 弾を発射
        projectileController.Launch(
            direction,
            sourceColliders
        );

        Debug.Log(
            $"Asteroid : Projectile Launched / OwnerId : {ownerId} / Scale : {projectileScale}",
            projectileObject
        );

        return true;
    }

    /// <summary>
    /// メイン処理
    /// </summary>
    private IEnumerator Sequence()
    {
        // ① Cube Effectを再生して一時停止
        yield return AsteroidEffekseerControl.PlayAndPause(
            cubeEffect,
            cubeInitializeFrames
        );

        Debug.Log(
            "Asteroid : Cube Effect Ready",
            this
        );

        // ② Cubeの拡大完了を待つ
        while (!scaleAnimationFinished)
        {
            yield return null;
        }

        Debug.Log(
            "Asteroid : Ready",
            this
        );

        // ③ デバッグ用自動発射
        if (debugAutoFire)
        {
            float elapsed = 0f;

            while (
                !fireRequested &&
                elapsed < debugWaitAfterReady
            )
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            if (!fireRequested)
            {
                RequestFire();
            }
        }

        // ④ 発射要求を待機
        while (!fireRequested)
        {
            yield return null;
        }

        if (sequenceFinished)
            yield break;

        sequenceFinished = true;

        Debug.Log(
            "Asteroid : Fire Requested",
            this
        );

        // ⑤ 弾の生成位置と向きを先に保存
        Vector3 spawnPosition = projectileSpawnPoint != null
            ? projectileSpawnPoint.position
            : transform.position;

        Quaternion spawnRotation = projectileSpawnPoint != null
            ? projectileSpawnPoint.rotation
            : transform.rotation;

        // ⑥ Flash Effectの再生開始
        Coroutine flashCoroutine = StartCoroutine(
            AsteroidEffekseerControl.PlayForSecondsAndStop(
                flashEffect,
                flashDuration
            )
        );

        Debug.Log(
            "Asteroid : Flash Started",
            this
        );

        // ⑦ Cube Effectを完全停止
        if (cubeEffect != null)
        {
            cubeEffect.paused = false;
            cubeEffect.Stop();
        }

        // ⑧ Cube Visualを即時非表示にして削除
        if (cubeVisual != null)
        {
            cubeVisual.gameObject.SetActive(false);
            Destroy(cubeVisual.gameObject);
        }

        Debug.Log(
            "Asteroid : Cube Visual Deleted",
            this
        );

        // ⑨ Projectileを生成して発射
        bool projectileLaunched = SpawnAndLaunchProjectile(
            spawnPosition,
            spawnRotation
        );

        if (projectileLaunched)
        {
            Debug.Log(
                "Asteroid : Projectile Created",
                this
            );
        }

        // ⑩ フラッシュ終了まで待つ
        // Cubeを先にDestroyするとCoroutineが停止するため、
        // この時点ではCubeを削除しない
        if (flashCoroutine != null)
        {
            yield return flashCoroutine;
        }

        Debug.Log(
            "Asteroid : Flash Finished",
            this
        );

        // ⑪ AsteroidBulletを削除
        Destroy(gameObject);
    }
}