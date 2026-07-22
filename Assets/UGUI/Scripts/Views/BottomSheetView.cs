using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace KingdomIdle.UGUI
{
    /// <summary>
    /// 하단 시트형 패널 공통 셸 (PanelRoot / Backdrop / Sheet / 헤더).
    /// 기존 UXML 패널 스켈레톤 + UITKUIManager.BindPanelCommon 대응.
    /// Backdrop 탭 → 닫기, 닫기 버튼 → 닫기. 시트는 하단바(190px) 위에 앵커된다.
    /// </summary>
    public class BottomSheetView : MonoBehaviour
    {
        [SerializeField] internal Button backdrop;
        [SerializeField] internal RectTransform sheet;
        [SerializeField] internal TMP_Text title;
        [SerializeField] internal Button closeButton;

        /// <summary>패널이 스택에서 제거될 때 UIManager가 호출 — 컨트롤러 Dispose 훅.</summary>
        internal Action OnClosed;

        public RectTransform Sheet => sheet;

        public void SetTitle(string text)
        {
            if (title != null) title.text = text;
        }
    }
}
