using UnityEngine;
using UnityEngine.UI;

namespace KingdomIdle.UGUI
{
    /// <summary>Panel_KingdomArmy 셸: 멤버 탭 + 스크롤 콘텐츠 + 하단 네비 바.</summary>
    public sealed class KingdomArmyPanelView : BottomSheetView
    {
        [SerializeField] internal RectTransform memberTabs;
        [SerializeField] internal ScrollRect scroll;
        [SerializeField] internal RectTransform content;
        [SerializeField] internal RectTransform navBar;
    }
}
