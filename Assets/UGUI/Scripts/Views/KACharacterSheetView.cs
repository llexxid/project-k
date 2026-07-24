using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace KingdomIdle.UGUI
{
    /// <summary>
    /// 왕국군 종합(캐릭터) 시트. 프리팹: Panel_KACharacterSheet.prefab
    /// 초상화(RectMask2D + Inner Image) + 스탯 라벨 + 장착 장비 라벨.
    /// 컨트롤러가 값을 채우고 initial idle 스프라이트로 고정 스케일을 잡는다.
    /// </summary>
    public sealed class KACharacterSheetView : MonoBehaviour
    {
        [SerializeField] internal Image portraitInner;   // 초상화 내부 스프라이트 (RectMask2D로 클리핑)
        [SerializeField] internal TMP_Text jobLabel;
        [SerializeField] internal TMP_Text hpLabel;       // 200ms 실시간 갱신 대상
        [SerializeField] internal TMP_Text atkLabel;
        [SerializeField] internal TMP_Text moveLabel;
        [SerializeField] internal TMP_Text equippedLabel; // 장착 장비 라벨 (없으면 placeholder 스타일)
    }
}
