using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

public enum TestDummyTriggerActivation
{
    OnPressed,
    WhileHeld,
    OnReleased
}

[Serializable]
public sealed class TestDummyTriggerSlot
{
    [SerializeField] private string slotName = "Projectile";
    [SerializeField] private TriggerSide side = TriggerSide.Main;
    [SerializeField] private TriggerControl control = TriggerControl.Trigger;
    [SerializeField] private TestDummyTriggerActivation activation =
        TestDummyTriggerActivation.OnPressed;
    [SerializeField, Min(0.05f)] private float repeatInterval = 0.5f;
    [SerializeField] private TestDummyTriggerAction action;
    [SerializeField] private UnityEvent onTriggered = new UnityEvent();

    public string SlotName => slotName;
    public TriggerSide Side => side;
    public TriggerControl Control => control;
    public TestDummyTriggerActivation Activation => activation;
    public float RepeatInterval => repeatInterval;
    public TestDummyTriggerAction Action => action;
    public UnityEvent OnTriggered => onTriggered;
}

[Serializable]
public sealed class TestDummyInputBinding
{
    [SerializeField] private string bindingName = "Input Binding";
    [SerializeField] private TriggerSide side = TriggerSide.Main;
    [SerializeField] private TriggerControl control = TriggerControl.Trigger;
    [SerializeField] private InputActionProperty action;

    public string BindingName => bindingName;
    public TriggerSide Side => side;
    public TriggerControl Control => control;
    public InputActionProperty Action => action;
}

/// <summary>
/// Configurable local test dummy.
/// Uses the project's TriggerSide / TriggerControl enums and existing projectile collision system.
/// </summary>
[DisallowMultipleComponent]
public sealed class TestDummyShooter : MonoBehaviour
{
    private const int AutoInputSourceId = -1;

    [Header("Target")]
    [SerializeField] private Transform target;
    [SerializeField] private bool useMainCameraWhenTargetIsMissing = true;

    [Header("Projectile")]
    [SerializeField] private GameObject defaultProjectilePrefab;
    [SerializeField] private Transform muzzle;

    [Header("Trigger Slots")]
    [SerializeField] private TestDummyTriggerSlot[] triggerSlots =
        Array.Empty<TestDummyTriggerSlot>();

    [Header("Controller Input Bindings")]
    [SerializeField] private TestDummyInputBinding[] inputBindings =
        Array.Empty<TestDummyInputBinding>();
    [SerializeField] private bool enableActionsIfNeeded = false;

    [Header("Automatic Trigger Test")]
    [SerializeField] private bool autoTriggerEnabled = true;
    [SerializeField] private TriggerSide autoTriggerSide = TriggerSide.Main;
    [SerializeField] private TriggerControl autoTriggerControl = TriggerControl.Trigger;
    [SerializeField, Min(0.1f)] private float autoTriggerInterval = 1.5f;
    [SerializeField, Min(0f)] private float firstAutoTriggerDelay = 2f;
    [SerializeField, Min(0.02f)] private float autoTriggerHoldDuration = 0.12f;

    [Header("Debug")]
    [SerializeField] private bool logTriggerEvents = true;
    [SerializeField] private bool createFallbackVisual = true;

    private sealed class TriggerRuntimeState
    {
        public readonly HashSet<int> PressedSources = new HashSet<int>();
        public float NextRepeatTime;
    }

    private sealed class ActionBinding
    {
        public InputAction Action;
        public Action<InputAction.CallbackContext> Performed;
        public Action<InputAction.CallbackContext> Canceled;
        public bool EnabledByThisComponent;
    }

    private readonly List<TriggerRuntimeState> runtimeStates =
        new List<TriggerRuntimeState>();
    private readonly List<ActionBinding> boundActions =
        new List<ActionBinding>();

    private TestDummyContext actionContext;
    private ulong ownerId;
    private float nextAutoTriggerTime;
    private float autoReleaseTime;
    private bool autoInputHeld;
    private bool warnedAboutMissingTarget;
    private bool warnedAboutMissingProjectile;

    public ulong OwnerId => ownerId;
    public Transform CurrentTarget => target;
    public Transform Muzzle => muzzle;
    public GameObject DefaultProjectilePrefab => defaultProjectilePrefab;

    private void Awake()
    {
        // Match the runtime-local owner ID convention already used by Asteroid.
        ownerId = unchecked((ulong)(uint)GetInstanceID());
        actionContext = new TestDummyContext(this);

        if (createFallbackVisual)
        {
            CreateFallbackVisualIfNeeded();
        }
    }

    private void OnEnable()
    {
        RebuildRuntimeStates();
        BindInputActions();

        autoInputHeld = false;
        nextAutoTriggerTime = Time.time + Mathf.Max(0f, firstAutoTriggerDelay);
    }

    private void OnDisable()
    {
        UnbindInputActions();
        ClearRuntimeInputStates();
    }

    private void Update()
    {
        ResolveTarget();
        UpdateAutomaticTrigger();
        TickHeldTriggers();
    }

    private void RebuildRuntimeStates()
    {
        runtimeStates.Clear();

        if (triggerSlots == null)
        {
            triggerSlots = Array.Empty<TestDummyTriggerSlot>();
            return;
        }

        for (int i = 0; i < triggerSlots.Length; i++)
        {
            runtimeStates.Add(new TriggerRuntimeState());
        }
    }

    private void BindInputActions()
    {
        UnbindInputActions();

        if (inputBindings == null)
        {
            return;
        }

        for (int i = 0; i < inputBindings.Length; i++)
        {
            TestDummyInputBinding binding = inputBindings[i];
            if (binding == null)
            {
                continue;
            }

            InputAction action = binding.Action.action;
            if (action == null)
            {
                continue;
            }

            int sourceId = i + 1;
            TriggerSide side = binding.Side;
            TriggerControl control = binding.Control;

            Action<InputAction.CallbackContext> performed = _ =>
                SetInput(side, control, true, sourceId);
            Action<InputAction.CallbackContext> canceled = _ =>
                SetInput(side, control, false, sourceId);

            action.performed += performed;
            action.canceled += canceled;

            bool enabledByThisComponent = false;
            if (enableActionsIfNeeded && !action.enabled)
            {
                action.Enable();
                enabledByThisComponent = true;
            }

            boundActions.Add(new ActionBinding
            {
                Action = action,
                Performed = performed,
                Canceled = canceled,
                EnabledByThisComponent = enabledByThisComponent
            });
        }
    }

    private void UnbindInputActions()
    {
        foreach (ActionBinding binding in boundActions)
        {
            if (binding.Action == null)
            {
                continue;
            }

            binding.Action.performed -= binding.Performed;
            binding.Action.canceled -= binding.Canceled;

            if (binding.EnabledByThisComponent && binding.Action.enabled)
            {
                binding.Action.Disable();
            }
        }

        boundActions.Clear();
    }

    private void SetInput(
        TriggerSide side,
        TriggerControl control,
        bool pressed,
        int sourceId)
    {
        if (triggerSlots == null)
        {
            return;
        }

        for (int i = 0; i < triggerSlots.Length; i++)
        {
            TestDummyTriggerSlot slot = triggerSlots[i];
            if (slot == null || slot.Side != side || slot.Control != control)
            {
                continue;
            }

            TriggerRuntimeState state = GetRuntimeState(i);
            if (state == null)
            {
                continue;
            }

            if (pressed)
            {
                bool wasHeld = state.PressedSources.Count > 0;
                if (!state.PressedSources.Add(sourceId) || wasHeld)
                {
                    continue;
                }

                HandlePressed(slot, state);
            }
            else
            {
                if (!state.PressedSources.Remove(sourceId))
                {
                    continue;
                }

                if (state.PressedSources.Count == 0)
                {
                    HandleReleased(slot, state);
                }
            }
        }
    }

    private TriggerRuntimeState GetRuntimeState(int index)
    {
        if (index < 0 || index >= runtimeStates.Count)
        {
            return null;
        }

        return runtimeStates[index];
    }

    private void HandlePressed(
        TestDummyTriggerSlot slot,
        TriggerRuntimeState state)
    {
        switch (slot.Activation)
        {
            case TestDummyTriggerActivation.OnPressed:
                ExecuteSlot(slot);
                break;

            case TestDummyTriggerActivation.WhileHeld:
                ExecuteSlot(slot);
                state.NextRepeatTime = Time.time + Mathf.Max(0.05f, slot.RepeatInterval);
                break;
        }
    }

    private void HandleReleased(
        TestDummyTriggerSlot slot,
        TriggerRuntimeState state)
    {
        if (slot.Activation == TestDummyTriggerActivation.OnReleased)
        {
            ExecuteSlot(slot);
        }

        state.NextRepeatTime = 0f;
    }

    private void TickHeldTriggers()
    {
        if (triggerSlots == null)
        {
            return;
        }

        for (int i = 0; i < triggerSlots.Length; i++)
        {
            TestDummyTriggerSlot slot = triggerSlots[i];
            TriggerRuntimeState state = GetRuntimeState(i);

            if (slot == null || state == null ||
                slot.Activation != TestDummyTriggerActivation.WhileHeld ||
                state.PressedSources.Count == 0 ||
                Time.time < state.NextRepeatTime)
            {
                continue;
            }

            ExecuteSlot(slot);
            state.NextRepeatTime = Time.time + Mathf.Max(0.05f, slot.RepeatInterval);
        }
    }

    private void ExecuteSlot(TestDummyTriggerSlot slot)
    {
        if (slot.Action != null)
        {
            slot.Action.Execute(actionContext);
        }

        slot.OnTriggered?.Invoke();

        if (logTriggerEvents)
        {
            Debug.Log(
                $"[TestDummy] Triggered: {slot.SlotName} ({slot.Side}/{slot.Control}/{slot.Activation})",
                this);
        }
    }

    private void UpdateAutomaticTrigger()
    {
        if (!autoTriggerEnabled)
        {
            if (autoInputHeld)
            {
                SetInput(autoTriggerSide, autoTriggerControl, false, AutoInputSourceId);
                autoInputHeld = false;
            }

            return;
        }

        if (autoInputHeld && Time.time >= autoReleaseTime)
        {
            SetInput(autoTriggerSide, autoTriggerControl, false, AutoInputSourceId);
            autoInputHeld = false;
        }

        if (!autoInputHeld && Time.time >= nextAutoTriggerTime)
        {
            SetInput(autoTriggerSide, autoTriggerControl, true, AutoInputSourceId);
            autoInputHeld = true;
            autoReleaseTime = Time.time + Mathf.Max(0.02f, autoTriggerHoldDuration);
            nextAutoTriggerTime = Time.time + Mathf.Max(0.1f, autoTriggerInterval);
        }
    }

    private void ResolveTarget()
    {
        if (target != null || !useMainCameraWhenTargetIsMissing)
        {
            return;
        }

        Camera mainCamera = Camera.main;
        if (mainCamera != null)
        {
            target = mainCamera.transform;
            warnedAboutMissingTarget = false;
        }
    }

    public bool TryGetAimDirection(out Vector3 direction)
    {
        Vector3 origin = muzzle != null
            ? muzzle.position
            : transform.position + Vector3.up * 1.2f + transform.forward * 0.75f;

        if (target == null)
        {
            direction = transform.forward;
            return false;
        }

        Vector3 offset = target.position - origin;
        if (offset.sqrMagnitude < 0.000001f)
        {
            direction = transform.forward;
            return false;
        }

        direction = offset.normalized;
        return true;
    }

    public bool SpawnProjectile(GameObject prefab, Vector3 direction)
    {
        if (prefab == null)
        {
            if (!warnedAboutMissingProjectile)
            {
                Debug.LogError("TestDummyShooter: 弾Prefabが設定されていません。", this);
                warnedAboutMissingProjectile = true;
            }

            return false;
        }

        if (direction.sqrMagnitude < 0.000001f)
        {
            Debug.LogWarning("TestDummyShooter: 発射方向がゼロです。", this);
            return false;
        }

        Vector3 spawnPosition = muzzle != null
            ? muzzle.position
            : transform.position + Vector3.up * 1.2f + transform.forward * 0.75f;
        direction.Normalize();

        GameObject projectileObject = Instantiate(
            prefab,
            spawnPosition,
            Quaternion.LookRotation(direction, GetSafeUp(direction)));

        ProjectileCollisionSystem collisionSystem =
            projectileObject.GetComponent<ProjectileCollisionSystem>();
        ProjectileController projectileController =
            projectileObject.GetComponent<ProjectileController>();

        if (collisionSystem == null || projectileController == null)
        {
            Debug.LogError(
                "TestDummyShooter: 弾PrefabにProjectileCollisionSystemまたはProjectileControllerがありません。",
                projectileObject);
            Destroy(projectileObject);
            return false;
        }

        // The shared projectile system requires Initialize before Launch.
        collisionSystem.Initialize(ownerId, transform);
        projectileController.Launch(direction);
        return true;
    }

    private static Vector3 GetSafeUp(Vector3 direction)
    {
        return Mathf.Abs(Vector3.Dot(direction.normalized, Vector3.up)) > 0.98f
            ? Vector3.forward
            : Vector3.up;
    }

    private void CreateFallbackVisualIfNeeded()
    {
        if (GetComponentInChildren<Renderer>(true) != null)
        {
            return;
        }

        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.name = "DummyVisual";
        body.transform.SetParent(transform, false);
        body.transform.localPosition = new Vector3(0f, 1f, 0f);
        body.transform.localRotation = Quaternion.identity;
        body.transform.localScale = new Vector3(0.7f, 0.9f, 0.7f);

        Collider visualCollider = body.GetComponent<Collider>();
        if (visualCollider != null)
        {
            Destroy(visualCollider);
        }

        Renderer bodyRenderer = body.GetComponent<Renderer>();
        if (bodyRenderer != null)
        {
            bodyRenderer.material.color = new Color(0.85f, 0.18f, 0.12f, 1f);
        }
    }

    private void ClearRuntimeInputStates()
    {
        foreach (TriggerRuntimeState state in runtimeStates)
        {
            state.PressedSources.Clear();
            state.NextRepeatTime = 0f;
        }

        autoInputHeld = false;
    }

    private void OnValidate()
    {
        autoTriggerInterval = Mathf.Max(0.1f, autoTriggerInterval);
        firstAutoTriggerDelay = Mathf.Max(0f, firstAutoTriggerDelay);
        autoTriggerHoldDuration = Mathf.Max(0.02f, autoTriggerHoldDuration);

        if (triggerSlots == null)
        {
            triggerSlots = Array.Empty<TestDummyTriggerSlot>();
        }

        if (inputBindings == null)
        {
            inputBindings = Array.Empty<TestDummyInputBinding>();
        }
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 origin = muzzle != null
            ? muzzle.position
            : transform.position + Vector3.up * 1.2f + transform.forward * 0.75f;

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(origin, 0.12f);

        Transform debugTarget = target;
        if (debugTarget == null && useMainCameraWhenTargetIsMissing && Camera.main != null)
        {
            debugTarget = Camera.main.transform;
        }

        if (debugTarget != null)
        {
            Gizmos.DrawLine(origin, debugTarget.position);
        }
    }
}
