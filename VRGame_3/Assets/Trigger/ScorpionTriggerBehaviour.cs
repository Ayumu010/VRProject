using System;
using UnityEngine;

[CreateAssetMenu(
    fileName = "ScorpionBehaviour",
    menuName = "Game/Triggers/Behaviours/Scorpion"
)]
public class ScorpionTriggerBehaviour : TriggerBehaviour
{
    [Header("Scorpion Settings")]
    [SerializeField] private GameObject scorpionPrefab;

    // 実行インスタンスごとに保持する状態
    [NonSerialized]
    private GameObject scorpionInstance;

    [NonSerialized]
    private float chargeTime;

    // --------------------------------------------------
    // 選択されたとき
    // --------------------------------------------------

    protected override void OnSelected()
    {
        Debug.Log("Scorpion Selected");

        // 手にスコーピオンを生成する
        if (scorpionInstance == null)
        {
            scorpionInstance = SpawnOnHand(scorpionPrefab);
        }
    }

    // --------------------------------------------------
    // 選択解除されたとき
    // --------------------------------------------------

    protected override void OnDeselected()
    {
        Debug.Log("Scorpion Deselected");

        // 見た目だけ解除する
        DestroySpawned(scorpionInstance);
        scorpionInstance = null;
    }

    // --------------------------------------------------
    // コントローラー操作
    // --------------------------------------------------

    protected override void OnInput(
        TriggerControl control,
        bool pressed)
    {
        if (control == TriggerControl.Trigger)
        {
            if (pressed)
            {
                StartAttack();
            }
            else
            {
                EndAttack();
            }
        }

        if (control == TriggerControl.Grip)
        {
            if (pressed)
            {
                StartCharge();
            }
            else
            {
                EndCharge();
            }
        }

        if (control == TriggerControl.Primary && pressed)
        {
            Debug.Log("Scorpion Primary Action");
        }
    }

    // --------------------------------------------------
    // 攻撃
    // --------------------------------------------------

    private void StartAttack()
    {
        Debug.Log("Scorpion Attack Start");

        // 攻撃開始処理
        // 当たり判定の有効化など
    }

    private void EndAttack()
    {
        Debug.Log("Scorpion Attack End");

        // 攻撃終了処理
        // 当たり判定の無効化など
    }

    // --------------------------------------------------
    // チャージ
    // --------------------------------------------------

    private void StartCharge()
    {
        Debug.Log("Scorpion Charge Start");

        chargeTime = 0f;
    }

    private void EndCharge()
    {
        Debug.Log($"Scorpion Charge Time: {chargeTime}");
    }

    // --------------------------------------------------
    // 毎フレーム処理
    // --------------------------------------------------

    protected override void OnTick(float deltaTime)
    {
        if (!IsSelected)
            return;

        // チャージ中の処理などを追加する
    }

    // --------------------------------------------------
    // 登録解除
    // --------------------------------------------------

    protected override void OnUnregistered()
    {
        DestroySpawned(scorpionInstance);
        scorpionInstance = null;
    }
}