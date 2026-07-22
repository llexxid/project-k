using UnityEngine;
using UnityEngine.UI;

namespace KingdomIdle.UGUI
{
    /// <summary>Panel_Development 셸: 스크롤 콘텐츠만.</summary>
    public sealed class DevelopmentPanelView : BottomSheetView
    {
        [SerializeField] internal ScrollRect scroll;
        [SerializeField] internal RectTransform content;
    }
}
