using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace KingdomIdle.UGUI
{
    /// <summary>
    /// 마탑 스킬 상세 팝업 셸 (프리팹 Panel_MageTowerDetail). 중앙 정렬 오버레이 팝업.
    /// 컨트롤러(MageTowerDetailPopupController)는 이 View의 참조로 값만 세팅한다 (코드 생성 없음).
    /// 고정 구조(헤더/아이콘·스탯/강화·각성·초기화 섹션)라 반복 셀은 없다.
    /// </summary>
    public sealed class MageTowerDetailPopupView : MonoBehaviour
    {
        [Header("Shell")]
        public Button backdropButton;   // 딤 배경 클릭 → 닫기
        public Button closeButton;      // 헤더 X → 닫기
        public TMP_Text titleLabel;

        [Header("Icon + stats")]
        public Image icon;              // 스킬 아이콘 (스프라이트 없으면 비활성)
        public TMP_Text lblBaseDmg;
        public TMP_Text lblEffDmg;
        public TMP_Text lblBaseCd;
        public TMP_Text lblEffCd;

        [Header("Enhance section")]
        public TMP_Text lblEnhLevel;
        public TMP_Text lblEnhCost;
        public Button btnEnhance;
        public TMP_Text btnEnhanceLabel;

        [Header("Awaken section")]
        public TMP_Text lblAwkLevel;
        public TMP_Text lblAwkCost;
        public Button btnAwaken;
        public TMP_Text btnAwakenLabel;

        [Header("Reset section")]
        public TMP_Text lblResetRefund;
        public Button btnReset;
    }
}
