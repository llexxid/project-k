using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace KingdomIdle.UGUI
{
    /// <summary>
    /// 가이드 단계 카드 셀. 프리팹 Item_GuideStepRow. 인스펙터 편집 가능.
    /// 체크 버튼(원형) + 제목/설명/힌트 컬럼으로 구성되며, 완료 상태에 따라 흐림/체크 표시가 토글된다.
    /// (기존 GuidePanelController.BuildStepRow 의 런타임 코드생성 구조를 프리팹화한 것)
    /// </summary>
    public sealed class GuideStepRowView : MonoBehaviour
    {
        public CanvasGroup canvasGroup;   // 완료 단계 흐리게 (0.55)
        public Button checkButton;        // 체크 토글 버튼 (원형 테두리에 부착)
        public Image checkBorder;         // 원형 외곽 (버튼 타겟 그래픽)
        public Image checkIcon;           // 체크 아이콘 (iconCheck 존재 시 — done일 때만 표시)
        public TMP_Text checkLabel;       // 아이콘 폴백 "V" (iconCheck 없을 때)
        public TMP_Text titleLabel;       // 단계 제목 (완료 시 흐린 색)
        public TMP_Text descLabel;        // 단계 설명
        public TMP_Text hintLabel;        // 완료 힌트 (미완료 단계에만 표시)

        // .guide-step-title / .guide-step-title-done (GameUI.uss guide-* 토큰)
        private static readonly Color TitleColor = new Color(1f, 1f, 1f, 0.95f);
        private static readonly Color TitleDoneColor = new Color(1f, 1f, 1f, 0.45f);

        /// <summary>초기 값 세팅. 힌트는 미완료 단계에서만 노출(원본과 동일 — 토글 시 갱신하지 않음).</summary>
        public void Set(string title, string desc, string hint, bool done)
        {
            if (titleLabel != null) titleLabel.text = title;
            if (descLabel != null) descLabel.text = desc;
            if (hintLabel != null)
            {
                bool show = !string.IsNullOrEmpty(hint) && !done;
                hintLabel.gameObject.SetActive(show);
                if (show) hintLabel.text = hint;
            }
            SetDone(done);
        }

        /// <summary>완료 상태만 갱신 (체크 토글 시 호출). 힌트는 건드리지 않는다.</summary>
        public void SetDone(bool done)
        {
            if (canvasGroup != null) canvasGroup.alpha = done ? 0.55f : 1f;
            if (titleLabel != null) titleLabel.color = done ? TitleDoneColor : TitleColor;
            if (checkIcon != null) checkIcon.gameObject.SetActive(done);
            if (checkLabel != null) checkLabel.text = done ? "V" : "";
        }
    }
}
