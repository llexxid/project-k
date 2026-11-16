using UnityEngine;
using TMPro;

namespace KingdomIdle.UGUI
{
    /// <summary>
    /// 가이드 목록 빈 상태 힌트. 프리팹 Item_GuideEmptyHint.
    /// (.guide-empty-hint: 26px, 흰색 50%, 중앙 정렬, padding-top 40px)
    /// </summary>
    public sealed class GuideEmptyHintView : MonoBehaviour
    {
        public TMP_Text label;

        public void SetText(string text)
        {
            if (label == null) return;
            // 빈 안내는 밝은 퀘스트 카드 밖, 어두운 패널 본문 위에 표시한다.
            if (label.color != UguiTheme.TextSecondary) label.color = UguiTheme.TextSecondary;
            if (label.text != text) label.text = text;
        }
    }
}
