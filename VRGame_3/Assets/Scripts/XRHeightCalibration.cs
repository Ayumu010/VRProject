
using System.Collections;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR;

namespace VRPlayerCalibration
{
    /// <summary>
    /// Calibrates the initial HMD eye height.
    /// The correction is applied to the parent of Camera Offset,
    /// preserving the relative positions of the HMD and controllers.
    /// </summary>
    [DisallowMultipleComponent]
    public class XRHeightCalibration : MonoBehaviour
    {
        [Header("References")]
        [SerializeField]
        private XROrigin xrOrigin;

        [SerializeField]
        private Transform trackingCalibrationOffset;

        [Header("Height Settings")]
        [SerializeField, Range(0.8f, 2.0f)]
        private float targetEyeHeight = 1.6f;

        [Header("Tracking Initialization")]
        [SerializeField, Min(1)]
        private int requiredTrackedFrames = 5;

        [SerializeField, Min(1f)]
        private float trackingTimeout = 10f;

        [SerializeField, Min(0f)]
        private float additionalWaitAfterTracking = 0.1f;

        [Header("Debug")]
        [SerializeField]
        private bool showCalibrationLog = true;

        private bool calibrated;

        private void Awake()
        {
            if (trackingCalibrationOffset == null)
                trackingCalibrationOffset = transform;

            if (xrOrigin == null)
                xrOrigin = GetComponentInParent<XROrigin>();
        }

        private IEnumerator Start()
        {
            if (!ValidateReferences())
                yield break;

            float deadline =
                Time.realtimeSinceStartup + trackingTimeout;

            int trackedFrames = 0;

            // Wait until Floor tracking and HMD tracking are ready.
            while (Time.realtimeSinceStartup < deadline)
            {
                bool floorTracking = IsFloorTrackingActive();
                bool headTracked = IsHeadTracked();

                if (floorTracking && headTracked)
                {
                    trackedFrames++;
                }
                else
                {
                    trackedFrames = 0;
                }

                if (trackedFrames >= requiredTrackedFrames)
                    break;

                yield return null;
            }

            if (trackedFrames < requiredTrackedFrames)
            {
                Debug.LogError(
                    "XRHeightCalibration: Tracking initialization failed. " +
                    "Check HMD tracking and Floor Tracking Mode.",
                    this
                );

                yield break;
            }

            // Allow the camera pose to update before calibration.
            if (additionalWaitAfterTracking > 0f)
            {
                yield return new WaitForSecondsRealtime(
                    additionalWaitAfterTracking
                );
            }

            yield return new WaitForEndOfFrame();

            ApplyCalibration();
        }

        /// <summary>
        /// Checks required references and hierarchy.
        /// </summary>
        private bool ValidateReferences()
        {
            if (xrOrigin == null)
            {
                Debug.LogError(
                    "XRHeightCalibration: XR Origin is not assigned.",
                    this
                );
                return false;
            }

            if (xrOrigin.Camera == null)
            {
                Debug.LogError(
                    "XRHeightCalibration: XR Origin Camera is not assigned.",
                    this
                );
                return false;
            }

            if (trackingCalibrationOffset == null)
            {
                Debug.LogError(
                    "XRHeightCalibration: Tracking Calibration Offset " +
                    "is not assigned.",
                    this
                );
                return false;
            }

            Transform originTransform = xrOrigin.Origin != null
                ? xrOrigin.Origin.transform
                : xrOrigin.transform;

            if (!trackingCalibrationOffset.IsChildOf(originTransform))
            {
                Debug.LogError(
                    "XRHeightCalibration: Tracking Calibration Offset " +
                    "must be a child of XR Origin.",
                    this
                );
                return false;
            }

            return true;
        }

        /// <summary>
        /// Checks whether the runtime is using Floor tracking.
        /// </summary>
        private bool IsFloorTrackingActive()
        {
            TrackingOriginModeFlags mode =
                xrOrigin.CurrentTrackingOriginMode;

            return (mode & TrackingOriginModeFlags.Floor) != 0;
        }

        /// <summary>
        /// Checks whether the HMD is currently being tracked.
        /// </summary>
        private bool IsHeadTracked()
        {
            InputDevice headDevice =
                InputDevices.GetDeviceAtXRNode(XRNode.Head);

            if (!headDevice.isValid)
                return false;

            return headDevice.TryGetFeatureValue(
                CommonUsages.isTracked,
                out bool isTracked
            ) && isTracked;
        }

        /// <summary>
        /// Calculates and applies the initial eye height correction.
        /// </summary>
        private void ApplyCalibration()
        {
            if (calibrated)
                return;

            float currentEyeHeight =
                xrOrigin.CameraInOriginSpaceHeight;

            if (float.IsNaN(currentEyeHeight) ||
                float.IsInfinity(currentEyeHeight) ||
                currentEyeHeight < 0.1f ||
                currentEyeHeight > 2.8f)
            {
                Debug.LogError(
                    "XRHeightCalibration: Invalid HMD height: " +
                    currentEyeHeight,
                    this
                );
                return;
            }

            // Calculate the difference between current and target height.
            float correction =
                targetEyeHeight - currentEyeHeight;

            // Apply the correction to the parent of Camera Offset.
            Vector3 offsetPosition =
                trackingCalibrationOffset.localPosition;

            offsetPosition.y += correction;

            trackingCalibrationOffset.localPosition =
                offsetPosition;

            calibrated = true;

            if (showCalibrationLog)
            {
                Debug.Log(
                    $"XR Height Calibration Completed\n" +
                    $"Current Eye Height: {currentEyeHeight:F3}m\n" +
                    $"Target Eye Height: {targetEyeHeight:F3}m\n" +
                    $"Applied Correction: {correction:F3}m",
                    this
                );
            }
        }
    }
}
