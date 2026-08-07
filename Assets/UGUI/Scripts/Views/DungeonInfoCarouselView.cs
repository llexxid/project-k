using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KingdomIdle.UGUI
{
    public sealed class DungeonInfoCarouselView : MonoBehaviour
    {
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private RectTransform viewport;
        [SerializeField] private RectTransform content;
        [SerializeField] private HorizontalLayoutGroup layout;
        [SerializeField] private DungeonInfoSlotView slotTemplate;
        [SerializeField] private TMP_Text emptyLabel;
        [SerializeField] private Button previousButton;
        [SerializeField] private Button nextButton;
        [SerializeField] private int visibleCapacity = 3;
        [SerializeField] private float slotWidth = 82f;
        [SerializeField] private float slotHeight = 82f;

        private readonly List<DungeonInfoSlotView> slots = new();
        private int itemCount;

        private void Awake()
        {
            if (previousButton != null)
                previousButton.onClick.AddListener(ScrollPrevious);
            if (nextButton != null)
                nextButton.onClick.AddListener(ScrollNext);
        }

        private void OnDestroy()
        {
            if (previousButton != null)
                previousButton.onClick.RemoveListener(ScrollPrevious);
            if (nextButton != null)
                nextButton.onClick.RemoveListener(ScrollNext);
        }

        public void SetItems(IReadOnlyList<Sprite> sprites)
        {
            int count = sprites != null ? sprites.Count : 0;
            itemCount = count;
            EnsureSlotCount(count);

            for (int i = 0; i < slots.Count; i++)
            {
                bool visible = i < count;
                slots[i].gameObject.SetActive(visible);
                if (!visible)
                    continue;

                slots[i].SetSize(slotWidth, slotHeight);
                slots[i].SetItem(sprites[i], i + 1);
            }

            if (emptyLabel != null)
                emptyLabel.gameObject.SetActive(count == 0);
            if (scrollRect != null)
            {
                scrollRect.horizontal = count > visibleCapacity;
                scrollRect.horizontalNormalizedPosition = 0f;
            }
            bool hasOverflow = count > visibleCapacity;
            if (previousButton != null)
                previousButton.gameObject.SetActive(hasOverflow);
            if (nextButton != null)
                nextButton.gameObject.SetActive(hasOverflow);

            ResizeContent(count);
            UpdateArrowState();
        }

        private void ScrollPrevious()
        {
            ScrollPage(-1);
        }

        private void ScrollNext()
        {
            ScrollPage(1);
        }

        private void ScrollPage(int direction)
        {
            if (scrollRect == null || itemCount <= visibleCapacity)
                return;

            float step = Mathf.Clamp01(
                (float)visibleCapacity / Mathf.Max(1, itemCount - visibleCapacity));
            scrollRect.horizontalNormalizedPosition = Mathf.Clamp01(
                scrollRect.horizontalNormalizedPosition + direction * step);
            UpdateArrowState();
        }

        private void UpdateArrowState()
        {
            if (scrollRect == null)
                return;
            if (previousButton != null)
                previousButton.interactable = scrollRect.horizontalNormalizedPosition > 0.001f;
            if (nextButton != null)
                nextButton.interactable = scrollRect.horizontalNormalizedPosition < 0.999f;
        }

        private void EnsureSlotCount(int count)
        {
            if (slotTemplate == null || content == null)
                return;

            while (slots.Count < count)
            {
                DungeonInfoSlotView slot = Instantiate(slotTemplate, content);
                slot.name = $"Slot_{slots.Count + 1:00}";
                slot.gameObject.SetActive(true);
                slots.Add(slot);
            }

            slotTemplate.gameObject.SetActive(false);
        }

        private void ResizeContent(int count)
        {
            if (content == null || viewport == null)
                return;

            float spacing = layout != null ? layout.spacing : 0f;
            float preferredWidth = count > 0
                ? count * slotWidth + (count - 1) * spacing
                : 0f;
            float viewportWidth = viewport.rect.width;
            content.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Horizontal,
                Mathf.Max(viewportWidth, preferredWidth));

            if (layout != null)
                layout.childAlignment = TextAnchor.MiddleCenter;
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        }
    }
}
