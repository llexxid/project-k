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
        private LayoutElement _contentSize;

        private void LateUpdate()
        {
            if (scroll == null || scroll.viewport == null || content == null) return;
            if (_contentSize == null) _contentSize = content.GetComponent<LayoutElement>();
            float height = scroll.viewport.rect.height;
            // 높이만 보정해 레이아웃 갱신 때 ScrollRect의 스크롤 위치를 보존한다.
            if (_contentSize != null && Mathf.Abs(_contentSize.minHeight - height) > .5f)
                _contentSize.minHeight = height;
        }
    }
}
