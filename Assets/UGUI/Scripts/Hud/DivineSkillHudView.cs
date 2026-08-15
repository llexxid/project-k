using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace KingdomIdle.UGUI
{
    /// <summary>
    /// 신성 스킬(궁극기) HUD 셸: 좌하단 대형 버튼 1개.
    /// 방사형 쿨다운 + 남은 초 + 시전 가능 후광 + 등급색 테두리를 담는 직렬화 참조만 가진다(로직 없음).
    /// </summary>
    public sealed class DivineSkillHudView : MonoBehaviour
    {
        [SerializeField] internal Button button;
        [SerializeField] internal Image frame;            // 버튼 본체 (시전 중 하이라이트 대상)
        [SerializeField] internal Image gradeBorder;      // 등급 색 테두리
        [SerializeField] internal Image icon;             // 장착 카드 아이콘 (스프라이트 없으면 꺼둔다)
        [SerializeField] internal TMP_Text emptyLabel;    // 미장착 / 아이콘 없음 표기
        [SerializeField] internal Image cooldownFill;     // 방사형 쿨다운 (Filled/Radial360)
        [SerializeField] internal TMP_Text cooldownText;  // 남은 초
        [SerializeField] internal Image readyGlow;        // 시전 가능 시 맥동하는 후광
        [SerializeField] internal CanvasGroup readyGlowGroup;
        [SerializeField] internal UIPulseGroup pulse;     // readyGlowGroup 맥동 구동 (루트에 부착)
    }
}
