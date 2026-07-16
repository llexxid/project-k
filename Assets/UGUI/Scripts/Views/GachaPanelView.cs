using UnityEngine;
using UnityEngine.UI;

namespace KingdomIdle.UGUI
{
    /// <summary>Panel_Gacha 셸: 탭 바 + 스크롤 콘텐츠.</summary>
    public sealed class GachaPanelView : BottomSheetView
    {
        [SerializeField] internal RectTransform tabBar;
        [SerializeField] internal ScrollRect scroll;
        [SerializeField] internal RectTransform content;
    }
}
