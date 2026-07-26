using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace KingdomIdle.UGUI
{
    /// <summary>
    /// 마탑 스킬 장착 팝업 셸 (프리팹 Panel_MageTowerEquip). 중앙 정렬 오버레이 팝업.
    /// 컨트롤러는 이 View의 참조로 슬롯/인벤토리를 채운다 (코드 생성 없음).
    /// </summary>
    public sealed class MageTowerEquipPopupView : MonoBehaviour
    {
        public RectTransform panelBox;         // 중앙 900x680 박스 (열릴 때 PopIn 애니메이션 대상)
        public Button backdropButton;         // 딤 배경 클릭 → 닫기
        public Button closeButton;            // 타이틀바 X → 닫기
        public TMP_Text titleLabel;
        public RectTransform slotsContainer;  // 장착 슬롯 셀(MageEquipSlotView) 부모
        public ScrollRect invScroll;
        public RectTransform invGrid;         // 보유 스킬 셀(MageSkillCellView) 그리드 부모
        public UIPulseGroup pulse;            // 선택모드 펄스 구동자 (루트에 부착)
    }
}
