using UnityEngine;
using UnityEngine.UI;

namespace KingdomIdle.UGUI
{
    /// <summary>전체 화면 본문과 보존된 시트형 콘텐츠의 프리팹 참조.</summary>
    public sealed class GachaPanelView : BottomSheetView
    {
        [SerializeField] internal RectTransform tabBar;
        [SerializeField] internal ScrollRect scroll;
        [SerializeField] internal RectTransform content;
        [SerializeField] internal GachaTabContentView authoredContent;
    }
}
