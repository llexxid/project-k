using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace KingdomIdle.MageTower
{
    [CreateAssetMenu(menuName = "KingdomIdle/MageTower/Skill", fileName = "MageTowerSkill_New")]
    public class MageTowerSkillSO : ScriptableObject
    {
        public int id;
        public string nameEng;
        [FormerlySerializedAs("skillName")]
        public string nameKor;
        public Sprite icon;
        public float baseCooldown;
        public int maxEnhanceLevel = 100;
        public int maxAwakeningLevel = 10;
        public GameObject prefab;

        [SerializeField]
        private List<SkillEffect> effects = new List<SkillEffect>();

        /// <summary>
        /// 첫 번째로 매칭되는 T 타입 이펙트를 반환한다. 없으면 null.
        /// 리스트가 1~3개로 작으므로 선형 순회로 충분.
        /// </summary>
        public T GetEffect<T>() where T : SkillEffect
        {
            for (int i = 0; i < effects.Count; i++)
            {
                if (effects[i] is T typed)
                    return typed;
            }
            return null;
        }

        /// <summary>
        /// DamageEffect의 baseDamage를 반환. 하위 호환용 프로퍼티.
        /// </summary>
        public float BaseDamage
        {
            get
            {
                var dmg = GetEffect<DamageEffect>();
                return dmg != null ? dmg.baseDamage : 0f;
            }
        }
    }
}
