using System;
using System.Linq;

namespace KingdomIdle.Balance
{
    /// <summary>저장이나 싱글턴 접근 없이 정의와 승인된 상태로 목표 진행값을 계산한다.</summary>
    public static class QuestProgressEvaluator
    {
        /// <summary>기존 저장의 enum 정수 기반 카운터 키를 그대로 만든다.</summary>
        public static string CounterKey(string period, eQuestObjectiveType type, long target)
            => $"{period}|{(int)type}|{target}";

        /// <summary>기록되지 않은 목표는 0으로 조회한다. 조회는 상태를 변경하지 않는다.</summary>
        public static long Read(ProgressionState state, string period, eQuestObjectiveType type, long target)
            => state.Counters.TryGetValue(CounterKey(period, type, target), out long value) ? value : 0;

        /// <summary>완료 보존과 단계 잠금을 적용하기 전의 실제 목표값을 반환한다.</summary>
        public static long Evaluate(QuestDefinition quest, ProgressionState state, QuestCatalog catalog,
            Func<int, int> equipmentRarity)
        {
            // 잠겨 있어도 카운터는 수집하지만 기간 목표의 달성 권리는 해금 후 생긴다.
            if (quest.IsRepeatable && !state.MainClears.Contains(0x20001000B)) return 0;
            if (quest.ProgressMode != eQuestProgressMode.CurrentState)
            {
                if (quest.ProgressMode == eQuestProgressMode.LifetimeTotal && quest.TargetId == 0)
                {
                    if (quest.ObjectiveType == eQuestObjectiveType.MonsterKill) return state.Kills;
                    if (quest.ObjectiveType == eQuestObjectiveType.Reincarnate) return state.ReincarnationCount;
                }
                string period = quest.ProgressMode == eQuestProgressMode.LifetimeTotal ? "L"
                    : quest.Category == eQuestCategory.Daily ? "D" + state.QuestDay : "W" + state.QuestWeek;
                return Read(state, period, quest.ObjectiveType, quest.TargetId);
            }

            // 가이드의 현재값과 업적의 역대 최고 합계를 의도적으로 구분한다.
            bool best = quest.Category == eQuestCategory.Achievement;
            bool Matches(EquipmentSave item) => quest.TargetId == 0 ||
                (equipmentRarity != null && equipmentRarity(item.Code) >= quest.TargetId);
            switch (quest.ObjectiveType)
            {
                case eQuestObjectiveType.StageClear: return state.MainClears.Contains(quest.TargetId) ? 1 : 0;
                case eQuestObjectiveType.LevelUp:
                case eQuestObjectiveType.PlayerLevel: return state.AccountLevel;
                case eQuestObjectiveType.StatEnhance:
                    return quest.TargetId == 1 ? state.AttackLevel : quest.TargetId == 2 ? state.HealthLevel
                        : best ? state.BestStatTotal : state.AttackLevel + state.HealthLevel;
                case eQuestObjectiveType.EquipmentObtain: return state.Equipment.Count(Matches);
                case eQuestObjectiveType.EquipmentEquip:
                    return state.Equipment.Where(x => x.Player.HasValue && Matches(x)).Select(x => x.Player.Value).Distinct().Count();
                case eQuestObjectiveType.EquipmentEnhance: return best ? state.BestEquipmentTotal : state.Equipment.Sum(x => x.Level);
                case eQuestObjectiveType.JobChange:
                    return state.Jobs.Values.Count(x => !string.IsNullOrEmpty(x) &&
                        (quest.TargetId == 2 ? x.StartsWith("Elite_", StringComparison.Ordinal) : x != "Spearman"));
                case eQuestObjectiveType.SkillObtain: return state.MageSkills.Count;
                case eQuestObjectiveType.SkillEquip: return state.MageSlots.Where(x => x >= 0).Distinct().Count();
                case eQuestObjectiveType.SkillEnhance: return best ? state.BestMageTotal : state.MageSkills.Values.Sum(x => x.Enhance);
                case eQuestObjectiveType.SkillAwaken: return state.MageSkills.Count == 0 ? 0 : state.MageSkills.Values.Max(x => x.Awaken);
                case eQuestObjectiveType.ReincarnationLevel: return state.ReincarnationLevel;
                case eQuestObjectiveType.DungeonClear:
                    int cleared = (quest.TargetId & 0x30000000) == 0x10000000 ? state.GoldDungeonClear : state.RubyDungeonClear;
                    return cleared >= ((quest.TargetId >> 16) & 0xFFF) ? 1 : 0;
                case eQuestObjectiveType.QuestAllClear:
                    // 자기 자신과 비활성 정의를 제외하고 달성/수령을 같은 한 건으로 센다.
                    return catalog.Definitions.Count(x => x.Category == quest.Category && x.ObjectiveType != eQuestObjectiveType.QuestAllClear &&
                        (state.Claims.Contains(QuestEconomy.Key(x, state)) || state.PendingQuests.ContainsKey(QuestEconomy.Key(x, state)) ||
                         Evaluate(x, state, catalog, equipmentRarity) >= x.RequiredCount));
                default: return 0;
            }
        }
    }
}
