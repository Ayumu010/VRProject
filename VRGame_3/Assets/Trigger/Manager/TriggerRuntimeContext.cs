
using UnityEngine;

public class TriggerRuntimeContext
{
    public TriggerSlotManager Manager { get; }
    public TriggerDefinition Definition { get; }
    public TriggerSide Side { get; }
    public int SlotIndex { get; }

    public Transform HandAttachPoint { get; }
    public Transform PlayerRoot { get; }

    public Transform MenuAnchor { get; }

    public TriggerRuntimeContext(
        TriggerSlotManager manager,
        TriggerDefinition definition,
        TriggerSide side,
        int slotIndex,
        Transform handAttachPoint,
        Transform playerRoot,
        Transform menuAnchor)
    {
        Manager = manager;
        Definition = definition;
        Side = side;
        SlotIndex = slotIndex;

        HandAttachPoint = handAttachPoint;
        PlayerRoot = playerRoot;

        MenuAnchor = menuAnchor;
    }
}
