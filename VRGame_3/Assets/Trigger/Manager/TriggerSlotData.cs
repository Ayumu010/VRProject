using System;
using UnityEngine;

[Serializable]
public class TriggerSlotData
{
    [SerializeField] private string slotName;
    [SerializeField] private TriggerDefinition triggerDefinition;

    [NonSerialized]
    private TriggerBehaviour runtimeInstance;

    public string SlotName => slotName;
    public TriggerDefinition TriggerDefinition => triggerDefinition;
    public TriggerBehaviour RuntimeInstance => runtimeInstance;

    public void SetTrigger(TriggerDefinition definition)
    {
        triggerDefinition = definition;
    }

    public void SetRuntimeInstance(TriggerBehaviour instance)
    {
        runtimeInstance = instance;
    }
}