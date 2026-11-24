
using UnityEngine;

namespace AdaptiveHands
{
    /// <summary>
    /// Stores the current controller input state.
    /// One instance represents one hand.
    /// </summary>
    public readonly struct HandInputState
    {
        // Analog inputs
        public readonly float Trigger;
        public readonly float Grip;

        // Index finger inputs
        public readonly bool TriggerTouch;
        public readonly float TriggerProximity;
        public readonly float TriggerCurl;
        public readonly float TriggerSlide;
        public readonly float TriggerForce;

        // Thumb inputs
        public readonly float ThumbProximity;
        public readonly bool ThumbrestTouched;
        public readonly bool ThumbstickTouched;
        public readonly bool PrimaryTouched;
        public readonly bool SecondaryTouched;

        public HandInputState(
            float trigger,
            float grip,
            bool triggerTouch,
            float triggerProximity,
            float triggerCurl,
            float triggerSlide,
            float triggerForce,
            float thumbProximity,
            bool thumbrestTouched,
            bool thumbstickTouched,
            bool primaryTouched,
            bool secondaryTouched)
        {
            Trigger = Mathf.Clamp01(trigger);
            Grip = Mathf.Clamp01(grip);

            TriggerTouch = triggerTouch;
            TriggerProximity = Mathf.Clamp01(triggerProximity);
            TriggerCurl = Mathf.Clamp01(triggerCurl);
            TriggerSlide = Mathf.Clamp01(triggerSlide);
            TriggerForce = Mathf.Clamp01(triggerForce);

            ThumbProximity = Mathf.Clamp01(thumbProximity);
            ThumbrestTouched = thumbrestTouched;
            ThumbstickTouched = thumbstickTouched;
            PrimaryTouched = primaryTouched;
            SecondaryTouched = secondaryTouched;
        }
    }
}
