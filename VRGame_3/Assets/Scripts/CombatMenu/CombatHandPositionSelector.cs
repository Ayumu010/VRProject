
using UnityEngine;

public class CombatHandPositionSelector : MonoBehaviour
{
    [Header("Selection Distance")]
    [SerializeField, Min(0.001f)]
    private float enterDistance = 0.08f;

    [SerializeField, Min(0.001f)]
    private float exitDistance = 0.05f;

    [SerializeField, Min(0f)]
    private float switchMargin = 0.02f;

    private Vector3 startPosition;

    private Vector3 horizontalRight;

    private Vector2[] directions =
        new Vector2[0];

    private bool[] available =
        new bool[0];

    public int SelectedSlot { get; private set; } = -1;

    public int OptionCount =>
        directions != null ? directions.Length : 0;

    // ==================================================
    // Legacy: Default 4 Directions
    // ==================================================

    public void Begin(
        Vector3 handPosition,
        Vector3 viewRight)
    {
        Vector2[] defaultDirections =
        {
            Vector2.up,
            Vector2.right,
            Vector2.down,
            Vector2.left
        };

        bool[] defaultAvailability =
        {
            true, true, true, true
        };

        Begin(
            handPosition,
            viewRight,
            defaultDirections,
            defaultAvailability
        );
    }

    // ==================================================
    // Legacy: Optional 5th Direction
    // ==================================================

    public void Begin(
        Vector3 handPosition,
        Vector3 viewRight,
        bool enableFifthOption)
    {
        Vector2[] optionDirections =
        {
            Vector2.up,
            Vector2.right,
            Vector2.down,
            Vector2.left,
            new Vector2(0.7071068f, 0.7071068f)
        };

        bool[] optionAvailability =
        {
            true,
            true,
            true,
            true,
            enableFifthOption
        };

        int count = enableFifthOption ? 5 : 4;

        Vector2[] selectedDirections =
            new Vector2[count];

        bool[] selectedAvailability =
            new bool[count];

        for (int i = 0; i < count; i++)
        {
            selectedDirections[i] =
                optionDirections[i];

            selectedAvailability[i] =
                optionAvailability[i];
        }

        Begin(
            handPosition,
            viewRight,
            selectedDirections,
            selectedAvailability
        );
    }

    // ==================================================
    // New: Custom Directions
    // ==================================================

    public void Begin(
        Vector3 handPosition,
        Vector3 viewRight,
        Vector2[] customDirections,
        bool[] customAvailability)
    {
        if (!ConfigureOptions(
                customDirections,
                customAvailability))
        {
            return;
        }

        startPosition = handPosition;

        horizontalRight =
            Vector3.ProjectOnPlane(
                viewRight,
                Vector3.up
            );

        if (horizontalRight.sqrMagnitude < 0.0001f)
        {
            horizontalRight = Vector3.right;
        }

        horizontalRight.Normalize();

        SelectedSlot = -1;
    }

    // ==================================================
    // Configure Options
    // ==================================================

    private bool ConfigureOptions(
        Vector2[] customDirections,
        bool[] customAvailability)
    {
        if (customDirections == null ||
            customDirections.Length == 0)
        {
            Debug.LogError(
                "CombatHandPositionSelector: No options configured.",
                this
            );

            return false;
        }

        directions =
            new Vector2[customDirections.Length];

        available =
            new bool[customDirections.Length];

        for (int i = 0; i < customDirections.Length; i++)
        {
            Vector2 direction =
                customDirections[i];

            if (direction.sqrMagnitude < 0.0001f)
            {
                Debug.LogError(
                    $"CombatHandPositionSelector: Invalid direction at {i}.",
                    this
                );

                return false;
            }

            directions[i] = direction.normalized;

            available[i] =
                customAvailability == null ||
                (i < customAvailability.Length &&
                 customAvailability[i]);
        }

        return true;
    }

    // ==================================================
    // Availability
    // ==================================================

    public void SetOptionAvailable(
        int index,
        bool isAvailable)
    {
        if (index < 0 ||
            index >= available.Length)
        {
            return;
        }

        available[index] = isAvailable;

        if (!isAvailable && SelectedSlot == index)
        {
            SelectedSlot = -1;
        }
    }

    // ==================================================
    // Update Selection
    // ==================================================

    public int UpdateSelection(Vector3 handPosition)
    {
        Vector3 delta =
            handPosition - startPosition;

        float horizontal =
            Vector3.Dot(delta, horizontalRight);

        float vertical =
            Vector3.Dot(delta, Vector3.up);

        Vector2 movement =
            new Vector2(horizontal, vertical);

        int candidate = -1;

        float candidateDistance = 0f;

        // ----------------------------------------------
        // Find nearest available direction
        // ----------------------------------------------

        for (int i = 0; i < directions.Length; i++)
        {
            if (!available[i])
                continue;

            float distance =
                Vector2.Dot(
                    movement,
                    directions[i]
                );

            if (distance > candidateDistance)
            {
                candidateDistance = distance;

                candidate = i;
            }
        }

        if (candidateDistance < enterDistance)
        {
            candidate = -1;
        }

        // ----------------------------------------------
        // Clear invalid current selection
        // ----------------------------------------------

        if (SelectedSlot >= 0 &&
            (SelectedSlot >= available.Length ||
             !available[SelectedSlot]))
        {
            SelectedSlot = -1;
        }

        if (SelectedSlot < 0)
        {
            SelectedSlot = candidate;

            return SelectedSlot;
        }

        float currentDistance = Mathf.Max(
            0f,
            Vector2.Dot(
                movement,
                directions[SelectedSlot]
            )
        );

        if (candidate < 0)
        {
            if (currentDistance <= exitDistance)
            {
                SelectedSlot = -1;
            }

            return SelectedSlot;
        }

        if (candidate == SelectedSlot)
        {
            return SelectedSlot;
        }

        // ----------------------------------------------
        // Hysteresis
        // ----------------------------------------------

        if (currentDistance <= exitDistance ||
            candidateDistance >=
            currentDistance + switchMargin)
        {
            SelectedSlot = candidate;
        }

        return SelectedSlot;
    }

    // ==================================================
    // Reset
    // ==================================================

    public void ClearSelection()
    {
        SelectedSlot = -1;
    }
}
