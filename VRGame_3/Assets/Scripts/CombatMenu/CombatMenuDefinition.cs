
using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "CombatMenuDefinition",
    menuName = "Game/Combat Menu/Definition"
)]
public sealed class CombatMenuDefinition : ScriptableObject
{
    [Serializable]
    public sealed class Option
    {
        [Header("Identification")]
        [SerializeField]
        private string id;

        [SerializeField]
        private int value;

        [SerializeField]
        private string label;

        [Header("Visual")]
        [SerializeField]
        private Sprite icon;

        [SerializeField]
        private GameObject visualPrefab;

        [Header("Position")]
        [SerializeField]
        private Vector2 selectionDirection =
            Vector2.up;

        [SerializeField]
        private Vector2 layoutPosition =
            Vector2.up * 0.15f;

        [Header("Availability")]
        [SerializeField]
        private bool enabledByDefault = true;

        public string Id => id;
        public int Value => value;
        public string Label => label;
        public Sprite Icon => icon;
        public GameObject VisualPrefab => visualPrefab;
        public Vector2 SelectionDirection => selectionDirection;
        public Vector2 LayoutPosition => layoutPosition;
        public bool EnabledByDefault => enabledByDefault;
    }

    [Header("Definition")]
    [SerializeField]
    private string menuId;

    [SerializeField]
    private string menuName;

    [Header("Options")]
    [SerializeField]
    private Option[] options =
        Array.Empty<Option>();

    public string MenuId => menuId;
    public string MenuName => menuName;
    public Option[] Options => options;

    public int OptionCount =>
        options != null ? options.Length : 0;

    // ==================================================
    // Option Lookup
    // ==================================================

    public int FindOptionIndex(string optionId)
    {
        if (string.IsNullOrEmpty(optionId) ||
            options == null)
        {
            return -1;
        }

        for (int i = 0; i < options.Length; i++)
        {
            if (options[i] != null &&
                options[i].Id == optionId)
            {
                return i;
            }
        }

        return -1;
    }

    // ==================================================
    // Validation
    // ==================================================

    private void OnValidate()
    {
        if (options == null)
            return;

        HashSet<string> usedIds =
            new HashSet<string>();

        for (int i = 0; i < options.Length; i++)
        {
            Option option = options[i];

            if (option == null)
                continue;

            if (string.IsNullOrWhiteSpace(option.Id))
            {
                Debug.LogWarning(
                    $"{name}: Option {i} has an empty ID.",
                    this
                );

                continue;
            }

            if (!usedIds.Add(option.Id))
            {
                Debug.LogWarning(
                    $"{name}: Duplicate Option ID: {option.Id}",
                    this
                );
            }

            if (option.SelectionDirection.sqrMagnitude < 0.0001f)
            {
                Debug.LogWarning(
                    $"{name}: Option {option.Id} has an invalid direction.",
                    this
                );
            }
        }
    }
}
