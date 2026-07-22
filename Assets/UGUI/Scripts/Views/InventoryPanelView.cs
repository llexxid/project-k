using UnityEngine;
using UnityEngine.UI;

namespace KingdomIdle.UGUI
{
    /// <summary>Panel_Inventory 셸: 네비 바 + 스크롤 콘텐츠.</summary>
    public sealed class InventoryPanelView : BottomSheetView
    {
        [SerializeField] internal RectTransform navBar;
        [SerializeField] internal ScrollRect scroll;
        [SerializeField] internal RectTransform content;
    }
}
