
using System;
using System.Collections.Generic;
using UnityEngine;

namespace AdaptiveHands
{
    /// <summary>
    /// Determines how a pose affects the fingers.
    /// </summary>
    public enum HandPoseApplyMode
    {
        FullOverride,
        PartialOverride
    }

    /// <summary>
    /// Determines how multiple conditions are evaluated.
    /// </summary>
    public enum HandPoseConditionMode
    {
        All,
        Any
    }

    /// <summary>
    /// Available controller inputs for pose conditions.
    /// </summary>
    public enum HandPoseInput
    {
        // Analog inputs
        Trigger,
        Grip,

        // Index finger inputs
        TriggerProximity,
        TriggerCurl,
        TriggerSlide,
        TriggerForce,

        // Thumb inputs
        ThumbProximity,

        // Button / touch inputs
        TriggerTouch,
        ThumbrestTouched,
        ThumbstickTouched,
        PrimaryTouched,
        SecondaryTouched
    }

    /// <summary>
    /// Comparison operators for pose conditions.
    /// </summary>
    public enum HandPoseComparison
    {
        GreaterThan,
        GreaterThanOrEqual,
        LessThan,
        LessThanOrEqual,

        IsPressed,
        IsNotPressed
    }

    /// <summary>
    /// One input condition required to activate a pose.
    /// </summary>
    [Serializable]
    public class HandPoseCondition
    {
        [SerializeField]
        private HandPoseInput input;

        [SerializeField]
        private HandPoseComparison comparison =
            HandPoseComparison.GreaterThanOrEqual;

        [SerializeField, Range(0f, 1f)]
        private float threshold = 0.5f;

        public HandPoseInput Input => input;

        public HandPoseComparison Comparison => comparison;

        public float Threshold => threshold;
    }

    /// <summary>
    /// Animation value and override setting for one finger.
    /// </summary>
    [Serializable]
    public class HandPoseFingerSetting
    {
        [SerializeField]
        private bool overrideFinger;

        [SerializeField, Range(0f, 1f)]
        private float amount;

        public bool OverrideFinger => overrideFinger;

        public float Amount => Mathf.Clamp01(amount);
    }

    /// <summary>
    /// One complete hand pose definition.
    /// </summary>
    [Serializable]
    public class HandPoseRule
    {
        [Header("Pose Information")]

        [SerializeField]
        private string poseName = "New Pose";

        [SerializeField]
        private int priority;

        [Header("Activation Conditions")]

        [SerializeField]
        private HandPoseConditionMode conditionMode =
            HandPoseConditionMode.All;

        [SerializeField]
        private List<HandPoseCondition> conditions =
            new List<HandPoseCondition>();

        [Header("Pose Application")]

        [SerializeField]
        private HandPoseApplyMode applyMode =
            HandPoseApplyMode.FullOverride;

        [Header("Finger Settings")]

        [SerializeField]
        private HandPoseFingerSetting thumb =
            new HandPoseFingerSetting();

        [SerializeField]
        private HandPoseFingerSetting index =
            new HandPoseFingerSetting();

        [SerializeField]
        private HandPoseFingerSetting middle =
            new HandPoseFingerSetting();

        [SerializeField]
        private HandPoseFingerSetting ring =
            new HandPoseFingerSetting();

        [SerializeField]
        private HandPoseFingerSetting pinky =
            new HandPoseFingerSetting();

        [Header("Transition")]

        [SerializeField, Min(0f)]
        private float transitionSpeed = 8f;

        public string PoseName => poseName;

        public int Priority => priority;

        public HandPoseConditionMode ConditionMode => conditionMode;

        public IReadOnlyList<HandPoseCondition> Conditions => conditions;

        public HandPoseApplyMode ApplyMode => applyMode;

        public HandPoseFingerSetting Thumb => thumb;

        public HandPoseFingerSetting Index => index;

        public HandPoseFingerSetting Middle => middle;

        public HandPoseFingerSetting Ring => ring;

        public HandPoseFingerSetting Pinky => pinky;

        public float TransitionSpeed => transitionSpeed;
    }

    /// <summary>
    /// Collection of hand poses.
    /// Create different assets for different gameplay situations.
    /// </summary>
    [CreateAssetMenu(
        fileName = "HandPoseProfile",
        menuName = "Adaptive Hands/Hand Pose Profile"
    )]
    public class HandPoseProfile : ScriptableObject
    {
        [Header("Default Transition")]

        [SerializeField, Min(0f)]
        private float defaultTransitionSpeed = 8f;

        [Header("Registered Poses")]

        [SerializeField]
        private List<HandPoseRule> poses =
            new List<HandPoseRule>();

        public float DefaultTransitionSpeed =>
            defaultTransitionSpeed;

        public IReadOnlyList<HandPoseRule> Poses => poses;
    }
}
