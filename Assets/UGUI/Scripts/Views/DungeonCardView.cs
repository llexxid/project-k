using System;
using Scripts.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KingdomIdle.UGUI
{
    public sealed class DungeonCardView : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private Image previewImage;
        [SerializeField] private TMP_Text dungeonName;
        [SerializeField] private TMP_Text description;
        [SerializeField] private Image dungeonIcon;
        [SerializeField] private eStageType dungeonType;
        public event Action<DungeonCardView> Clicked;

        public string DungeonName => dungeonName != null ? dungeonName.text : string.Empty;
        public string Description => description != null ? description.text : string.Empty;
        public Sprite PreviewSprite => previewImage != null ? previewImage.sprite : null;
        public Color PreviewColor => previewImage != null ? previewImage.color : Color.white;
        public Sprite DungeonIcon => dungeonIcon != null ? dungeonIcon.sprite : null;
        public eStageType DungeonType => dungeonType;

        private void Awake()
        {
            if (button != null)
                button.onClick.AddListener(HandleClick);
        }

        private void OnDestroy()
        {
            if (button != null)
                button.onClick.RemoveListener(HandleClick);
        }

        private void HandleClick()
        {
            Clicked?.Invoke(this);
        }
    }
}
