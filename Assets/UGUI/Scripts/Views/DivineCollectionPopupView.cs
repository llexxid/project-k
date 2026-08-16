using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace KingdomIdle.UGUI
{
    /// <summary>
    /// 신 스킬 컬렉션북(도감) 팝업 셸 (프리팹 Popup_DivineCollection). 중앙 정렬 오버레이 팝업.
    /// 컨트롤러는 이 View의 참조로 카드 그리드/상세 페인을 채운다 (코드 생성 없음).
    /// </summary>
    public sealed class DivineCollectionPopupView : MonoBehaviour
    {
        [Header("셸")]
        public RectTransform panelBox;        // 중앙 960x1200 박스 (열릴 때 PopIn 애니메이션 대상)
        public Button backdropButton;         // 딤 배경 클릭 → 닫기
        public Button closeButton;            // 타이틀바 X → 닫기
        public TMP_Text titleLabel;
        public TMP_Text bonusLabel;           // 컬렉션 보너스 요약 줄
        public RectTransform cardGrid;        // 카드 셀(DivineCardItemView) 4x2 그리드 부모

        [Header("상세 페인")]
        public Image illustration;            // 일러스트 (없으면 아이콘, 그것도 없으면 비활성)
        public TMP_Text cardNameLabel;        // 카드(초월자) 이름 — 등급색
        public Image gradePill;               // 등급 알약 배경 (등급색 틴트)
        public TMP_Text gradePillLabel;       // "영웅/전설/신화"
        public TMP_Text skillNameLabel;       // 스킬 이름
        public TMP_Text descriptionLabel;
        public TMP_Text statCooldownLabel;    // 쿨타임
        public TMP_Text statMultiplierLabel;  // 레벨 배율
        public TMP_Text statValueLabel;       // 예상 피해/회복량 (효과 종류에 따라 숨김)

        [Header("액션")]
        public Button equipButton;
        public TMP_Text equipButtonLabel;     // "장착" / "장착됨"
        public Button levelUpButton;
        public TMP_Text levelUpButtonLabel;   // "레벨업 (N/M)"
        public TMP_Text lockedHintLabel;      // 미보유 안내 (버튼 대신 표시)
    }
}
