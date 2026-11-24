
using System;
using UnityEngine;

public class TriggerSlotManager : MonoBehaviour
{
    private const int SlotCount = 4;

    [Header("Player References")]
    [SerializeField] private Transform playerRoot;

    [Header("Hand Attach Points")]
    [SerializeField] private Transform mainHandAttachPoint;
    [SerializeField] private Transform subHandAttachPoint;

    [Header("Menu Reference")]
    [SerializeField] private Transform menuAnchor;

    [Header("MAIN Slots")]
    [SerializeField]
    private TriggerSlotData[] mainSlots =
        new TriggerSlotData[SlotCount];

    [Header("SUB Slots")]
    [SerializeField]
    private TriggerSlotData[] subSlots =
        new TriggerSlotData[SlotCount];

    [Header("Initial Selection")]
    [SerializeField, Range(-1, 3)]
    private int initialMainSlot = -1;

    [SerializeField, Range(-1, 3)]
    private int initialSubSlot = -1;

    private int selectedMainSlot = -1;
    private int selectedSubSlot = -1;

    public int SelectedMainSlot => selectedMainSlot;
    public int SelectedSubSlot => selectedSubSlot;

    // ==================================================
    // 初期化
    // ==================================================

    private void OnValidate()
    {
        EnsureSlots(ref mainSlots);
        EnsureSlots(ref subSlots);
    }

    private void Awake()
    {
        EnsureSlots(ref mainSlots);
        EnsureSlots(ref subSlots);

        InitializeSide(TriggerSide.Main);
        InitializeSide(TriggerSide.Sub);

        if (initialMainSlot >= 0)
            SelectSlot(TriggerSide.Main, initialMainSlot);

        if (initialSubSlot >= 0)
            SelectSlot(TriggerSide.Sub, initialSubSlot);
    }

    private void EnsureSlots(ref TriggerSlotData[] slots)
    {
        if (slots == null || slots.Length != SlotCount)
        {
            Array.Resize(ref slots, SlotCount);
        }

        for (int i = 0; i < SlotCount; i++)
        {
            if (slots[i] == null)
                slots[i] = new TriggerSlotData();
        }
    }

    private void InitializeSide(TriggerSide side)
    {
        for (int i = 0; i < SlotCount; i++)
        {
            TriggerSlotData slot = GetSlot(side, i);

            if (slot.TriggerDefinition != null)
            {
                CreateRuntimeInstance(
                    side,
                    i,
                    slot
                );
            }
        }
    }

    // ==================================================
    // トリガー登録
    // ==================================================

    public bool AssignTrigger(
        TriggerSide side,
        int slotIndex,
        TriggerDefinition definition)
    {
        TriggerSlotData slot = GetSlot(side, slotIndex);

        if (slot == null)
            return false;

        if (definition == null ||
            definition.BehaviourAsset == null)
        {
            Debug.LogWarning(
                "TriggerDefinition or BehaviourAsset is missing.",
                this
            );

            return false;
        }

        if (slot.TriggerDefinition == definition &&
            slot.RuntimeInstance != null)
        {
            return true;
        }

        bool isTargetSelected =
            GetSelectedSlotIndex(side) == slotIndex;

        DestroyRuntimeInstance(slot);

        slot.SetTrigger(definition);

        if (!CreateRuntimeInstance(side, slotIndex, slot))
        {
            slot.SetTrigger(null);
            return false;
        }

        if (isTargetSelected)
        {
            slot.RuntimeInstance.SetSelected(true);
        }

        return true;
    }

    // ==================================================
    // スロット選択
    // ==================================================

    public bool SelectSlot(
        TriggerSide side,
        int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= SlotCount)
            return false;

        int currentIndex = GetSelectedSlotIndex(side);

        if (currentIndex == slotIndex)
            return true;

        TriggerSlotData currentSlot =
            GetSlot(side, currentIndex);

        if (currentSlot != null &&
            currentSlot.RuntimeInstance != null)
        {
            currentSlot.RuntimeInstance.SetSelected(false);
        }

        SetSelectedSlotIndex(side, slotIndex);

        TriggerSlotData newSlot =
            GetSlot(side, slotIndex);

        if (newSlot != null &&
            newSlot.RuntimeInstance != null)
        {
            newSlot.RuntimeInstance.SetSelected(true);
        }

        return true;
    }

    // ==================================================
    // スロット解除
    // ==================================================

    public bool ClearSlot(
        TriggerSide side,
        int slotIndex)
    {
        TriggerSlotData slot = GetSlot(side, slotIndex);

        if (slot == null)
            return false;

        if (GetSelectedSlotIndex(side) == slotIndex)
        {
            SetSelectedSlotIndex(side, -1);
        }

        DestroyRuntimeInstance(slot);

        slot.SetTrigger(null);

        return true;
    }

    // ==================================================
    // 実行インスタンス生成
    // ==================================================

    private bool CreateRuntimeInstance(
        TriggerSide side,
        int slotIndex,
        TriggerSlotData slot)
    {
        TriggerDefinition definition = slot.TriggerDefinition;

        if (definition == null ||
            definition.BehaviourAsset == null)
        {
            return false;
        }

        TriggerBehaviour runtime =
            Instantiate(definition.BehaviourAsset);

        runtime.name =
            $"{definition.TriggerName}_{side}_Slot{slotIndex + 1}_Runtime";

        Transform attachPoint =
            side == TriggerSide.Main
                ? mainHandAttachPoint
                : subHandAttachPoint;

        TriggerRuntimeContext context =
            new TriggerRuntimeContext(
                this,
                definition,
                side,
                slotIndex,
                attachPoint,
                playerRoot,
                menuAnchor
            );

        try
        {
            runtime.Initialize(context);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);

            Destroy(runtime);

            return false;
        }

        slot.SetRuntimeInstance(runtime);

        return true;
    }

    // ==================================================
    // 実行インスタンス破棄
    // ==================================================

    private void DestroyRuntimeInstance(TriggerSlotData slot)
    {
        TriggerBehaviour runtime = slot.RuntimeInstance;

        if (runtime == null)
            return;

        runtime.Shutdown();

        slot.SetRuntimeInstance(null);

        Destroy(runtime);
    }

    // ==================================================
    // コントローラー入力受信
    // ==================================================

    public void SendInput(
        TriggerSide side,
        TriggerControl control,
        bool pressed)
    {
        TriggerSlotData slot =
            GetSlot(side, GetSelectedSlotIndex(side));

        if (slot == null || slot.RuntimeInstance == null)
            return;

        slot.RuntimeInstance.ReceiveInput(
            control,
            pressed
        );
    }

    /// <summary>
    /// 入力元が無効になった場合の強制解除
    /// </summary>
    public void ReleaseInputs(TriggerSide side)
    {
        TriggerSlotData slot =
            GetSlot(side, GetSelectedSlotIndex(side));

        if (slot != null && slot.RuntimeInstance != null)
        {
            slot.RuntimeInstance.ReleaseAllInputs();
        }
    }

    // ==================================================
    // 情報取得
    // ==================================================

    public TriggerDefinition GetTrigger(
        TriggerSide side,
        int slotIndex)
    {
        TriggerSlotData slot = GetSlot(side, slotIndex);

        return slot != null
            ? slot.TriggerDefinition
            : null;
    }

    public TriggerBehaviour GetRuntimeTrigger(
        TriggerSide side,
        int slotIndex)
    {
        TriggerSlotData slot = GetSlot(side, slotIndex);

        return slot != null
            ? slot.RuntimeInstance
            : null;
    }

    public TriggerBehaviour GetSelectedTrigger(TriggerSide side)
    {
        return GetRuntimeTrigger(
            side,
            GetSelectedSlotIndex(side)
        );
    }

    public int GetSelectedSlotIndex(TriggerSide side)
    {
        return side == TriggerSide.Main
            ? selectedMainSlot
            : selectedSubSlot;
    }

    // ==================================================
    // 内部処理
    // ==================================================

    private TriggerSlotData GetSlot(
        TriggerSide side,
        int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= SlotCount)
            return null;

        switch (side)
        {
            case TriggerSide.Main:
                return mainSlots[slotIndex];

            case TriggerSide.Sub:
                return subSlots[slotIndex];

            default:
                return null;
        }
    }

    private Transform GetAttachPoint(TriggerSide side)
    {
        return side == TriggerSide.Main
            ? mainHandAttachPoint
            : subHandAttachPoint;
    }

    private void SetSelectedSlotIndex(
        TriggerSide side,
        int index)
    {
        if (side == TriggerSide.Main)
            selectedMainSlot = index;
        else
            selectedSubSlot = index;
    }

    // ==================================================
    // 毎フレーム処理
    // ==================================================

    private void Update()
    {
        TickSlots(mainSlots);
        TickSlots(subSlots);
    }

    private void TickSlots(TriggerSlotData[] slots)
    {
        foreach (TriggerSlotData slot in slots)
        {
            if (slot == null || slot.RuntimeInstance == null)
                continue;

            slot.RuntimeInstance.Tick(Time.deltaTime);
        }
    }

    // ==================================================
    // 終了処理
    // ==================================================

    private void OnDestroy()
    {
        ShutdownSlots(mainSlots);
        ShutdownSlots(subSlots);
    }

    private void ShutdownSlots(TriggerSlotData[] slots)
    {
        if (slots == null)
            return;

        foreach (TriggerSlotData slot in slots)
        {
            if (slot != null)
            {
                DestroyRuntimeInstance(slot);
            }
        }
    }
}
