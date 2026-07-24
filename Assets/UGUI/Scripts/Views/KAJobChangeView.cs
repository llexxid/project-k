using UnityEngine;
using TMPro;

namespace KingdomIdle.UGUI
{
    /// <summary>
    /// 왕국군 전직 목록. 프리팹: Panel_KAJobChange.prefab
    /// 전직 파편 배너 + 1차/2차 전직 카드 그리드(Item_JobCard 인스턴스).
    /// </summary>
    public sealed class KAJobChangeView : MonoBehaviour
    {
        [SerializeField] internal TMP_Text placeholder;      // 직업 데이터 없음 (없으면 숨김)
        [SerializeField] internal RectTransform contentGroup; // 배너+그리드 묶음 (placeholder일 때 숨김)

        [Header("전직 파편 배너")]
        [SerializeField] internal TMP_Text bannerValue;
        [SerializeField] internal TMP_Text bannerHint;

        [Header("그리드")]
        [SerializeField] internal RectTransform basicGrid;   // 1차 전직 (Item_JobCard)
        [SerializeField] internal RectTransform eliteGrid;   // 2차 전직 (Item_JobCard)
    }
}
