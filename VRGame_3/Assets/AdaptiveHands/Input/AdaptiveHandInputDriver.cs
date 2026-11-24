
using UnityEngine;
using UnityEngine.InputSystem;
using Debug = UnityEngine.Debug;

namespace AdaptiveHands
{
    /// <summary>
    /// Reads Quest Touch Plus controller inputs
    /// and sends them to the hand pose controller.
    /// Attach one instance to each RiggedHand object.
    /// </summary>
    public class AdaptiveHandInputDriver : MonoBehaviour
    {
        public enum HandSide
        {
            Left,
            Right
        }

        [Header("References")]
        [SerializeField] private RiggedHand riggedHand;
        [SerializeField] private HandPoseController poseController;
        [SerializeField] private InputActionAsset inputActions;

        [Header("Controller")]
        [SerializeField] private HandSide handSide;

#if UNITY_EDITOR
        [Header("Editor Debug Input")]
        [SerializeField] private bool useDebugInput;

        [Header("Analog Inputs")]
        [SerializeField, Range(0f, 1f)]
        private float debugTrigger;

        [SerializeField, Range(0f, 1f)]
        private float debugGrip;

        [Header("Index Inputs")]
        [SerializeField] private bool debugTriggerTouch;

        [SerializeField, Range(0f, 1f)]
        private float debugTriggerProximity;

        [SerializeField, Range(0f, 1f)]
        private float debugTriggerCurl;

        [SerializeField, Range(0f, 1f)]
        private float debugTriggerSlide;

        [SerializeField, Range(0f, 1f)]
        private float debugTriggerForce;

        [Header("Thumb Inputs")]
        [SerializeField, Range(0f, 1f)]
        private float debugThumbProximity;

        [SerializeField] private bool debugThumbrestTouched;
        [SerializeField] private bool debugThumbstickTouched;
        [SerializeField] private bool debugPrimaryTouched;
        [SerializeField] private bool debugSecondaryTouched;
#endif

        private InputAction trigger;
        private InputAction grip;
        private InputAction triggerTouch;
        private InputAction triggerProximity;
        private InputAction triggerCurl;
        private InputAction triggerSlide;
        private InputAction triggerForce;
        private InputAction thumbProximity;
        private InputAction thumbrestTouched;
        private InputAction thumbstickTouched;
        private InputAction primaryTouched;
        private InputAction secondaryTouched;

        private bool initialized;

        private void Awake()
        {
            if (riggedHand == null)
                riggedHand = GetComponent<RiggedHand>();

            if (poseController == null)
                poseController = GetComponent<HandPoseController>();

            if (riggedHand == null)
            {
                Debug.LogError(
                    "RiggedHand reference is missing.",
                    this);
                return;
            }

            if (poseController == null)
            {
                Debug.LogError(
                    "HandPoseController reference is missing.",
                    this);
                return;
            }

            InputActionMap map = null;

            if (inputActions != null)
            {
                map = inputActions.FindActionMap(
                    "AdaptiveHands", false);
            }

            if (map != null)
            {
                string prefix =
                    handSide == HandSide.Left ? "L_" : "R_";

                trigger = FindAction(map, prefix + "Trigger");
                grip = FindAction(map, prefix + "Grip");
                triggerTouch = FindAction(map, prefix + "TriggerTouch");
                triggerProximity = FindAction(map, prefix + "TriggerProximity");
                triggerCurl = FindAction(map, prefix + "TriggerCurl");
                triggerSlide = FindAction(map, prefix + "TriggerSlide");
                triggerForce = FindAction(map, prefix + "TriggerForce");
                thumbProximity = FindAction(map, prefix + "ThumbProximity");
                thumbrestTouched = FindAction(map, prefix + "ThumbrestTouched");
                thumbstickTouched = FindAction(map, prefix + "ThumbstickTouched");
                primaryTouched = FindAction(map, prefix + "PrimaryTouched");
                secondaryTouched = FindAction(map, prefix + "SecondaryTouched");
            }

#if UNITY_EDITOR
            if (useDebugInput)
            {
                initialized = true;
                return;
            }
#endif

            if (inputActions == null)
            {
                Debug.LogError(
                    "AdaptiveHandsInputActions is not assigned.",
                    this);
                return;
            }

            if (map == null)
            {
                Debug.LogError(
                    "Action Map 'AdaptiveHands' was not found.",
                    this);
                return;
            }

            initialized = true;
        }

        private InputAction FindAction(
            InputActionMap map,
            string actionName)
        {
            InputAction action = map.FindAction(actionName, false);

            if (action == null)
            {
                Debug.LogError(
                    $"Input Action '{actionName}' was not found.",
                    this);
            }

            return action;
        }

        private float ReadAxis(InputAction action)
        {
            if (action == null || !action.enabled)
                return 0f;

            return Mathf.Clamp01(action.ReadValue<float>());
        }

        private bool ReadButton(InputAction action)
        {
            return action != null &&
                   action.enabled &&
                   action.IsPressed();
        }

        private void Update()
        {
            if (!initialized)
                return;

            HandInputState inputState;

#if UNITY_EDITOR
            // Use manually configured inputs in Unity Editor.
            if (useDebugInput)
            {
                inputState = new HandInputState(
                    debugTrigger,
                    debugGrip,
                    debugTriggerTouch,
                    debugTriggerProximity,
                    debugTriggerCurl,
                    debugTriggerSlide,
                    debugTriggerForce,
                    debugThumbProximity,
                    debugThumbrestTouched,
                    debugThumbstickTouched,
                    debugPrimaryTouched,
                    debugSecondaryTouched
                );

                poseController.ProcessInput(inputState);

                // Dynamic collision processing is excluded
                // from manual pose debugging.
                return;
            }
#endif

            // Read actual controller inputs.
            float triggerValue = ReadAxis(trigger);
            float gripValue = ReadAxis(grip);

            bool isTriggerTouched = ReadButton(triggerTouch);

            float proximityValue = ReadAxis(triggerProximity);
            float curlValue = ReadAxis(triggerCurl);
            float slideValue = ReadAxis(triggerSlide);
            float forceValue = ReadAxis(triggerForce);

            float thumbValue = ReadAxis(thumbProximity);

            bool thumbrest = ReadButton(thumbrestTouched);
            bool thumbstick = ReadButton(thumbstickTouched);
            bool primary = ReadButton(primaryTouched);
            bool secondary = ReadButton(secondaryTouched);

            inputState = new HandInputState(
                triggerValue,
                gripValue,
                isTriggerTouched,
                proximityValue,
                curlValue,
                slideValue,
                forceValue,
                thumbValue,
                thumbrest,
                thumbstick,
                primary,
                secondary
            );

            // Process input and apply the resulting hand pose.
            poseController.ProcessInput(inputState);

            //// Preserve original dynamic collision behavior.
            //if (triggerValue >= 0.999f)
            //{
            //    riggedHand.PoseHandDynamic();
            //}
        }
    }
}
