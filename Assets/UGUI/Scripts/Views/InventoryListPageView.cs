using UnityEngine;
using TMPro;

namespace KingdomIdle.UGUI
{
    /// <summary>
    /// 인벤토리 목록 페이지 셸 (프리팹 Item_InventoryListPage).
    /// 섹션 제목 + 소섹션 제목 + 장비 그리드 + 빈 상태 플레이스홀더로 구성된 고정 구조.
    /// 컨트롤러는 텍스트/표시여부를 지정하고 grid에 itemEquipCell을 채운다(코드 생성 없음).
    /// </summary>
    public sealed class InventoryListPageView : MonoBehaviour
    {
        [SerializeField] internal TMP_Text sectionTitle;      // 예: "인벤토리" / "장비"
        [SerializeField] internal TMP_Text subsectionTitle;   // 예: "장비"
        [SerializeField] internal RectTransform grid;         // 장비 셀(itemEquipCell) 그리드 부모
        [SerializeField] internal TMP_Text placeholder;       // 빈 상태 안내

        /// <summary>섹션 제목 설정. null이면 숨김.</summary>
        public void SetSection(string text)
        {
            if (sectionTitle == null) return;
            bool has = !string.IsNullOrEmpty(text);
            sectionTitle.gameObject.SetActive(has);
            if (has) sectionTitle.text = text;
        }

        /// <summary>소섹션 제목 설정. null이면 숨김.</summary>
        public void SetSubsection(string text)
        {
            if (subsectionTitle == null) return;
            bool has = !string.IsNullOrEmpty(text);
            subsectionTitle.gameObject.SetActive(has);
            if (has) subsectionTitle.text = text;
        }

        /// <summary>플레이스홀더 설정. null이면 숨김.</summary>
        public void SetPlaceholder(string text)
        {
            if (placeholder == null) return;
            bool has = !string.IsNullOrEmpty(text);
            placeholder.gameObject.SetActive(has);
            if (has) placeholder.text = text;
        }

        /// <summary>그리드 표시/숨김 (빈 탭에서는 숨겨 레이아웃 여백 제거).</summary>
        public void SetGridActive(bool active)
        {
            if (grid != null) grid.gameObject.SetActive(active);
        }
    }
}
