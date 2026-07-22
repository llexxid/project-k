using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace KingdomIdle.UGUI
{
    /// <summary>
    /// 육성 강화 카드 (공격력/체력 등). 헤더(이름·레벨·효과) + 버튼 행.
    /// 버튼(x1/x10)은 컨트롤러가 buttonRow 에 Item_ActionButton 등을 넣는다.
    /// 프리팹: Item_EnhanceCard.prefab
    /// </summary>
    public sealed class EnhanceCardView : MonoBehaviour
    {
        [SerializeField] internal Image accent;       // 좌측 액센트 바
        [SerializeField] internal TMP_Text nameLabel;
        [SerializeField] internal TMP_Text levelLabel;
        [SerializeField] internal TMP_Text bonusLabel;
        [SerializeField] internal RectTransform buttonRow;

        public RectTransform ButtonRow => buttonRow;

        public void Set(string name, string level, string bonus, Color? accentColor = null)
        {
            if (nameLabel != null) nameLabel.text = name;
            if (levelLabel != null) levelLabel.text = level;
            if (bonusLabel != null) bonusLabel.text = bonus;
            if (accent != null && accentColor.HasValue) accent.color = accentColor.Value;
        }
    }
}
