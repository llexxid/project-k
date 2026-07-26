using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace KingdomIdle.UGUI
{
    /// <summary>
    /// 왕국군 종합(캐릭터) 시트. 프리팹: Panel_KACharacterSheet.prefab
    /// 스탯 블록(초상화+HP바+ATK/이동 칩) = 버튼 → 탭하면 상세 스탯 방정식이 롤다운.
    /// 스킬은 스탯 블록 아래에 표시(별도 스킬 탭 제거). 컨트롤러가 값/방정식/스킬을 채운다.
    /// </summary>
    public sealed class KACharacterSheetView : MonoBehaviour
    {
        [Header("Portrait / Job")]
        [SerializeField] internal Image portraitInner;   // 초상화(RectMask2D 내부, 고정 스케일)
        [SerializeField] internal TMP_Text jobLabel;

        [Header("Stats block (버튼 → 상세 롤다운)")]
        [SerializeField] internal Button statsButton;
        [SerializeField] internal Image hpFill;          // HP 채움(현재/최대, 색은 비율)
        [SerializeField] internal TMP_Text hpValueLabel; // "3600 / 3600"
        [SerializeField] internal TMP_Text atkValueLabel;
        [SerializeField] internal TMP_Text moveValueLabel;
        [SerializeField] internal RectTransform expandArrow; // 펼침 시 회전

        [Header("Detail rolldown")]
        [SerializeField] internal GameObject detailRoot;     // 접힘/펼침 컨테이너
        [SerializeField] internal RectTransform atkEqRow;    // 공격력 방정식 term 컨테이너(HLayout)
        [SerializeField] internal RectTransform hpEqRow;     // 체력 방정식 term 컨테이너
        [SerializeField] internal GameObject termPopup;      // term 설명 팝업
        [SerializeField] internal TMP_Text termPopupLabel;
        [SerializeField] internal RectTransform termPopupRect;

        [Header("Skills (스탯 탭 하단)")]
        [SerializeField] internal RectTransform skillsRoot;  // 스킬 행 컨테이너

        [Header("Equipped")]
        [SerializeField] internal TMP_Text equippedLabel;
    }
}
