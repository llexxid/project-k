using UnityEngine;
using TMPro;

namespace KingdomIdle.UGUI
{
    /// <summary>
    /// 육성 패널 본문 셸 (프리팹 Body_Development). 스크롤 콘텐츠에 1회 인스턴스화된다.
    /// 설명 라벨 / 보유 골드 바 / 강화 카드 컨테이너 / 빈 상태 라벨을 담는 고정 구조.
    /// 컨트롤러는 이 View 참조로 골드 라벨 텍스트와 카드 목록만 갱신한다(코드 생성 없음).
    /// </summary>
    public sealed class DevelopmentBodyView : MonoBehaviour
    {
        [SerializeField] internal TMP_Text descLabel;     // 안내 설명 (고정 텍스트)
        [SerializeField] internal TMP_Text goldLabel;     // 보유 골드 바 (라이브 갱신)
        [SerializeField] internal RectTransform cardsRoot; // 강화 카드(EnhanceCardView) 부모
        [SerializeField] internal TMP_Text emptyLabel;    // 강화 항목 없음 안내 (토글)

        public RectTransform CardsRoot => cardsRoot;
    }
}
