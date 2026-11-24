
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Samples.StarterAssets;
using Debug = UnityEngine.Debug;

namespace VRPlayerAnimation
{
    /// <summary>
    /// Converts world-space VR movement into
    /// character-relative movement for strafe animation.
    /// </summary>
    [DisallowMultipleComponent]
    public class VRStrafeAnimationController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField]
        private DynamicMoveProvider moveProvider;

        [SerializeField]
        private Animator animator;

        [SerializeField]
        private Transform characterForward;

        [Header("Animator Parameters")]
        [SerializeField]
        private string moveXParameter = "MoveX";

        [SerializeField]
        private string moveYParameter = "MoveY";

        [Header("Movement Settings")]
        [SerializeField, Range(0f, 0.5f)]
        private float movementDeadZone = 0.05f;

        [SerializeField, Min(0f)]
        private float dampTime = 0.1f;

        [Header("Input Response")]
        [SerializeField, Range(1f, 4f)]
        private float inputResponseExponent = 2f;

        private int moveXHash;
        private int moveYHash;

        private bool hasMoveXParameter;
        private bool hasMoveYParameter;

        private void Awake()
        {
            // Get Animator automatically if not assigned.
            if (animator == null)
                animator = GetComponent<Animator>();

            // Use the Animator transform as the default
            // character forward reference.
            if (characterForward == null && animator != null)
                characterForward = animator.transform;

            if (moveProvider == null)
            {
                Debug.LogError(
                    "VRStrafeAnimationController: " +
                    "DynamicMoveProvider is not assigned.",
                    this
                );
                return;
            }

            if (animator == null)
            {
                Debug.LogError(
                    "VRStrafeAnimationController: " +
                    "Animator is not assigned.",
                    this
                );
                return;
            }

            if (characterForward == null)
            {
                Debug.LogError(
                    "VRStrafeAnimationController: " +
                    "Character Forward is not assigned.",
                    this
                );
                return;
            }

            moveXHash = Animator.StringToHash(moveXParameter);
            moveYHash = Animator.StringToHash(moveYParameter);

            hasMoveXParameter =
                HasFloatParameter(moveXHash);

            hasMoveYParameter =
                HasFloatParameter(moveYHash);

            if (!hasMoveXParameter)
            {
                Debug.LogError(
                    $"Animator Float Parameter '{moveXParameter}' " +
                    "was not found.",
                    this
                );
            }

            if (!hasMoveYParameter)
            {
                Debug.LogError(
                    $"Animator Float Parameter '{moveYParameter}' " +
                    "was not found.",
                    this
                );
            }
        }

        /// <summary>
        /// Checks whether the Animator contains a Float parameter.
        /// </summary>
        private bool HasFloatParameter(int parameterHash)
        {
            foreach (AnimatorControllerParameter parameter
                     in animator.parameters)
            {
                if (parameter.nameHash == parameterHash &&
                    parameter.type == AnimatorControllerParameterType.Float)
                {
                    return true;
                }
            }

            return false;
        }

        private void LateUpdate()
        {
            if (moveProvider == null ||
                animator == null ||
                characterForward == null)
            {
                return;
            }

            // Get calculated world-space movement.
            Vector3 worldMove = moveProvider.DesiredMoveWorld;

            // Ignore vertical movement.
            worldMove.y = 0f;

            // Extract the character's horizontal facing rotation.
            float characterYaw = characterForward.eulerAngles.y;

            Quaternion characterRotation =
                Quaternion.Euler(0f, characterYaw, 0f);

            // Convert world movement into character-local movement.
            Vector3 localMove =
                Quaternion.Inverse(characterRotation) * worldMove;

            // Get movement direction in character-local space.
            Vector2 localDirection = new Vector2(
                localMove.x,
                localMove.z
            );

            Vector2 targetMove = Vector2.zero;

            // Get original stick input magnitude.
            float inputMagnitude =
                moveProvider.DesiredMoveInputMagnitude;

            // Apply dead zone and input response.
            if (inputMagnitude > movementDeadZone &&
                localDirection.sqrMagnitude > 0.000001f)
            {
                // Remap input magnitude from dead zone to 0-1.
                float normalizedMagnitude = Mathf.InverseLerp(
                    movementDeadZone,
                    1f,
                    inputMagnitude
                );

                // Adjust sensitivity using an exponential curve.
                float animationIntensity = Mathf.Pow(
                    normalizedMagnitude,
                    inputResponseExponent
                );

                // Preserve movement direction and apply intensity.
                targetMove =
                    localDirection.normalized * animationIntensity;
            }

            // Update Animator parameters with smoothing.
            if (hasMoveXParameter)
            {
                animator.SetFloat(
                    moveXHash,
                    targetMove.x,
                    dampTime,
                    Time.deltaTime
                );
            }

            if (hasMoveYParameter)
            {
                animator.SetFloat(
                    moveYHash,
                    targetMove.y,
                    dampTime,
                    Time.deltaTime
                );
            }
        }
    }
}
