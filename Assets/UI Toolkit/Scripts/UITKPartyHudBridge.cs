using System.Collections.Generic;
using UnityEngine;

namespace KingdomIdle.UIToolkit
{
    /// <summary>
    /// 파티 HUD 외부 호출용 브릿지
    /// </summary>
    public static class UITKPartyHudBridge
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoEnsure()
        {
            Ensure();
        }

        public static UITKPartyHudController Ensure()
        {
            if (UITKPartyHudController.Instance != null)
                return UITKPartyHudController.Instance;

            if (UITKUIManager.Instance != null)
            {
                var go = UITKUIManager.Instance.gameObject;
                var c = go.GetComponent<UITKPartyHudController>();
                if (c == null) c = go.AddComponent<UITKPartyHudController>();
                return c;
            }

            // 없으면 임시 오브젝트 생성(씬 전환해도 유지)
            var obj = new GameObject("UITKPartyHudController");
            Object.DontDestroyOnLoad(obj);
            return obj.AddComponent<UITKPartyHudController>();
        }

        public static void SetMemberHealth(int memberIndex, float current, float max)
        {
            Ensure()?.SetMemberHealth(memberIndex, current, max);
        }

        public static void SetMemberSkillCount(int memberIndex, int count)
        {
            Ensure()?.SetMemberSkillCount(memberIndex, count);
        }

        public static void SetMemberSkillIds(int memberIndex, IReadOnlyList<int> skillIds)
        {
            Ensure()?.SetMemberSkillIds(memberIndex, skillIds);
        }

        public static void NotifySkillUsedBySlotIndex(int memberIndex, int slotIndex, float cooldownSeconds)
        {
            Ensure()?.NotifySkillUsedBySlotIndex(memberIndex, slotIndex, cooldownSeconds);
        }

        public static void NotifySkillUsedBySkillId(int memberIndex, int skillId, float cooldownSeconds)
        {
            Ensure()?.NotifySkillUsedBySkillId(memberIndex, skillId, cooldownSeconds);
        }
    }
}