
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(CombatHandMenu))]
public sealed class AsteroidSplitMenu : MonoBehaviour
{
    [Header("Menu Definition")]
    [SerializeField]
    private CombatMenuDefinition menuDefinition;

    private CombatHandMenu menu;

    public int SelectedSlot =>
        menu != null ? menu.SelectedIndex : -1;

    public int SelectedDivisionPerAxis =>
        menu != null ? menu.SelectedValue : 0;

    public int SelectedCubeCount
    {
        get
        {
            int division = SelectedDivisionPerAxis;

            return division * division * division;
        }
    }

    private void Awake()
    {
        menu = GetComponent<CombatHandMenu>();

        if (menu == null)
        {
            Debug.LogError(
                "AsteroidSplitMenu: CombatHandMenu is missing.",
                this
            );
        }
    }

    // ==================================================
    // Begin
    // ==================================================

    public void Begin(
        Vector3 handPosition,
        Vector3 viewRight)
    {
        if (menuDefinition == null)
        {
            Debug.LogError(
                "AsteroidSplitMenu: MenuDefinition is missing.",
                this
            );

            return;
        }

        menu.Begin(
            menuDefinition,
            handPosition,
            viewRight
        );
    }

    // ==================================================
    // Update
    // ==================================================

    public void UpdateSelection(Vector3 handPosition)
    {
        if (menu == null)
            return;

        menu.UpdateSelection(handPosition);
    }

    // ==================================================
    // Cleanup
    // ==================================================

    public void Close()
    {
        if (menu != null)
        {
            menu.Close();
        }
    }
}
