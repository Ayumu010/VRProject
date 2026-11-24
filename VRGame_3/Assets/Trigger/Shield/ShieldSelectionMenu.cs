
using UnityEngine;

[RequireComponent(typeof(CombatHandPositionSelector))]
public sealed class ShieldSelectionMenu : MonoBehaviour
{
    [Header("Menu Options")]
    [SerializeField]
    private GameObject[] optionObjects =
        new GameObject[5];

    [Header("Selection Highlights")]
    [SerializeField]
    private GameObject[] selectionHighlights =
        new GameObject[5];

    private CombatHandPositionSelector positionSelector;

    private bool wholeBodyAvailable;

    public int SelectedOption =>
        positionSelector != null
            ? positionSelector.SelectedSlot
            : -1;

    public bool WholeBodyAvailable =>
        wholeBodyAvailable;

    public bool IsInitialized { get; private set; }

    // ==================================================
    // Awake
    // ==================================================

    private void Awake()
    {
        positionSelector =
            GetComponent<CombatHandPositionSelector>();

        if (positionSelector == null)
        {
            Debug.LogError(
                "ShieldSelectionMenu: CombatHandPositionSelector is missing.",
                this
            );
        }
    }

    // ==================================================
    // Begin
    // ==================================================

    public void Begin(
        Vector3 handPosition,
        Vector3 viewRight,
        bool enableWholeBody)
    {
        if (positionSelector == null)
            return;

        wholeBodyAvailable = enableWholeBody;

        positionSelector.Begin(
            handPosition,
            viewRight,
            wholeBodyAvailable
        );

        UpdateOptionVisibility();

        UpdateHighlight();

        IsInitialized = true;

        Debug.Log(
            $"ShieldSelectionMenu: Initialized / " +
            $"Options = {positionSelector.OptionCount}"
        );
    }

    // ==================================================
    // Update Selection
    // ==================================================

    public void UpdateSelection(Vector3 handPosition)
    {
        if (!IsInitialized ||
            positionSelector == null)
        {
            return;
        }

        int previousSelection =
            positionSelector.SelectedSlot;

        positionSelector.UpdateSelection(
            handPosition
        );

        int currentSelection =
            positionSelector.SelectedSlot;

        if (previousSelection == currentSelection)
            return;

        UpdateHighlight();

        if (currentSelection >= 0)
        {
            Debug.Log(
                $"Shield Menu Selected : {currentSelection}"
            );
        }
        else
        {
            Debug.Log(
                "Shield Menu Selection Cleared"
            );
        }
    }

    // ==================================================
    // Option Visibility
    // ==================================================

    private void UpdateOptionVisibility()
    {
        if (optionObjects == null)
            return;

        for (int i = 0; i < optionObjects.Length; i++)
        {
            if (optionObjects[i] == null)
                continue;

            bool shouldShow =
                i < 4 ||
                (i == 4 && wholeBodyAvailable);

            optionObjects[i].SetActive(
                shouldShow
            );
        }
    }

    // ==================================================
    // Highlight
    // ==================================================

    private void UpdateHighlight()
    {
        if (selectionHighlights == null)
            return;

        for (int i = 0; i < selectionHighlights.Length; i++)
        {
            if (selectionHighlights[i] == null)
                continue;

            selectionHighlights[i].SetActive(
                i == SelectedOption
            );
        }
    }
}
