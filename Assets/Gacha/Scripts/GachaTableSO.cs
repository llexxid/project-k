using System;
using System.Collections.Generic;
using UnityEngine;
using Scripts.Wallets;

namespace KingdomIdle.Gacha
{
    public enum eGachaRewardType
    {
        Currency,
        Skill,
        Equipment
    }

    [Serializable]
    public class GachaRewardEntry
    {
        public string nameKor;
        public Sprite icon;
        public eGachaRewardType rewardType;

        [Header("Currency 보상")]
        public eCurrency currency;
        public int amount;

        [Header("Skill 보상")]
        public int skillId;

        [Header("Equipment 보상")]
        [Tooltip("rewardType이 Equipment일 때 뽑기로 획득할 장비 데이터")]
        public EquipmentData equipmentData;

        [Min(0f)] public float weight;
    }

    [CreateAssetMenu(menuName = "KingdomIdle/Gacha/Table", fileName = "GachaTable_New")]
    public class GachaTableSO : ScriptableObject
    {
        public string nameKor;
        public string nameEng;
        [TextArea(2, 4)]
        public string description;
        public eCurrency costCurrency;
        public int costAmount;
        public bool isImplemented;
        public List<GachaRewardEntry> rewards = new List<GachaRewardEntry>();

        /// <summary>
        /// 가중 랜덤으로 보상 1개를 뽑는다.
        /// 서버 마이그레이션 시 이 메서드를 서버 API 응답으로 교체.
        /// </summary>
        public GachaRewardEntry Roll()
        {
            if (rewards == null || rewards.Count == 0) return null;

            float total = 0f;
            for (int i = 0; i < rewards.Count; i++)
                total += rewards[i].weight;

            if (total <= 0f) return rewards[0];

            float rand = UnityEngine.Random.Range(0f, total);
            float acc = 0f;
            for (int i = 0; i < rewards.Count; i++)
            {
                acc += rewards[i].weight;
                if (rand <= acc) return rewards[i];
            }
            return rewards[rewards.Count - 1];
        }
    }
}
