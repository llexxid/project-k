using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace KingdomIdle.UGUI
{
    /// <summary>
    /// 궁극기 컷인 오버레이 셸 (직렬화 참조만, 로직 없음).
    /// 암전 → 일러스트 슬라이드 인 → 등급 리본 + 이름 플레이트 → 섬광 아웃.
    /// 일러스트/아이콘이 모두 없어도 이름 플레이트만으로 연출이 끝까지 돌아야 한다.
    /// </summary>
    public sealed class DivineCutInView : MonoBehaviour
    {
        [SerializeField] internal Image scrim;                 // 전체 암전 (입력 차단 겸용)
        [SerializeField] internal RectTransform illustHolder;  // 옆에서 밀려 들어오는 대상
        [SerializeField] internal CanvasGroup illustGroup;
        [SerializeField] internal Image illust;                // illustration → 없으면 icon → 둘 다 없으면 비활성
        [SerializeField] internal RectTransform plate;         // 이름 플레이트 (PopIn 대상)
        [SerializeField] internal CanvasGroup plateGroup;
        [SerializeField] internal Image gradeRibbon;           // 등급 색 리본
        [SerializeField] internal TMP_Text gradeLabel;         // "영웅" / "전설" / "신화"
        [SerializeField] internal TMP_Text nameLabel;          // nameKor (초월자 이름)
        [SerializeField] internal TMP_Text skillLabel;         // skillNameKor (스킬 이름)
        [SerializeField] internal Image flash;                 // 마무리 섬광
    }
}
