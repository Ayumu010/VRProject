
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using Effekseer;

/// <summary>
/// アステロイド弾の初期化・飛翔・衝突・寿命を管理する。
/// </summary>
public class AsteroidProjectileController : MonoBehaviour
{
    [System.Serializable]
    public class ProjectileHitEvent : UnityEvent<Collider, Vector3>
    {
    }

    [Header("Effekseer")]
    [SerializeField]
    private EffekseerEmitter projectileEmitter;

    [SerializeField, Min(1)]
    private int initializeFrames = 3;

    [Header("Flight")]
    [SerializeField, Min(0f)]
    private float speed = 20f;

    [SerializeField, Min(0.01f)]
    private float lifetime = 5f;

    [Header("Collision")]
    [SerializeField, Min(0.001f)]
    private float collisionRadius = 0.1f;

    [SerializeField]
    private LayerMask collisionLayers = ~0;

    [SerializeField]
    private QueryTriggerInteraction triggerInteraction =
        QueryTriggerInteraction.Collide;

    [Header("Hit Event")]
    [SerializeField]
    private ProjectileHitEvent onHit =
        new ProjectileHitEvent();

    private Vector3 flightDirection;

    private bool initializationStarted;
    private bool isFlying;
    private bool isFinished;

    private float flightElapsed;

    private void Awake()
    {
        // EffekseerEmitterを取得
        if (projectileEmitter == null)
        {
            projectileEmitter =
                GetComponentInChildren<EffekseerEmitter>(true);
        }

        // 自動再生・ループ再生を無効化
        if (projectileEmitter != null)
        {
            projectileEmitter.playOnStart = false;
            projectileEmitter.isLooping = false;
        }
    }

    /// <summary>
    /// 外部から飛翔方向を受け取って初期化を開始する。
    /// </summary>
    public void Initialize(Vector3 direction)
    {
        if (initializationStarted || isFinished)
        {
            return;
        }

        initializationStarted = true;

        if (projectileEmitter == null)
        {
            Debug.LogError(
                "ProjectileEmitterが設定されていません。",
                this
            );

            FinishProjectile();
            return;
        }

        if (projectileEmitter.effectAsset == null)
        {
            Debug.LogError(
                "ProjectileEmitterのEffect Assetが設定されていません。",
                this
            );

            FinishProjectile();
            return;
        }

        // 飛翔方向を決定
        if (direction.sqrMagnitude < 0.0001f)
        {
            direction = transform.forward;
        }

        flightDirection = direction.normalized;

        StartCoroutine(InitializeRoutine());
    }

    /// <summary>
    /// Effectを数フレーム再生して一時停止した後、飛翔を開始する。
    /// </summary>
    private IEnumerator InitializeRoutine()
    {
        // 念のため一時停止を解除
        projectileEmitter.paused = false;

        // Effekseer再生
        projectileEmitter.Play();

        Debug.Log("Asteroid Projectile : Effect Started", this);

        // 指定フレーム数だけ待つ
        int frames = Mathf.Max(1, initializeFrames);

        for (int i = 0; i < frames; i++)
        {
            yield return null;
        }

        // 現在のエフェクト状態を維持して一時停止
        projectileEmitter.paused = true;

        Debug.Log("Asteroid Projectile : Effect Paused", this);

        // 飛翔開始
        flightElapsed = 0f;
        isFlying = true;

        Debug.Log("Asteroid Projectile : Flying", this);
    }

    private void Update()
    {
        if (!isFlying || isFinished)
        {
            return;
        }

        UpdateFlight();
    }

    /// <summary>
    /// 飛翔・衝突・寿命を更新する。
    /// </summary>
    private void UpdateFlight()
    {
        float deltaTime = Time.deltaTime;

        if (deltaTime <= 0f)
        {
            return;
        }

        // 寿命までの残り時間
        float remainingTime = lifetime - flightElapsed;

        if (remainingTime <= 0f)
        {
            FinishProjectile();
            return;
        }

        float stepTime = Mathf.Min(deltaTime, remainingTime);

        // 今フレームの移動距離
        float moveDistance = speed * stepTime;

        // 移動経路上の衝突判定
        if (
            moveDistance > 0f &&
            Physics.SphereCast(
                transform.position,
                collisionRadius,
                flightDirection,
                out RaycastHit hit,
                moveDistance,
                collisionLayers,
                triggerInteraction
            )
        )
        {
            // 命中位置まで移動
            transform.position +=
                flightDirection * hit.distance;

            HandleHit(hit);

            return;
        }

        // 衝突していなければ移動
        transform.position +=
            flightDirection * moveDistance;

        // 飛翔時間を更新
        flightElapsed += stepTime;

        // 寿命判定
        if (flightElapsed >= lifetime)
        {
            FinishProjectile();
        }
    }

    /// <summary>
    /// 衝突時の処理。
    /// </summary>
    private void HandleHit(RaycastHit hit)
    {
        if (isFinished)
        {
            return;
        }

        Debug.Log(
            $"Asteroid Projectile : Hit {hit.collider.name}",
            hit.collider
        );

        // 命中イベントを通知
        onHit?.Invoke(hit.collider, hit.point);

        // 弾を終了
        FinishProjectile();
    }

    /// <summary>
    /// 弾を終了する。
    /// </summary>
    private void FinishProjectile()
    {
        if (isFinished)
        {
            return;
        }

        isFinished = true;
        isFlying = false;

        // Effekseerを完全停止
        if (projectileEmitter != null)
        {
            projectileEmitter.Stop();
        }

        Debug.Log("Asteroid Projectile : Finished", this);

        Destroy(gameObject);
    }
}
