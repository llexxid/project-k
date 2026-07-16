using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace KingdomIdle.UGUI
{
    /// <summary>Panel_Guide 셸: 진행 바 + 진행 라벨 + 스크롤 리스트.</summary>
    public sealed class GuidePanelView : BottomSheetView
    {
        [SerializeField] internal Image progressFill;
        [SerializeField] internal TMP_Text progressLabel;
        [SerializeField] internal ScrollRect scroll;
        [SerializeField] internal RectTransform listContent;
    }

    /// <summary>Panel_Gacha 셸: 탭 바 + 스크롤 콘텐츠.</summary>
    public sealed class GachaPanelView : BottomSheetView
    {
        [SerializeField] internal RectTransform tabBar;
        [SerializeField] internal ScrollRect scroll;
        [SerializeField] internal RectTransform content;
    }

    /// <summary>Panel_KingdomArmy 셸: 멤버 탭 + 스크롤 콘텐츠 + 하단 네비 바.</summary>
    public sealed class KingdomArmyPanelView : BottomSheetView
    {
        [SerializeField] internal RectTransform memberTabs;
        [SerializeField] internal ScrollRect scroll;
        [SerializeField] internal RectTransform content;
        [SerializeField] internal RectTransform navBar;
    }

    /// <summary>Panel_Development 셸: 스크롤 콘텐츠만.</summary>
    public sealed class DevelopmentPanelView : BottomSheetView
    {
        [SerializeField] internal ScrollRect scroll;
        [SerializeField] internal RectTransform content;
    }

    /// <summary>Panel_Inventory 셸: 네비 바 + 스크롤 콘텐츠.</summary>
    public sealed class InventoryPanelView : BottomSheetView
    {
        [SerializeField] internal RectTransform navBar;
        [SerializeField] internal ScrollRect scroll;
        [SerializeField] internal RectTransform content;
    }

    /// <summary>Panel_Placeholder 셸: 안내 라벨만.</summary>
    public sealed class PlaceholderPanelView : BottomSheetView
    {
        [SerializeField] internal TMP_Text hint;
    }
}
