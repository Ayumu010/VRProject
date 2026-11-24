using UnityEngine;

[CreateAssetMenu(
    fileName = "TriggerDefinition",
    menuName = "Game/Triggers/Trigger Definition"
)]
public class TriggerDefinition : ScriptableObject
{
    [Header("Trigger Information")]
    [SerializeField] private string triggerId;
    [SerializeField] private string triggerName;
    [SerializeField] private Sprite icon;

    [Header("Trigger Behaviour")]
    [SerializeField] private TriggerBehaviour behaviourAsset;

    public string TriggerId => triggerId;
    public string TriggerName => triggerName;
    public Sprite Icon => icon;
    public TriggerBehaviour BehaviourAsset => behaviourAsset;
}