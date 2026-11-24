
using UnityEngine;
using UnityEngine.UI;

public class CombatMenuUI : MonoBehaviour
{
    [Header("Slot Images")]
    [SerializeField]
    private Image[] slotImages = new Image[4];

    [Header("Colors")]
    [SerializeField]
    private Color normalColor = Color.gray;

    [SerializeField]
    private Color selectedColor = Color.cyan;

    private Vector3[] originalScales;

    private void Awake()
    {
        originalScales = new Vector3[slotImages.Length];

        for (int i = 0; i < slotImages.Length; i++)
        {
            if (slotImages[i] != null)
            {
                originalScales[i] =
                    slotImages[i].rectTransform.localScale;
            }
        }

        SetSelected(-1);
    }

    /// <summary>
    /// ëIëèÛë‘ÇUIÇ…îΩâfÇ∑ÇÈ
    /// </summary>
    public void SetSelected(int selectedIndex)
    {
        for (int i = 0; i < slotImages.Length; i++)
        {
            Image image = slotImages[i];

            if (image == null)
                continue;

            bool selected = i == selectedIndex;

            image.color =
                selected ? selectedColor : normalColor;

            image.rectTransform.localScale =
                selected
                    ? originalScales[i] * 1.12f
                    : originalScales[i];
        }
    }
}
