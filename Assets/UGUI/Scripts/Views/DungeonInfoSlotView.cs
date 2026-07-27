using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KingdomIdle.UGUI
{
    public sealed class DungeonInfoSlotView : MonoBehaviour
    {
        [SerializeField] private Image itemImage;
        [SerializeField] private LayoutElement layoutElement;
        [SerializeField] private TMP_Text placeholderLabel;

        private Color placeholderColor = Color.white;

        private void Awake()
        {
            if (itemImage != null)
                placeholderColor = itemImage.color;
        }

        public void SetItem(Sprite sprite, int placeholderIndex)
        {
            if (itemImage == null)
                return;

            itemImage.sprite = sprite;
            itemImage.preserveAspect = sprite != null;
            itemImage.color = sprite != null ? Color.white : placeholderColor;
            if (placeholderLabel != null)
            {
                placeholderLabel.gameObject.SetActive(sprite == null);
                placeholderLabel.text = placeholderIndex.ToString();
            }
        }

        public void SetSize(float width, float height)
        {
            if (layoutElement == null)
                layoutElement = GetComponent<LayoutElement>();
            if (layoutElement == null)
                layoutElement = gameObject.AddComponent<LayoutElement>();

            layoutElement.preferredWidth = width;
            layoutElement.preferredHeight = height;
            layoutElement.minWidth = width;
            layoutElement.minHeight = height;
        }
    }
}
