using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace KingdomIdle.UGUI
{
    /// <summary>
    /// 스킬 행 (왕국군 스킬 / 전직 상세 스킬 목록). 타입 배지 + 이름 + 설명.
    /// 프리팹: Item_SkillRow.prefab
    /// </summary>
    public sealed class SkillRowView : MonoBehaviour
    {
        [SerializeField] internal Image typeBadge;
        [SerializeField] internal TMP_Text typeLabel;
        [SerializeField] internal TMP_Text nameLabel;
        [SerializeField] internal TMP_Text detailLabel;

        private static readonly Color ActiveColor = new Color(80f / 255f, 140f / 255f, 220f / 255f, 0.85f);
        private static readonly Color PassiveColor = new Color(160f / 255f, 100f / 255f, 200f / 255f, 0.85f);

        public void Set(string name, string detail, bool isPassive)
        {
            if (typeLabel != null) typeLabel.text = isPassive ? "패시브" : "액티브";
            if (typeBadge != null) typeBadge.color = isPassive ? PassiveColor : ActiveColor;
            if (nameLabel != null) nameLabel.text = name;
            if (detailLabel != null) detailLabel.text = detail;
        }
    }
}
