
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

namespace AdaptiveHands
{
    /// <summary>
    /// Controls hand poses based on controller inputs
    /// and registered HandPoseProfile rules.
    /// </summary>
    [DisallowMultipleComponent]
    public class HandPoseController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private RiggedHand riggedHand;
        [SerializeField] private HandPoseProfile profile;

        [Header("Base Input Settings")]
        [SerializeField, Range(0f, 1f)]
        private float touchMinimumPose = 0.15f;

        [SerializeField, Range(0f, 1f)]
        private float thumbTouchPose = 0.25f;

        [Header("Transition")]
        [SerializeField, Min(0f)]
        private float fallbackTransitionSpeed = 8f;

        private readonly float[] currentValues = new float[5];

        private bool[] matchedRules = new bool[0];
        private bool hasInitialized;

        private void Awake()
        {
            if (riggedHand == null)
                riggedHand = GetComponent<RiggedHand>();

            if (riggedHand == null)
            {
                UnityEngine.Debug.LogError(
                    "HandPoseController: RiggedHand reference is missing.",
                    this
                );
            }
        }

        /// <summary>
        /// Processes controller input and applies the resulting hand pose.
        /// Call once per frame from AdaptiveHandInputDriver.
        /// </summary>
        public void ProcessInput(HandInputState input)
        {
            if (riggedHand == null)
                return;

            IReadOnlyList<HandPoseRule> rules =
                profile != null ? profile.Poses : null;

            int ruleCount = rules != null ? rules.Count : 0;

            // Prepare matching rule cache.
            if (matchedRules.Length != ruleCount)
                matchedRules = new bool[ruleCount];

            // Evaluate each rule only once per frame.
            for (int i = 0; i < ruleCount; i++)
            {
                matchedRules[i] =
                    rules[i] != null && EvaluateRule(rules[i], input);
            }

            // Resolve and apply all five fingers.
            for (int i = 0; i < 5; i++)
            {
                float baseValue = GetBaseFingerValue(input, i);

                ResolveFingerPose(
                    i,
                    baseValue,
                    rules,
                    ruleCount,
                    out float target,
                    out float transitionSpeed
                );

                // Initialize without an initial transition.
                if (!hasInitialized)
                {
                    currentValues[i] = target;
                }
                else if (transitionSpeed <= 0f)
                {
                    currentValues[i] = target;
                }
                else
                {
                    currentValues[i] = Mathf.MoveTowards(
                        currentValues[i],
                        target,
                        transitionSpeed * Time.deltaTime
                    );
                }

                riggedHand.PoseDigit(
                    (RiggedHand.FingerType)i,
                    currentValues[i]
                );
            }

            hasInitialized = true;
        }

        /// <summary>
        /// Calculates the normal controller-based finger values.
        /// These values are used when no pose overrides a finger.
        /// </summary>
        private float GetBaseFingerValue(
            HandInputState input,
            int fingerIndex)
        {
            switch (fingerIndex)
            {
                case 0:
                    {
                        // Thumb
                        float value = input.ThumbProximity;

                        bool touched =
                            input.ThumbrestTouched ||
                            input.ThumbstickTouched ||
                            input.PrimaryTouched ||
                            input.SecondaryTouched;

                        if (touched)
                        {
                            value = Mathf.Max(
                                value,
                                thumbTouchPose
                            );
                        }

                        return Mathf.Clamp01(value);
                    }

                case 1:
                    {
                        // Index
                        float value = Mathf.Max(
                            input.Trigger,
                            Mathf.Max(
                                input.TriggerCurl,
                                input.TriggerForce
                            )
                        );

                        if (input.TriggerTouch)
                        {
                            value = Mathf.Max(
                                value,
                                touchMinimumPose
                            );
                        }

                        if (input.TriggerProximity > 0f)
                        {
                            value = Mathf.Max(
                                value,
                                input.TriggerProximity * touchMinimumPose
                            );
                        }

                        return Mathf.Clamp01(value);
                    }

                case 2:
                case 3:
                case 4:
                    {
                        // Middle / Ring / Pinky
                        return input.Grip;
                    }

                default:
                    return 0f;
            }
        }

        /// <summary>
        /// Finds the highest-priority matching rule
        /// that affects the specified finger.
        /// </summary>
        private void ResolveFingerPose(
            int fingerIndex,
            float baseValue,
            IReadOnlyList<HandPoseRule> rules,
            int ruleCount,
            out float target,
            out float transitionSpeed)
        {
            target = Mathf.Clamp01(baseValue);

            transitionSpeed = profile != null
                ? profile.DefaultTransitionSpeed
                : fallbackTransitionSpeed;

            bool found = false;
            int bestPriority = int.MinValue;

            if (rules == null)
                return;

            for (int i = 0; i < ruleCount; i++)
            {
                if (!matchedRules[i])
                    continue;

                HandPoseRule rule = rules[i];

                if (rule == null)
                    continue;

                HandPoseFingerSetting setting =
                    GetFingerSetting(rule, fingerIndex);

                if (!CanApplyToFinger(rule, setting))
                    continue;

                // A rule with the same priority does not
                // replace an earlier registered rule.
                if (found && rule.Priority <= bestPriority)
                    continue;

                target = setting != null
                    ? setting.Amount
                    : 0f;

                transitionSpeed = rule.TransitionSpeed;

                bestPriority = rule.Priority;
                found = true;
            }
        }

        /// <summary>
        /// Checks whether a rule affects the specified finger.
        /// </summary>
        private bool CanApplyToFinger(
            HandPoseRule rule,
            HandPoseFingerSetting setting)
        {
            switch (rule.ApplyMode)
            {
                case HandPoseApplyMode.FullOverride:
                    return true;

                case HandPoseApplyMode.PartialOverride:
                    return setting != null &&
                           setting.OverrideFinger;

                default:
                    return false;
            }
        }

        /// <summary>
        /// Returns the finger setting for the specified index.
        /// 0 = Thumb, 1 = Index, 2 = Middle,
        /// 3 = Ring, 4 = Pinky.
        /// </summary>
        private HandPoseFingerSetting GetFingerSetting(
            HandPoseRule rule,
            int fingerIndex)
        {
            switch (fingerIndex)
            {
                case 0: return rule.Thumb;
                case 1: return rule.Index;
                case 2: return rule.Middle;
                case 3: return rule.Ring;
                case 4: return rule.Pinky;
                default: return null;
            }
        }

        /// <summary>
        /// Evaluates all conditions of one pose rule.
        /// Empty condition lists are treated as inactive.
        /// </summary>
        private bool EvaluateRule(
            HandPoseRule rule,
            HandInputState input)
        {
            IReadOnlyList<HandPoseCondition> conditions =
                rule.Conditions;

            if (conditions == null || conditions.Count == 0)
                return false;

            if (rule.ConditionMode == HandPoseConditionMode.All)
            {
                for (int i = 0; i < conditions.Count; i++)
                {
                    HandPoseCondition condition = conditions[i];

                    if (condition == null ||
                        !EvaluateCondition(condition, input))
                    {
                        return false;
                    }
                }

                return true;
            }

            if (rule.ConditionMode == HandPoseConditionMode.Any)
            {
                for (int i = 0; i < conditions.Count; i++)
                {
                    HandPoseCondition condition = conditions[i];

                    if (condition != null &&
                        EvaluateCondition(condition, input))
                    {
                        return true;
                    }
                }

                return false;
            }

            return false;
        }

        /// <summary>
        /// Evaluates one input condition.
        /// Button inputs use pressed comparisons.
        /// Analog inputs use numerical comparisons.
        /// </summary>
        private bool EvaluateCondition(
            HandPoseCondition condition,
            HandInputState input)
        {
            if (IsButtonInput(condition.Input))
            {
                if (!TryGetButtonValue(
                    input,
                    condition.Input,
                    out bool pressed))
                {
                    return false;
                }

                switch (condition.Comparison)
                {
                    case HandPoseComparison.IsPressed:
                        return pressed;

                    case HandPoseComparison.IsNotPressed:
                        return !pressed;

                    default:
                        return false;
                }
            }

            if (!TryGetAxisValue(
                input,
                condition.Input,
                out float value))
            {
                return false;
            }

            float threshold = condition.Threshold;

            switch (condition.Comparison)
            {
                case HandPoseComparison.GreaterThan:
                    return value > threshold;

                case HandPoseComparison.GreaterThanOrEqual:
                    return value >= threshold;

                case HandPoseComparison.LessThan:
                    return value < threshold;

                case HandPoseComparison.LessThanOrEqual:
                    return value <= threshold;

                default:
                    return false;
            }
        }

        /// <summary>
        /// Checks whether an input represents a button or touch state.
        /// </summary>
        private bool IsButtonInput(HandPoseInput input)
        {
            switch (input)
            {
                case HandPoseInput.TriggerTouch:
                case HandPoseInput.ThumbrestTouched:
                case HandPoseInput.ThumbstickTouched:
                case HandPoseInput.PrimaryTouched:
                case HandPoseInput.SecondaryTouched:
                    return true;

                default:
                    return false;
            }
        }

        /// <summary>
        /// Gets a button input value.
        /// </summary>
        private bool TryGetButtonValue(
            HandInputState input,
            HandPoseInput type,
            out bool value)
        {
            switch (type)
            {
                case HandPoseInput.TriggerTouch:
                    value = input.TriggerTouch;
                    return true;

                case HandPoseInput.ThumbrestTouched:
                    value = input.ThumbrestTouched;
                    return true;

                case HandPoseInput.ThumbstickTouched:
                    value = input.ThumbstickTouched;
                    return true;

                case HandPoseInput.PrimaryTouched:
                    value = input.PrimaryTouched;
                    return true;

                case HandPoseInput.SecondaryTouched:
                    value = input.SecondaryTouched;
                    return true;

                default:
                    value = false;
                    return false;
            }
        }

        /// <summary>
        /// Gets an analog input value.
        /// </summary>
        private bool TryGetAxisValue(
            HandInputState input,
            HandPoseInput type,
            out float value)
        {
            switch (type)
            {
                case HandPoseInput.Trigger:
                    value = input.Trigger;
                    return true;

                case HandPoseInput.Grip:
                    value = input.Grip;
                    return true;

                case HandPoseInput.TriggerProximity:
                    value = input.TriggerProximity;
                    return true;

                case HandPoseInput.TriggerCurl:
                    value = input.TriggerCurl;
                    return true;

                case HandPoseInput.TriggerSlide:
                    value = input.TriggerSlide;
                    return true;

                case HandPoseInput.TriggerForce:
                    value = input.TriggerForce;
                    return true;

                case HandPoseInput.ThumbProximity:
                    value = input.ThumbProximity;
                    return true;

                default:
                    value = 0f;
                    return false;
            }
        }
    }
}
