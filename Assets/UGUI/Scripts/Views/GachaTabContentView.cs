using UnityEngine;
using TMPro;

namespace KingdomIdle.UGUI
{
    /// <summary>
    /// 뽑기 패널의 탭 콘텐츠 셸 (프리팹 GachaTabContent).
    /// 컨트롤러가 탭 전환 때마다 _view.content 아래에 1개 인스턴스화하고
    /// 이 View의 참조로 값/자식 위젯(확률 알약·뽑기 버튼·보상 카드)을 채운다.
    /// 코드로 UI 구조를 생성하지 않는다 (런타임 코드빌드 제거 완료).
    ///   ┌─ messageLabel  (전체 안내 메시지 — 일반 콘텐츠일 땐 숨김)
    ///   ├─ descLabel     (설명)
    ///   ├─ costLabel     (1회 비용 / 보유)
    ///   ├─ rateRow       (확률 알약 Item_RatePill 부모, HorizontalLayout)
    ///   ├─ pullRow       (뽑기 버튼 Item_GachaPullButton 부모, HorizontalLayout)
    ///   ├─ rewardSectionTitle ("획득 가능 보상")
    ///   └─ rewardGrid    (보상 카드 Item_GachaCard 그리드, GridLayout)
    /// </summary>
    public sealed class GachaTabContentView : MonoBehaviour
    {
        [SerializeField] internal TMP_Text messageLabel;
        [SerializeField] internal TMP_Text descLabel;
        [SerializeField] internal TMP_Text costLabel;
        [SerializeField] internal RectTransform rateRow;
        [SerializeField] internal RectTransform pullRow;
        [SerializeField] internal TMP_Text rewardSectionTitle;
        [SerializeField] internal RectTransform rewardGrid;
    }
}
