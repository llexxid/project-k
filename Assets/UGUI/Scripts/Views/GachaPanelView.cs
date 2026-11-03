using UnityEngine;
using UnityEngine.UI;

namespace KingdomIdle.UGUI
{
    /// <summary>공통 시트 안의 스크롤 본문과 고정 뽑기 버튼 참조.</summary>
    public sealed class GachaPanelView : BottomSheetView
    {
        [SerializeField] internal RectTransform tabBar;
        [SerializeField] internal ScrollRect scroll;
        [SerializeField] internal RectTransform content;
        [SerializeField] internal GachaTabContentView authoredContent;
    }
}
