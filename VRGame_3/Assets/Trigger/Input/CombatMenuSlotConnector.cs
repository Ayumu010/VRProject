using UnityEngine;

public class CombatMenuSlotConnector : MonoBehaviour
{
    [SerializeField]
    private TriggerSlotManager slotManager;

    public void SelectMainSlot(int slotIndex)
    {
        SelectSlot(TriggerSide.Main, slotIndex);
    }

    public void SelectSubSlot(int slotIndex)
    {
        SelectSlot(TriggerSide.Sub, slotIndex);
    }

    private void SelectSlot(
        TriggerSide side,
        int slotIndex)
    {
        if (slotManager == null)
        {
            Debug.LogError(
                "CombatMenuSlotConnector: SlotManager is missing.",
                this
            );
            return;
        }

        if (!slotManager.SelectSlot(side, slotIndex))
        {
            Debug.LogWarning(
                $"Failed to select {side} Slot {slotIndex + 1}.",
                this
            );
            return;
        }

        Debug.Log(
            $"Combat Menu: {side} Slot {slotIndex + 1} Selected.",
            this
        );
    }
}