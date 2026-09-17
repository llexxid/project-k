using System;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace KingdomIdle.Balance
{
    public static class QuestEconomy
    {
        private static List<QuestDefinition> _definitions;
        private static Dictionary<int, JObject> _rewards;
        public static IReadOnlyList<QuestDefinition> Definitions { get { Load(); return _definitions; } }
        private static void Load()
        {
            if (_definitions != null) return;
            var source = Resources.Load<TextAsset>("Balance/catalog");
            if (source == null) throw new InvalidOperationException("Balance quest catalog missing.");
            var json = JObject.Parse(source.text); var definitions = new List<QuestDefinition>();
            foreach (string section in new[] { "guide", "quests", "achievements" })
                foreach (JObject row in json[section])
                {
                    if ((string)row["status"] != "활성") continue;
                    var q = new QuestDefinition { QuestId = (long)row["id"], Title = (string)row["title"] ?? (string)row["code"], Description = (string)row["target"],
                        ObjectiveType = Enum.Parse<eQuestObjectiveType>((string)row["type"]), ProgressMode = Enum.Parse<eQuestProgressMode>((string)row["mode"]),
                        TargetId = (long)row["targetid"], RequiredCount = (int)row["required"], RewardGroupId = (int)row["reward"], NextQuestId = (int?)row["next"] ?? 0,
                        Category = section == "guide" ? eQuestCategory.Guide : section == "achievements" ? eQuestCategory.Achievement : (string)row["category"] == "일일" ? eQuestCategory.Daily : eQuestCategory.Weekly };
                    q.IsRepeatable = q.Category == eQuestCategory.Daily || q.Category == eQuestCategory.Weekly;
                    if (q.RequiredCount <= 0 || (q.ObjectiveType == eQuestObjectiveType.StageClear && q.TargetId == 0)) throw new InvalidOperationException("Invalid quest " + q.QuestId);
                    definitions.Add(q);
                }
            _rewards = json["rewards"].Cast<JObject>().ToDictionary(x => (int)x["id"]);
            if (definitions.Select(x => x.QuestId).Distinct().Count() != definitions.Count) throw new InvalidOperationException("Duplicate quest id.");
            _definitions = definitions;
        }
        public static string Week
        {
            get { var date = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(9)).Date; return date.AddDays(-((int)date.DayOfWeek + 6) % 7).ToString("yyyy-MM-dd"); }
        }
        public static string Key(QuestDefinition q, ProgressionState s) => $"quest:{q.QuestId}:" + (q.Category == eQuestCategory.Daily ? s.QuestDay : q.Category == eQuestCategory.Weekly ? s.QuestWeek : "permanent");
        private static string CounterKey(string period, eQuestObjectiveType type, long target) => $"{period}|{(int)type}|{target}";
        public static void Count(ProgressionState s, eQuestObjectiveType type, long target, long amount)
        {
            if (amount <= 0) return;
            foreach (string period in new[] { "L", "D" + s.QuestDay, "W" + s.QuestWeek })
            {
                string key = CounterKey(period, type, 0); s.Counters[key] = checked(Read(s, key) + amount);
                if (target != 0) { key = CounterKey(period, type, target); s.Counters[key] = checked(Read(s, key) + amount); }
            }
        }
        private static long Read(ProgressionState s, string key) => s.Counters.TryGetValue(key, out long value) ? value : 0;
        public static void Before(ProgressionState s)
        {
            string day = LocalProgression.KstDay, week = Week;
            if (s.QuestDay == day && s.QuestWeek == week) return;
            // Eligibility is captured during each approved transaction, before its period closes.
            s.QuestDay = day; s.QuestWeek = week;
            foreach (var key in s.Counters.Keys.Where(k => !k.StartsWith("L|") && !k.StartsWith("D" + day + "|") && !k.StartsWith("W" + week + "|")).ToArray()) s.Counters.Remove(key);
            foreach (var key in s.PendingQuests.Where(p => p.Value.ExpiresUtc < LocalProgression.UtcNow).Select(p => p.Key).ToArray()) s.PendingQuests.Remove(key);
        }
        public static void After(ProgressionState s)
        {
            s.BestStatTotal = Math.Max(s.BestStatTotal, s.AttackLevel + s.HealthLevel);
            s.BestMageTotal = Math.Max(s.BestMageTotal, s.MageSkills.Values.Sum(x => x.Enhance));
            s.BestEquipmentTotal = Math.Max(s.BestEquipmentTotal, s.Equipment.Sum(x => x.Level));
            if (!s.MainClears.Contains(0x20001000B)) return;
            foreach (var q in Definitions.Where(x => x.IsRepeatable))
            {
                string key = Key(q, s);
                if (s.Claims.Contains(key) || s.PendingQuests.ContainsKey(key) || Progress(q, s) < q.RequiredCount) continue;
                string date = q.Category == eQuestCategory.Daily ? s.QuestDay : s.QuestWeek;
                long end = new DateTimeOffset(DateTime.ParseExact(date, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture), TimeSpan.FromHours(9)).AddDays(q.Category == eQuestCategory.Daily ? 1 : 7).ToUnixTimeSeconds();
                s.PendingQuests[key] = new QuestPending { Id = q.QuestId, ExpiresUtc = end + 7 * 86400, Gold = q.RewardGroupId == 2001 ? DynamicGold(s) : 0 };
            }
        }
        private static bool Rarity(EquipmentSave e, long rarity) => rarity == 0 || (int)(EquipmentManager.Instance?.GetData(e.Code)?.rarity ?? eEquipmentRarity.Normal) + 1 == rarity;
        public static long Progress(QuestDefinition q, ProgressionState s)
        {
            if (q.IsRepeatable && !s.MainClears.Contains(0x20001000B)) return 0;
            if (q.ProgressMode == eQuestProgressMode.EventCount || q.ProgressMode == eQuestProgressMode.LifetimeTotal)
            {
                if (q.ProgressMode == eQuestProgressMode.LifetimeTotal && q.ObjectiveType == eQuestObjectiveType.MonsterKill) return s.Kills;
                if (q.ProgressMode == eQuestProgressMode.LifetimeTotal && q.ObjectiveType == eQuestObjectiveType.Reincarnate) return s.ReincarnationCount;
                string period = q.ProgressMode == eQuestProgressMode.LifetimeTotal ? "L" : q.Category == eQuestCategory.Daily ? "D" + s.QuestDay : "W" + s.QuestWeek;
                return Read(s, CounterKey(period, q.ObjectiveType, q.TargetId));
            }
            bool best = q.Category == eQuestCategory.Achievement;
            switch(q.ObjectiveType)
            {
                case eQuestObjectiveType.StageClear: return s.MainClears.Contains(q.TargetId) ? 1 : 0;
                case eQuestObjectiveType.LevelUp: case eQuestObjectiveType.PlayerLevel: return s.AccountLevel;
                case eQuestObjectiveType.StatEnhance: return q.TargetId == 1 ? s.AttackLevel : q.TargetId == 2 ? s.HealthLevel : best ? s.BestStatTotal : s.AttackLevel + s.HealthLevel;
                case eQuestObjectiveType.EquipmentObtain: return s.Equipment.Count(x => Rarity(x, q.TargetId));
                case eQuestObjectiveType.EquipmentEquip: return s.Equipment.Count(x => x.Player.HasValue && Rarity(x, q.TargetId));
                case eQuestObjectiveType.EquipmentEnhance: return best ? s.BestEquipmentTotal : s.Equipment.Sum(x => x.Level);
                case eQuestObjectiveType.JobChange: return s.Jobs.Values.Count(x => q.TargetId == 2 ? x.StartsWith("Elite_") : x != "Spearman");
                case eQuestObjectiveType.SkillObtain: return s.MageSkills.Count;
                case eQuestObjectiveType.SkillEquip: return s.MageSlots.Where(x => x >= 0).Distinct().Count();
                case eQuestObjectiveType.SkillEnhance: return best ? s.BestMageTotal : s.MageSkills.Values.Sum(x => x.Enhance);
                case eQuestObjectiveType.SkillAwaken: return s.MageSkills.Count == 0 ? 0 : s.MageSkills.Values.Max(x => x.Awaken);
                case eQuestObjectiveType.ReincarnationLevel: return s.ReincarnationLevel;
                case eQuestObjectiveType.DungeonClear: return (((q.TargetId & 0x30000000) == 0x10000000 ? s.GoldDungeonClear : s.RubyDungeonClear) >= ((q.TargetId >> 16) & 0xFFF)) ? 1 : 0;
                case eQuestObjectiveType.QuestAllClear: return Definitions.Count(x => x.Category == q.Category && x.ObjectiveType != eQuestObjectiveType.QuestAllClear && (s.Claims.Contains(Key(x,s)) || s.PendingQuests.ContainsKey(Key(x,s)) || Progress(x,s) >= x.RequiredCount));
                default: return 0;
            }
        }
        public static decimal DynamicGold(ProgressionState s)
        {
            int stage = (int)((s.OfflineStage >> 16) & 0xFFF), wave = (int)(s.OfflineStage & 0xFFFF);
            if (stage < 1 || stage > 3 || wave < 1 || wave > 10) return 60; // Explicit 1-1, 3 KPM bootstrap.
            return 2m * Math.Min(30m,s.OfflineKpm) * Scripts.Core.StageCatalogRules.MainEnemy(stage,wave).Gold * BalanceMath.RubyMultiplier(s.RubyGoldLevel);
        }
        public static bool CanClaim(QuestDefinition q, ProgressionState s) => q != null && !s.Claims.Contains(Key(q,s)) && (s.PendingQuests.ContainsKey(Key(q,s)) || Progress(q,s) >= q.RequiredCount);
        public static bool Claim(long id, string pendingKey = null)
        {
            var q = Definitions.FirstOrDefault(x => x.QuestId == id); if (q == null) return false;
            bool ok = LocalProgression.Execute("quest-claim", s => {
                string key = pendingKey ?? Key(q,s);
                bool pending = s.PendingQuests.TryGetValue(key, out var reward);
                if (s.Claims.Contains(key) || (pendingKey != null && (!pending || reward.Id != id || reward.ExpiresUtc < LocalProgression.UtcNow))) return false;
                if (pendingKey == null && !CanClaim(q,s)) return false;
                if (q.Category == eQuestCategory.Guide && Definitions.FirstOrDefault(x => x.Category == eQuestCategory.Guide && !s.Claims.Contains(Key(x,s)))?.QuestId != id) return false;
                JObject group = _rewards[q.RewardGroupId];
                for (int i = 1; i <= 2; i++)
                {
                    string name = (string)group["currency" + i]; if (string.IsNullOrEmpty(name)) continue;
                    long amount = (long)group["amount" + i];
                    if (q.RewardGroupId == 2001 && name == "Gold")
                    { decimal value = (pending ? reward.Gold : DynamicGold(s)) + s.GoldRemainder/1000000m; amount = BalanceMath.Floor(value); s.GoldRemainder = (long)((value-amount)*1000000m); }
                    LocalProgression.Credit(s, Enum.Parse<eCurrency>(name), amount);
                }
                s.Claims.Add(key); s.PendingQuests.Remove(key); return true;
            });
            return ok;
        }
        public static string RewardText(int groupId, Func<long, string> format = null)
        {
            format ??= value => value.ToString("N0", System.Globalization.CultureInfo.InvariantCulture);
            Load(); if (groupId == 2001) return "안전 사냥 2분 골드";
            var group = _rewards[groupId]; var parts = new List<string>();
            for(int i=1;i<=2;i++) { string c = (string)group["currency"+i]; if (string.IsNullOrEmpty(c)) continue; string name = c == "AncientCoin" ? "주화" : c == "ClassFragment" ? "전직 파편" : c == "ArcaneKnowledge" ? "마법 지식" : c; parts.Add($"{name} {format((long)group["amount"+i])}"); }
            return string.Join(" · ",parts);
        }
    }
}
