
using UnityEngine;

public sealed class CombatMenuOptionView : MonoBehaviour
{
    [Header("View References")]
    [SerializeField]
    private Transform visualAnchor;

    [SerializeField]
    private SpriteRenderer iconRenderer;

    [SerializeField]
    private TextMesh labelText;

    [SerializeField]
    private GameObject highlightObject;

    private GameObject spawnedVisual;

    public string OptionId { get; private set; }

    public bool IsAvailable { get; private set; }

    // ==================================================
    // Initialize
    // ==================================================

    public void Initialize(
        CombatMenuDefinition.Option option,
        bool available)
    {
        if (option == null)
            return;

        OptionId = option.Id;

        transform.localPosition = new Vector3(
            option.LayoutPosition.x,
            option.LayoutPosition.y,
            0f
        );

        if (labelText != null)
        {
            labelText.text = option.Label;
        }

        if (iconRenderer != null)
        {
            iconRenderer.sprite = option.Icon;

            iconRenderer.enabled =
                option.Icon != null;
        }

        CreateCustomVisual(
            option.VisualPrefab
        );

        SetAvailable(available);

        SetHighlighted(false);
    }

    // ==================================================
    // Custom Visual
    // ==================================================

    private void CreateCustomVisual(GameObject prefab)
    {
        if (visualAnchor == null ||
            prefab == null)
        {
            return;
        }

        spawnedVisual = Instantiate(
            prefab,
            visualAnchor,
            false
        );

        spawnedVisual.transform.localPosition =
            Vector3.zero;

        spawnedVisual.transform.localRotation =
            Quaternion.identity;

        spawnedVisual.transform.localScale =
            Vector3.one;
    }

    // ==================================================
    // Availability
    // ==================================================

    public void SetAvailable(bool available)
    {
        IsAvailable = available;

        gameObject.SetActive(available);

        if (!available)
        {
            SetHighlighted(false);
        }
    }

    // ==================================================
    // Highlight
    // ==================================================

    public void SetHighlighted(bool highlighted)
    {
        if (highlightObject != null)
        {
            highlightObject.SetActive(
                highlighted && IsAvailable
            );
        }
    }

    // ==================================================
    // Cleanup
    // ==================================================

    private void OnDestroy()
    {
        if (spawnedVisual != null)
        {
            Destroy(spawnedVisual);
        }
    }
}
