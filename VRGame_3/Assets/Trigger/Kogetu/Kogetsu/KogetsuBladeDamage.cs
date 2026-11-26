using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 刀身の前フレームから現在フレームへの移動を複数点でスイープし、
/// IKogetsuDamageReceiver を実装した対象へ一振りにつき一度だけ通知する。
/// 物理押し合い用Colliderとは別の判定経路であり、ColliderをTrigger化する必要はない。
/// </summary>
[DisallowMultipleComponent]
public sealed class KogetsuBladeDamage : MonoBehaviour
{
    [Header("Blade Points")]
    [SerializeField]
    private Transform bladeBase;

    [SerializeField]
    private Transform bladeTip;

    [Header("Damage")]
    [SerializeField, Min(0f)]
    private float baseDamage = 10f;

    [SerializeField, Min(0f)]
    private float minimumSwingSpeed = 1f;

    [SerializeField, Min(0.001f)]
    private float sweepRadius = 0.035f;

    [SerializeField, Min(0.01f)]
    private float maximumSampleSpacing = 0.07f;

    [SerializeField, Range(2, 128)]
    private int maximumSampleCount = 96;

    [SerializeField, Min(0.01f)]
    private float swingResetDelay = 0.15f;

    [SerializeField]
    private LayerMask damageLayers = ~0;

    [Header("Debug")]
    [SerializeField]
    private bool logDamageHits;

    private readonly HashSet<int> hitReceiverIds = new HashSet<int>();
    private readonly Collider[] overlapBuffer = new Collider[48];
    private readonly RaycastHit[] castBuffer = new RaycastHit[48];
    private readonly List<MonoBehaviour> receiverBehaviourBuffer = new List<MonoBehaviour>(8);

    private Vector3[] previousSamplePoints;
    private Vector3[] currentSamplePoints;
    private int currentSampleCount;
    private KogetsuWeapon weapon;
    private KogetsuWeaponManager weaponManager;

    private float senkuExtensionLength;
    private float currentDamageMultiplier = 1f;
    // 前フレームの実際の刀身位置。
    // 旋空の延長が有効になっても、この履歴は維持する。
    private Vector3 previousBasePosition;
    private Vector3 previousTipPosition; private bool senkuActive;
    private float currentSenkuTrailMinimumSwingSpeed = 1.5f;
    private bool hasHistory;
    private bool attackWindowOpen;
    private float idleTime;

    private void Awake()
    {
        if (weapon == null)
            weapon = GetComponent<KogetsuWeapon>();
    }

    public void Initialize(KogetsuWeapon ownerWeapon, KogetsuWeaponManager manager)
    {
        weapon = ownerWeapon;
        weaponManager = manager;
        ResetSampling();
    }

    public void SetSenkuParameters(
        bool active,
        float extensionLength,
        float damageMultiplier,
        float senkuMinimumSwingSpeed)
    {
        senkuActive = active;

        senkuExtensionLength = active
            ? Mathf.Max(0f, extensionLength)
            : 0f;

        currentDamageMultiplier = active
            ? Mathf.Max(0.01f, damageMultiplier)
            : 1f;

        currentSenkuTrailMinimumSwingSpeed = active
            ? Mathf.Max(0f, senkuMinimumSwingSpeed)
            : 0f;

        // 剣速の発動条件は KogetsuWeaponManager 側で判定する。
        // 通常攻撃の minimumSwingSpeed は変更しない。
        //
        // 重要：ここで ResetSampling() を呼ばない。
        // 履歴を残すことで旋空発動直後の一振りを判定できる。
    }

    private void LateUpdate()
    {
        if (weapon == null ||
            weaponManager == null ||
            bladeBase == null ||
            bladeTip == null ||
            !weapon.CanDealDamageNow())
        {
            ResetSampling();
            return;
        }

        if (weapon != null)
            weapon.SetSenkuTrailEmitting(false);

        // 現在の実際の刀身位置を取得
        Vector3 currentBasePosition = bladeBase.position;
        Vector3 currentTipPosition = bladeTip.position;

        Vector3 currentDirection =
            (currentTipPosition - currentBasePosition).normalized;

        if (currentDirection.sqrMagnitude < 0.0001f)
        {
            ResetSampling();
            return;
        }

        // 初回は履歴だけ保存する
        if (!hasHistory)
        {
            previousBasePosition = currentBasePosition;
            previousTipPosition = currentTipPosition;
            hasHistory = true;
            return;
        }

        float deltaTime = Mathf.Max(Time.deltaTime, 0.0001f);

        // 旋空の延長を含まない、実際の刀身の速度を計測する
        float swingSpeed = CalculatePhysicalSwingSpeed(
            previousBasePosition,
            previousTipPosition,
            currentBasePosition,
            currentTipPosition,
            deltaTime,
            out Vector3 swingDirection);

        // Armed 中で剣速条件を満たした場合、
        // Manager が Active へ移行させる。
        // Active になると senkuExtensionLength がここで更新される。
        if (swingSpeed >= 0.05f)
        {
            weaponManager.TryActivateArmedSenku(swingSpeed);
        }

        // 旋空Active中かつ、剣速が条件を満たしている間だけTrailを発生させる。
        weapon.SetSenkuTrailEmitting(
            senkuActive &&
            swingSpeed >= Mathf.Max(
                0.05f,
                currentSenkuTrailMinimumSwingSpeed));

        // 前フレームと現在フレームの刀身方向
        Vector3 previousDirection =
            (previousTipPosition - previousBasePosition).normalized;

        if (previousDirection.sqrMagnitude < 0.0001f)
            previousDirection = currentDirection;

        // 前フレーム・現在フレームの両方に
        // 同じ旋空延長距離を適用する。
        //
        // これにより延長そのものが移動速度として計測されない。
        Vector3 previousEffectiveTip =
            previousTipPosition + previousDirection * senkuExtensionLength;

        Vector3 currentEffectiveTip =
            currentTipPosition + currentDirection * senkuExtensionLength;

        float effectiveBladeLength = Mathf.Max(
            Vector3.Distance(previousBasePosition, previousEffectiveTip),
            Vector3.Distance(currentBasePosition, currentEffectiveTip));

        EnsureSampleCapacity(effectiveBladeLength);

        // 前フレームと現在フレームの攻撃判定用サンプルを構築
        BuildBladeSamples(
            previousBasePosition,
            previousEffectiveTip,
            previousSamplePoints);

        BuildBladeSamples(
            currentBasePosition,
            currentEffectiveTip,
            currentSamplePoints);

        // 通常攻撃の下限速度でダメージ判定する。
        // 旋空の発動条件 1.5m/s とは独立している。
        float requiredDamageSpeed = Mathf.Max(0.05f, minimumSwingSpeed);

        if (swingSpeed >= requiredDamageSpeed)
        {
            if (!attackWindowOpen)
            {
                hitReceiverIds.Clear();
                attackWindowOpen = true;
            }

            idleTime = 0f;

            SweepBlade(swingSpeed, swingDirection);
        }
        else
        {
            idleTime += deltaTime;

            if (idleTime >= swingResetDelay)
            {
                attackWindowOpen = false;
                hitReceiverIds.Clear();
            }
        }

        // 次フレーム用に、実際の刀身位置だけを保存する。
        previousBasePosition = currentBasePosition;
        previousTipPosition = currentTipPosition;
    }

    private void SweepBlade(float swingSpeed, Vector3 swingDirection)
    {
        // SphereCastだけでは開始点がCollider内にあるケースを検出しない。
        // そこで前フレームと現在フレームの刀身体積もOverlapCapsuleで検査し、
        // スイング開始時の接触や、相手側の移動による接触を拾う。
        CheckBladeVolume(
            previousSamplePoints[0],
            previousSamplePoints[currentSampleCount - 1],
            swingDirection,
            swingSpeed);
        CheckBladeVolume(
            currentSamplePoints[0],
            currentSamplePoints[currentSampleCount - 1],
            swingDirection,
            swingSpeed);

        // Blade length is sampled at a fixed maximum spacing. Each sample is
        // sphere-cast from the previous frame to the current frame to cover
        // fast translations and rotations between rendered poses.
        for (int i = 0; i < currentSampleCount; i++)
        {
            Vector3 from = previousSamplePoints[i];
            Vector3 to = currentSamplePoints[i];
            Vector3 delta = to - from;
            float distance = delta.magnitude;

            if (distance <= 0.0001f)
                continue;

            int hitCount = Physics.SphereCastNonAlloc(
                from,
                sweepRadius,
                delta / distance,
                castBuffer,
                distance,
                damageLayers,
                QueryTriggerInteraction.Collide);

            for (int hitIndex = 0; hitIndex < hitCount; hitIndex++)
            {
                RaycastHit hit = castBuffer[hitIndex];
                if (hit.collider != null)
                    TryApplyDamage(hit.collider, hit.point, swingDirection, swingSpeed);
            }
        }
    }

    private void CheckBladeVolume(
        Vector3 basePosition,
        Vector3 tipPosition,
        Vector3 swingDirection,
        float swingSpeed)
    {
        Vector3 midpoint = (basePosition + tipPosition) * 0.5f;
        int overlapCount = Physics.OverlapCapsuleNonAlloc(
            basePosition,
            tipPosition,
            sweepRadius,
            overlapBuffer,
            damageLayers,
            QueryTriggerInteraction.Collide);

        for (int i = 0; i < overlapCount; i++)
        {
            Collider collider = overlapBuffer[i];
            if (collider != null)
            {
                Vector3 hitPoint = collider.ClosestPoint(midpoint);
                TryApplyDamage(collider, hitPoint, swingDirection, swingSpeed);
            }
        }
    }

    private void TryApplyDamage(
        Collider targetCollider,
        Vector3 hitPoint,
        Vector3 hitDirection,
        float swingSpeed)
    {
        if (targetCollider == null || weapon == null || weaponManager == null)
            return;

        if (targetCollider.attachedRigidbody == weapon.Rigidbody)
            return;

        if (weaponManager.IsOwnPlayerTransform(targetCollider.transform))
            return;

        IKogetsuDamageReceiver receiver = FindDamageReceiver(targetCollider);
        if (receiver == null)
            return;

        MonoBehaviour receiverComponent = receiver as MonoBehaviour;
        if (receiverComponent == null || !receiverComponent.isActiveAndEnabled)
            return;

        int receiverId = receiverComponent.GetInstanceID();
        if (!weapon.CanDealDamageNow() || !weapon.CurrentHolderSide.HasValue ||
            !hitReceiverIds.Add(receiverId))
        {
            return;
        }

        // ヒット判定生成後にスロットが切り替わる可能性があるため、
        // ダメージ通知の直前でもう一度選択状態と保持手を検査する。
        if (!weapon.CanDealDamageNow() || !weapon.CurrentHolderSide.HasValue)
        {
            hitReceiverIds.Remove(receiverId);
            return;
        }

        TriggerSide holderSide = weapon.CurrentHolderSide.Value;

        KogetsuDamageInfo info = new KogetsuDamageInfo(
            weapon,
            weaponManager.PlayerRoot,
            weapon.SourceSide,
            holderSide,
            hitPoint,
            hitDirection.normalized,
            baseDamage * currentDamageMultiplier,
            swingSpeed,
            senkuActive);

        receiver.ReceiveKogetsuDamage(info);

        if (logDamageHits)
        {
            Debug.Log(
                $"Kogetsu hit: damage={info.Damage:F1}, speed={swingSpeed:F2}, senku={senkuActive}",
                receiverComponent);
        }
    }

    private IKogetsuDamageReceiver FindDamageReceiver(Collider targetCollider)
    {
        receiverBehaviourBuffer.Clear();
        targetCollider.GetComponentsInParent<MonoBehaviour>(true, receiverBehaviourBuffer);

        for (int i = 0; i < receiverBehaviourBuffer.Count; i++)
        {
            if (receiverBehaviourBuffer[i] is IKogetsuDamageReceiver receiver)
                return receiver;
        }

        return null;
    }



    private int CalculateSampleCount(float effectiveBladeLength)
    {
        maximumSampleCount = Mathf.Clamp(maximumSampleCount, 2, 128);
        maximumSampleSpacing = Mathf.Max(0.01f, maximumSampleSpacing);

        float effectiveSampleSpacing =
            Mathf.Min(maximumSampleSpacing, sweepRadius * 2f);

        int requiredCount =
            Mathf.CeilToInt(effectiveBladeLength / effectiveSampleSpacing) + 1;

        return Mathf.Clamp(requiredCount, 2, maximumSampleCount);
    }

    private void EnsureSampleCapacity(float effectiveBladeLength)
    {
        int targetCount = CalculateSampleCount(effectiveBladeLength);

        if (previousSamplePoints == null ||
            previousSamplePoints.Length != targetCount)
        {
            previousSamplePoints = new Vector3[targetCount];
            currentSamplePoints = new Vector3[targetCount];
        }

        currentSampleCount = targetCount;

        // 配列を再確保しても hasHistory は false にしない。
        // 前フレームの実位置から配列を再構築できるため。
    }

    private void BuildBladeSamples(
        Vector3 basePosition,
        Vector3 tipPosition,
        Vector3[] targetPoints)
    {
        for (int i = 0; i < currentSampleCount; i++)
        {
            float t = i / (float)(currentSampleCount - 1);

            targetPoints[i] = Vector3.Lerp(
                basePosition,
                tipPosition,
                t);
        }
    }

    private float CalculatePhysicalSwingSpeed(
    Vector3 previousBase,
    Vector3 previousTip,
    Vector3 currentBase,
    Vector3 currentTip,
    float deltaTime,
    out Vector3 swingDirection)
    {
        swingDirection = (currentTip - currentBase).normalized;

        float previousLength =
            Vector3.Distance(previousBase, previousTip);

        float currentLength =
            Vector3.Distance(currentBase, currentTip);

        float bladeLength = Mathf.Max(
            previousLength,
            currentLength);

        int sampleCount = CalculateSampleCount(bladeLength);

        float maximumSpeed = 0f;

        for (int i = 0; i < sampleCount; i++)
        {
            float t = i / (float)(sampleCount - 1);

            Vector3 previousPoint = Vector3.Lerp(
                previousBase,
                previousTip,
                t);

            Vector3 currentPoint = Vector3.Lerp(
                currentBase,
                currentTip,
                t);

            Vector3 movement = currentPoint - previousPoint;

            float distance = movement.magnitude;
            float speed = distance / deltaTime;

            if (speed > maximumSpeed)
            {
                maximumSpeed = speed;

                if (distance > 0.0001f)
                    swingDirection = movement / distance;
            }
        }

        return maximumSpeed;
    }

    private void ResetSampling()
    {
        hasHistory = false;
        attackWindowOpen = false;
        idleTime = 0f;
        hitReceiverIds.Clear();
    }

    private void OnValidate()
    {
        baseDamage = Mathf.Max(0f, baseDamage);
        minimumSwingSpeed = Mathf.Max(0f, minimumSwingSpeed);
        sweepRadius = Mathf.Max(0.001f, sweepRadius);
        maximumSampleSpacing = Mathf.Max(0.01f, maximumSampleSpacing);
        maximumSampleCount = Mathf.Clamp(maximumSampleCount, 2, 128);
        swingResetDelay = Mathf.Max(0.01f, swingResetDelay);
    }
}
