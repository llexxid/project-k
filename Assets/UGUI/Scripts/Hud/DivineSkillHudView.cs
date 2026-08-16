using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace KingdomIdle.UGUI
{
    /// <summary>
    /// 신성 스킬(궁극기) HUD 셸: 하단 중앙(파티 HUD 위) 원형 대형 버튼 1개.
    /// 청동 링 + 등급색 링 + 원형 크롭 아이콘 + 방사형 쿨다운 + 준비 후광 +
    /// AUTO 회전 링/필 + 시전 플래시를 담는 직렬화 참조만 가진다(로직 없음).
    /// </summary>
    public sealed class DivineSkillHudView : MonoBehaviour
    {
        [SerializeField] internal Button button;            // 시각(눌림 틴트)·SFX 용 — onClick에 시전을 달지 않는다
        [SerializeField] internal UILongPressButton longPress; // 탭=수동 시전 / 길게=자동 시전 토글
        [SerializeField] internal Image frame;              // 원형 버튼 본체 = 청동 외곽 링 (눌림 스케일 대상)
        [SerializeField] internal Image gradeBorder;        // 등급 색 얇은 링 (외곽 링과 디스크 사이)
        [SerializeField] internal Image disc;               // 어두운 원형 디스크 — Mask 겸용(아이콘 원형 크롭)
        [SerializeField] internal Image icon;               // 장착 카드 아이콘 (스프라이트 없으면 꺼둔다)
        [SerializeField] internal TMP_Text emptyLabel;      // 미장착 / 아이콘 없음 표기
        [SerializeField] internal Image cooldownFill;       // 방사형 쿨다운 (Filled/Radial360)
        [SerializeField] internal TMP_Text cooldownText;    // 남은 초
        [SerializeField] internal Image readyGlow;          // 시전 가능 시 맥동+호흡하는 후광
        [SerializeField] internal CanvasGroup readyGlowGroup;
        [SerializeField] internal UIPulseGroup pulse;       // readyGlowGroup 맥동 구동 (루트에 부착)
        [SerializeField] internal RectTransform autoRing;   // 자동 시전 ON 골드 회전 링 (틱 4개)
        [SerializeField] internal GameObject autoPill;      // 링 하단 "AUTO" 필 (회전하지 않음)
        [SerializeField] internal Image castFlash;          // 시전 발동 플래시 (캐시 1개 재사용)
    }
}
