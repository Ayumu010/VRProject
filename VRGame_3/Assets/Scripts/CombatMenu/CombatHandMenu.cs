
using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(CombatHandPositionSelector))]
public sealed class CombatHandMenu : MonoBehaviour
{
    [Header("References")]
    [SerializeField]
    private CombatHandPositionSelector positionSelector;

    [SerializeField]
    private Transform optionContainer;

    [SerializeField]
    private CombatMenuOptionView optionViewPrefab;

    private CombatMenuDefinition activeDefinition;

    private CombatMenuDefinition.Option[] activeOptions;

    private readonly List<CombatMenuOptionView> optionViews =
        new List<CombatMenuOptionView>();

    private bool[] availableOptions;

    private bool hasConfirmed;

    public bool IsOpen { get; private set; }

    public bool HasConfirmed => hasConfirmed;

    public int SelectedIndex =>
        IsOpen && positionSelector != null
            ? positionSelector.SelectedSlot
            : -1;

    public int OptionCount =>
        activeOptions != null
            ? activeOptions.Length
            : 0;

    public string SelectedOptionId
    {
        get
        {
            int index = SelectedIndex;

            return index >= 0 && index < OptionCount
                ? activeOptions[index].Id
                : null;
        }
    }

    public int SelectedValue
    {
        get
        {
            int index = SelectedIndex;

            return index >= 0 && index < OptionCount
                ? activeOptions[index].Value
                : 0;
        }
    }

    // ==================================================
    // Awake
    // ==================================================

    private void Awake()
    {
        if (positionSelector == null)
        {
            positionSelector =
                GetComponent<CombatHandPositionSelector>();
        }

        if (optionContainer == null)
        {
            optionContainer = transform;
        }

        if (positionSelector == null)
        {
            Debug.LogError(
                "CombatHandMenu: PositionSelector is missing.",
                this
            );

            enabled = false;
        }
    }

    // ==================================================
    // Begin
    // ==================================================

    public bool Begin(
        CombatMenuDefinition definition,
        Vector3 handPosition,
        Vector3 viewRight,
        string[] allowedOptionIds = null)
    {
        Close();

        if (!enabled ||
            positionSelector == null ||
            definition == null ||
            optionViewPrefab == null)
        {
            Debug.LogError(
                "CombatHandMenu: Required references are missing.",
                this
            );

            return false;
        }

        CombatMenuDefinition.Option[] options =
            definition.Options;

        if (options == null || options.Length == 0)
        {
            Debug.LogError(
                "CombatHandMenu: Definition has no options.",
                definition
            );

            return false;
        }

        Vector2[] directions =
            new Vector2[options.Length];

        bool[] availability =
            new bool[options.Length];

        // ----------------------------------------------
        // Create Option Views
        // ----------------------------------------------

        for (int i = 0; i < options.Length; i++)
        {
            CombatMenuDefinition.Option option =
                options[i];

            if (option == null ||
                string.IsNullOrWhiteSpace(option.Id) ||
                option.SelectionDirection.sqrMagnitude < 0.0001f)
            {
                Debug.LogError(
                    $"CombatHandMenu: Invalid option at index {i}.",
                    definition
                );

                ClearOptionViews();

                return false;
            }

            directions[i] =
                option.SelectionDirection;

            bool allowed =
                allowedOptionIds == null ||
                ContainsId(allowedOptionIds, option.Id);

            availability[i] =
                option.EnabledByDefault && allowed;

            CombatMenuOptionView view =
                Instantiate(
                    optionViewPrefab,
                    optionContainer,
                    false
                );

            view.name =
                $"MenuOption_{option.Id}";

            view.Initialize(
                option,
                availability[i]
            );

            optionViews.Add(view);
        }

        activeDefinition = definition;

        activeOptions = options;

        availableOptions = availability;

        // ----------------------------------------------
        // Initialize Selector
        // ----------------------------------------------

        positionSelector.Begin(
            handPosition,
            viewRight,
            directions,
            availability
        );

        IsOpen = true;

        hasConfirmed = false;

        UpdateHighlights();

        Debug.Log(
            $"CombatHandMenu Opened : " +
            $"{definition.MenuId} / " +
            $"{GetAvailableOptionCount()} Options",
            this
        );

        return true;
    }

    // ==================================================
    // Update Selection
    // ==================================================

    public void UpdateSelection(Vector3 handPosition)
    {
        if (!IsOpen ||
            positionSelector == null)
        {
            return;
        }

        int previousSelection =
            positionSelector.SelectedSlot;

        int currentSelection =
            positionSelector.UpdateSelection(
                handPosition
            );

        if (previousSelection != currentSelection)
        {
            UpdateHighlights();

            Debug.Log(
                $"CombatMenu Selection : {SelectedOptionId}",
                this
            );
        }
    }

    // ==================================================
    // Option Availability
    // ==================================================

    public bool SetOptionAvailable(
        string optionId,
        bool available)
    {
        if (!IsOpen ||
            activeDefinition == null)
        {
            return false;
        }

        int index =
            activeDefinition.FindOptionIndex(optionId);

        if (index < 0)
            return false;

        availableOptions[index] = available;

        positionSelector.SetOptionAvailable(
            index,
            available
        );

        optionViews[index].SetAvailable(
            available
        );

        UpdateHighlights();

        return true;
    }

    public bool IsOptionAvailable(string optionId)
    {
        if (!IsOpen ||
            activeDefinition == null)
        {
            return false;
        }

        int index =
            activeDefinition.FindOptionIndex(optionId);

        return index >= 0 &&
               index < availableOptions.Length &&
               availableOptions[index];
    }

    public int GetAvailableOptionCount()
    {
        if (availableOptions == null)
            return 0;

        int count = 0;

        foreach (bool available in availableOptions)
        {
            if (available)
                count++;
        }

        return count;
    }

    // ==================================================
    // Confirm
    // ==================================================

    public bool TryConfirm(
        out CombatMenuSelection selection)
    {
        selection = default;

        if (!IsOpen ||
            hasConfirmed ||
            activeOptions == null)
        {
            return false;
        }

        int index = SelectedIndex;

        if (index < 0 ||
            index >= activeOptions.Length ||
            !availableOptions[index])
        {
            return false;
        }

        CombatMenuDefinition.Option option =
            activeOptions[index];

        selection = new CombatMenuSelection(
            option.Id,
            option.Value,
            index,
            option.Label
        );

        hasConfirmed = true;

        Debug.Log(
            $"CombatMenu Confirmed : {selection.Id} / {selection.Value}",
            this
        );

        return true;
    }

    // ==================================================
    // Cancel
    // ==================================================

    public void Cancel()
    {
        Close();
    }

    // ==================================================
    // Close
    // ==================================================

    public void Close()
    {
        IsOpen = false;

        hasConfirmed = false;

        if (positionSelector != null)
        {
            positionSelector.ClearSelection();
        }

        ClearOptionViews();

        activeDefinition = null;

        activeOptions = null;

        availableOptions = null;
    }

    // ==================================================
    // Highlight
    // ==================================================

    private void UpdateHighlights()
    {
        int selected = SelectedIndex;

        for (int i = 0; i < optionViews.Count; i++)
        {
            if (optionViews[i] == null)
                continue;

            optionViews[i].SetHighlighted(
                i == selected
            );
        }
    }

    // ==================================================
    // Utility
    // ==================================================

    private static bool ContainsId(
        string[] ids,
        string target)
    {
        if (ids == null)
            return false;

        for (int i = 0; i < ids.Length; i++)
        {
            if (ids[i] == target)
                return true;
        }

        return false;
    }

    private void ClearOptionViews()
    {
        foreach (CombatMenuOptionView view in optionViews)
        {
            if (view == null)
                continue;

            if (Application.isPlaying)
            {
                Destroy(view.gameObject);
            }
            else
            {
                DestroyImmediate(view.gameObject);
            }
        }

        optionViews.Clear();
    }

    private void OnDestroy()
    {
        ClearOptionViews();
    }
}

// ==================================================
// Confirmed Menu Data
// ==================================================

public readonly struct CombatMenuSelection
{
    public string Id { get; }

    public int Value { get; }

    public int Index { get; }

    public string Label { get; }

    public CombatMenuSelection(
        string id,
        int value,
        int index,
        string label)
    {
        Id = id;
        Value = value;
        Index = index;
        Label = label;
    }
}
