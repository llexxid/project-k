using System.Collections.Generic;
using UnityEngine;

namespace KingdomIdle.Divine
{
    /// <summary>보유 가능한 신 스킬 카드 전체 목록. DivineSkillManager 가 참조한다.</summary>
    [CreateAssetMenu(menuName = "KingdomIdle/Divine/Skill Registry", fileName = "DivineSkillRegistry")]
    public class DivineSkillRegistrySO : ScriptableObject
    {
        public List<DivineSkillSO> cards = new List<DivineSkillSO>();

        public DivineSkillSO GetById(int id)
        {
            for (int i = 0; i < cards.Count; i++)
            {
                if (cards[i] != null && cards[i].id == id)
                    return cards[i];
            }
            return null;
        }
    }
}
