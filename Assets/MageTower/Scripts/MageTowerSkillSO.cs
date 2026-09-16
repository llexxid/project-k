using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace KingdomIdle.MageTower
{
    public enum MageSpellKind { Lightning = 0, IceSpike = 1, FireTornado = 2, ArcaneVolley = 3, VenomMist = 4, StoneSeal = 5, GaleBlades = 6, Sanctuary = 7, Meteor = 8, VoidRift = 9 }
    [CreateAssetMenu(menuName = "KingdomIdle/MageTower/Skill", fileName = "MageTowerSkill_New")]
    public class MageTowerSkillSO : ScriptableObject
    {
        public int id;
        public string nameEng;
        [FormerlySerializedAs("skillName")]
        public string nameKor;
        public Sprite icon;
        public Sprite bloomIcon;
        public Sprite DisplayIcon(bool bloom) => bloom && bloomIcon != null ? bloomIcon : icon;
        public float baseCooldown;
        public int maxEnhanceLevel = 100;
        public int maxAwakeningLevel = 10;
        public GameObject prefab;

        [Header("Catalog")]
        public MageSpellKind spellKind;
        [TextArea(2, 5)] public string description;
        [Min(0)] public float basePower;
        [Min(1)] public int baseHits = 3;
        [Min(.05f)] public float radius = .5f;
        [Min(.05f)] public float duration = 1f;
        [Min(.05f)] public float tickInterval = .5f;
        [Min(0)] public float controlDuration;
        [Range(0, .8f)] public float slowFraction;
        [Min(0)] public float secondaryPowerRatio;
        [Min(1)] public int maxTargets = 6;
        public GameObject castingPrefab;
        public GameObject secondaryPrefab;

        [Header("Bloom · Awakening 10")]
        public string bloomName = "미정";
        [TextArea(2, 5)] public string bloomDescription;
        [Min(1)] public float bloomCooldownMultiplier = 1f;
        [Min(1)] public float bloomPowerMultiplier = 1.15f;
        [Min(0)] public float bloomAreaPowerMultiplier = .5f;
        [Min(0)] public float bloomControlDuration = 2f;
        [Min(1)] public int bloomMaxTargets = 10;
        public GameObject bloomPrefab;
        public GameObject bloomCastingPrefab;
        public bool IsHealing => spellKind == MageSpellKind.Sanctuary;

        [Header("SFX")]
        [Tooltip("스킬 발동 시 1회 재생되는 SFX 이름 (eSFXType 항목과 정확히 일치해야 함)")]
        public string sfxName;
        [Tooltip("스킬 지속 동안 루프 재생되는 SFX 이름 (지속형 스킬 전용)")]
        public string sfxLoopName;

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
                if (basePower > 0) return basePower;
                var dmg = GetEffect<DamageEffect>();
                return dmg != null ? dmg.baseDamage : 0f;
            }
        }
    }
}
