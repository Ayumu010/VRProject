using UnityEngine;

[CreateAssetMenu(
    fileName = "TriggerInputTest",
    menuName = "Game/Triggers/Debug/Input Test"
)]
public class TriggerInputTestBehaviour : TriggerBehaviour
{
    protected override void OnRegistered()
    {
        Debug.Log(
            $"[TriggerTest] Registered: {Side} Slot {SlotIndex + 1}",
            this
        );
    }

    protected override void OnSelected()
    {
        Debug.Log(
            $"[TriggerTest] Selected: {Side} Slot {SlotIndex + 1}",
            this
        );
    }

    protected override void OnDeselected()
    {
        Debug.Log(
            $"[TriggerTest] Deselected: {Side} Slot {SlotIndex + 1}",
            this
        );
    }

    protected override void OnInput(
        TriggerControl control,
        bool pressed)
    {
        Debug.Log(
            $"[TriggerTest] Side={Side}, " +
            $"Slot={SlotIndex + 1}, " +
            $"Control={control}, " +
            $"Pressed={pressed}",
            this
        );
    }

    protected override void OnUnregistered()
    {
        Debug.Log(
            $"[TriggerTest] Unregistered: {Side} Slot {SlotIndex + 1}",
            this
        );
    }
}