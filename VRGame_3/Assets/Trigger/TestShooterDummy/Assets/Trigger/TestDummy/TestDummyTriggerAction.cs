using UnityEngine;

/// <summary>
/// Test dummy trigger execution unit.
/// Create derived ScriptableObjects to add new test actions without modifying TestDummyShooter.
/// </summary>
public abstract class TestDummyTriggerAction : ScriptableObject
{
    public abstract void Execute(TestDummyContext context);
}
