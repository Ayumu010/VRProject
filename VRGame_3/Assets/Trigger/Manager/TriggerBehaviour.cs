
using System.Collections.Generic;
using UnityEngine;

public abstract class TriggerBehaviour : ScriptableObject
{
    private TriggerRuntimeContext context;

    private HashSet<TriggerControl> pressedControls =
        new HashSet<TriggerControl>();

    private bool isRegistered;
    private bool isSelected;
    private bool isForceReleasingInput;

    public bool IsRegistered => isRegistered;
    public bool IsSelected => isSelected;

    /// <summary>
    /// 入力が強制解除されているか
    /// </summary>
    protected bool IsForceReleasingInput =>
        isForceReleasingInput;

    protected TriggerSlotManager Manager => context?.Manager;
    protected TriggerDefinition Definition => context?.Definition;
    protected TriggerSide Side => context.Side;
    protected int SlotIndex => context.SlotIndex;
    protected Transform HandAttachPoint => context?.HandAttachPoint;
    protected Transform PlayerRoot => context?.PlayerRoot;
    protected Transform MenuAnchor => context?.MenuAnchor;

    // --------------------------------------------------
    // 初期化
    // --------------------------------------------------

    internal void Initialize(TriggerRuntimeContext runtimeContext)
    {
        context = runtimeContext;

        pressedControls.Clear();

        isForceReleasingInput = false;
        isRegistered = true;
        isSelected = false;

        OnRegistered();
    }

    // --------------------------------------------------
    // 選択状態
    // --------------------------------------------------

    internal void SetSelected(bool selected)
    {
        if (!isRegistered || isSelected == selected)
            return;

        if (!selected)
        {
            // 押されている入力をすべて強制解除する
            ReleaseAllInputs();

            isSelected = false;

            OnDeselected();
        }
        else
        {
            isSelected = true;

            OnSelected();
        }
    }

    // --------------------------------------------------
    // 入力受信
    // --------------------------------------------------

    internal void ReceiveInput(
        TriggerControl control,
        bool pressed)
    {
        if (!isRegistered)
            return;

        if (pressed)
        {
            // 選択中のトリガーだけが入力を受け取る
            if (!isSelected)
                return;

            // 二重入力を防ぐ
            if (!pressedControls.Add(control))
                return;

            OnInput(control, true);
        }
        else
        {
            // 実際に押されていた入力だけ解除する
            if (!pressedControls.Remove(control))
                return;

            OnInput(control, false);
        }
    }

    // --------------------------------------------------
    // 入力の強制解除
    // --------------------------------------------------

    internal void ReleaseAllInputs()
    {
        var inputs =
            new TriggerControl[pressedControls.Count];

        pressedControls.CopyTo(inputs);

        bool previousState = isForceReleasingInput;

        isForceReleasingInput = true;

        try
        {
            foreach (TriggerControl control in inputs)
            {
                ReceiveInput(control, false);
            }
        }
        finally
        {
            isForceReleasingInput = previousState;
        }
    }

    // --------------------------------------------------
    // 毎フレーム処理
    // --------------------------------------------------

    internal void Tick(float deltaTime)
    {
        if (!isRegistered)
            return;

        OnTick(deltaTime);
    }

    // --------------------------------------------------
    // 登録解除
    // --------------------------------------------------

    internal void Shutdown()
    {
        if (!isRegistered)
            return;

        SetSelected(false);
        ReleaseAllInputs();

        OnUnregistered();

        isRegistered = false;
        context = null;

        pressedControls.Clear();
    }

    // ==================================================
    // 各トリガーで実装する処理
    // ==================================================

    protected virtual void OnRegistered() { }

    protected virtual void OnSelected() { }

    protected virtual void OnDeselected() { }

    protected virtual void OnInput(
        TriggerControl control,
        bool pressed)
    { }

    protected virtual void OnTick(float deltaTime) { }

    protected virtual void OnUnregistered() { }

    // ==================================================
    // 共通機能
    // ==================================================

    /// <summary>
    /// 手の位置にオブジェクトを生成する
    /// </summary>
    protected GameObject SpawnOnHand(GameObject prefab)
    {
        if (prefab == null || HandAttachPoint == null)
        {
            Debug.LogWarning(
                "Prefab or HandAttachPoint is missing."
            );

            return null;
        }

        return Instantiate(
            prefab,
            HandAttachPoint,
            false
        );
    }

    /// <summary>
    /// 生成したオブジェクトを削除する
    /// </summary>
    protected void DestroySpawned(GameObject instance)
    {
        if (instance != null)
        {
            Destroy(instance);
        }
    }
}
