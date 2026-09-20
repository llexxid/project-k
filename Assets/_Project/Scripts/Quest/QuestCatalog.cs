using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace KingdomIdle.Balance
{
    /// <summary>보상 한 항목의 지급 규칙이다. 동적 골드의 0은 지급량이 아니라 계산 표식이다.</summary>
    public readonly struct QuestRewardDefinition
    {
        public readonly eCurrency Currency;
        public readonly long Amount;
        public readonly bool IsDynamicGold;

        public QuestRewardDefinition(eCurrency currency, long amount, bool isDynamicGold = false)
        {
            Currency = currency;
            Amount = amount;
            IsDynamicGold = isDynamicGold;
        }
    }

    /// <summary>
    /// 퀘스트 정의와 보상을 한 번 검증하고 ID 및 체인으로 찾는 카탈로그다.
    /// Parse는 씬·저장 파일 없이 실행하며 Unity 접근은 Instance의 Resources 로딩에만 있다.
    /// </summary>
    public sealed class QuestCatalog
    {
        private static QuestCatalog _instance;
        private readonly Dictionary<long, QuestDefinition> _byId;
        private readonly Dictionary<long, long> _predecessors;
        private readonly Dictionary<long, long> _familyRoots;
        private readonly Dictionary<int, IReadOnlyList<QuestRewardDefinition>> _rewards;

        /// <summary>등록 가능한 활성 행만 반환한다. 폐기·보류 원문은 RawJson에 남는다.</summary>
        public IReadOnlyList<QuestDefinition> Definitions { get; }
        public string Version { get; }
        public string RawJson { get; }

        public static QuestCatalog Instance
        {
            get
            {
                if (_instance != null) return _instance;
                TextAsset source = Resources.Load<TextAsset>("Balance/catalog");
                if (source == null) throw new InvalidOperationException("퀘스트 카탈로그 Balance/catalog이 없습니다.");
                return _instance = Parse(source.text);
            }
        }

        private QuestCatalog(string json, JObject source, List<QuestDefinition> definitions,
            Dictionary<int, IReadOnlyList<QuestRewardDefinition>> rewards)
        {
            RawJson = json;
            Definitions = definitions.AsReadOnly();
            _rewards = rewards;
            _byId = new Dictionary<long, QuestDefinition>();
            _predecessors = new Dictionary<long, long>();
            _familyRoots = new Dictionary<long, long>();
            foreach (QuestDefinition definition in definitions) _byId.Add(definition.QuestId, definition);
            ValidateChains();
            ValidateCompletionGoals();

            // 공백 변경은 버전 변경으로 보지 않는다. 지급 당시 규칙을 식별할 내용 해시다.
            using (SHA256 hash = SHA256.Create())
                Version = "sha256:" + BitConverter.ToString(hash.ComputeHash(
                    Encoding.UTF8.GetBytes(source.ToString(Formatting.None)))).Replace("-", "").ToLowerInvariant();
        }

        /// <summary>전체 문서를 먼저 검증하므로 일부 잘못된 정의만 조용히 누락되는 일이 없다.</summary>
        public static QuestCatalog Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) throw new InvalidDataException("퀘스트 카탈로그가 비어 있습니다.");
            JObject source = JObject.Parse(json, new JsonLoadSettings
            {
                DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error
            });
            var rewards = ReadRewards(source);
            var definitions = new List<QuestDefinition>();
            var reservedIds = new HashSet<long>();
            foreach (string section in new[] { "guide", "quests", "achievements" })
            {
                foreach (JObject row in Rows(source, section))
                {
                    long id = Integer(row, "id");
                    if (id <= 0 || !reservedIds.Add(id)) throw Error(section, "중복 또는 잘못된 ID: " + id);
                    string status = Text(row, "status");
                    // 과거 Divine/Party 등의 타입은 현재 enum에 없으므로 비활성 원문을 역파싱하지 않는다.
                    if (status == "폐기" || status == "보류") continue;
                    if (status != "활성") throw Error(id.ToString(), "알 수 없는 status: " + status);

                    eQuestCategory category = section == "guide" ? eQuestCategory.Guide :
                        section == "achievements" ? eQuestCategory.Achievement : ReadPeriodicCategory(row);
                    var definition = new QuestDefinition
                    {
                        QuestId = id,
                        Category = category,
                        Title = OptionalText(row, "title") ?? OptionalText(row, "code") ?? Text(row, "target"),
                        Description = Text(row, "target"),
                        ObjectiveType = NamedEnum<eQuestObjectiveType>(row, "type"),
                        ProgressMode = NamedEnum<eQuestProgressMode>(row, "mode"),
                        TargetId = Integer(row, "targetid"),
                        RequiredCount = PositiveInt(row, "required"),
                        RewardGroupId = PositiveInt(row, "reward"),
                        NextQuestId = row["next"] == null ? 0 : NonNegativeInt(row, "next"),
                        PresentationType = row["presentation"] == null && section != "guide"
                            ? eQuestPresentationType.None : NamedEnum<eQuestPresentationType>(row, "presentation"),
                        IsRepeatable = category == eQuestCategory.Daily || category == eQuestCategory.Weekly
                    };
                    if (!rewards.ContainsKey(definition.RewardGroupId))
                        throw Error(id.ToString(), "보상 그룹을 찾을 수 없습니다: " + definition.RewardGroupId);
                    ValidateObjective(definition);
                    definitions.Add(definition);
                }
            }
            return new QuestCatalog(json, source, definitions, rewards);
        }

        public QuestDefinition Get(long id) => _byId.TryGetValue(id, out QuestDefinition definition) ? definition : null;
        public long GetPredecessor(long id) => _predecessors.TryGetValue(id, out long previous) ? previous : 0;
        public long GetFamilyRoot(long id) => _familyRoots.TryGetValue(id, out long root) ? root : 0;

        /// <summary>없는 그룹은 빈 보상으로 성공시키지 않고 오류로 처리한다.</summary>
        public IReadOnlyList<QuestRewardDefinition> GetRewards(int groupId) =>
            _rewards.TryGetValue(groupId, out IReadOnlyList<QuestRewardDefinition> rewards)
                ? rewards : throw Error(groupId.ToString(), "보상 그룹을 찾을 수 없습니다.");

        private void ValidateChains()
        {
            foreach (QuestDefinition definition in Definitions)
            {
                if (definition.NextQuestId == 0) continue;
                QuestDefinition next = Get(definition.NextQuestId);
                if (next == null || next.Category != definition.Category || definition.IsRepeatable)
                    throw Error(definition.QuestId.ToString(), "NextQuestId는 같은 영구 카테고리의 활성 목표여야 합니다.");
                if (_predecessors.ContainsKey(next.QuestId))
                    throw Error(next.QuestId.ToString(), "선행 퀘스트가 둘 이상입니다.");
                _predecessors.Add(next.QuestId, definition.QuestId);
            }

            // 숫자 ID 대역이나 입력 행 순서 대신 명시적 Next 연결의 루트를 계열로 삼는다.
            foreach (QuestDefinition definition in Definitions)
            {
                long cursor = definition.QuestId;
                var visited = new HashSet<long>();
                while (_predecessors.TryGetValue(cursor, out long previous))
                {
                    if (!visited.Add(cursor)) throw Error(definition.QuestId.ToString(), "NextQuestId 순환이 있습니다.");
                    cursor = previous;
                }
                _familyRoots.Add(definition.QuestId, cursor);
            }
            long guideRoot = 0;
            foreach (QuestDefinition definition in Definitions)
            {
                if (definition.Category != eQuestCategory.Guide || GetPredecessor(definition.QuestId) != 0) continue;
                if (guideRoot != 0) throw Error("guide", "가이드는 하나의 연결된 체인이어야 합니다.");
                guideRoot = definition.QuestId;
            }
        }

        private static Dictionary<int, IReadOnlyList<QuestRewardDefinition>> ReadRewards(JObject source)
        {
            var result = new Dictionary<int, IReadOnlyList<QuestRewardDefinition>>();
            foreach (JObject row in Rows(source, "rewards"))
            {
                int id = PositiveInt(row, "id");
                if (result.ContainsKey(id)) throw Error("rewards", "중복 보상 그룹: " + id);
                string calculation = OptionalText(row, "calculation");
                bool dynamicGold = id == 2001;
                if (dynamicGold && (calculation != "DynamicGold" || Integer(row, "minutes") != 2 || Integer(row, "kpmCap") != 30))
                    throw Error(id.ToString(), "동적 골드는 DynamicGold, minutes=2, kpmCap=30 규칙이어야 합니다.");
                if (!dynamicGold && calculation != null)
                    throw Error(id.ToString(), "지원하지 않는 보상 계산 규칙입니다.");

                var entries = new List<QuestRewardDefinition>();
                var currencies = new HashSet<eCurrency>();
                for (int slot = 1; slot <= 2; slot++)
                {
                    string name = OptionalText(row, "currency" + slot);
                    JToken amountToken = row["amount" + slot];
                    if (name == null)
                    {
                        if (amountToken != null && amountToken.Type != JTokenType.Null && amountToken.ToString() != "")
                            throw Error(id.ToString(), "재화 없이 수량만 지정되었습니다.");
                        continue;
                    }
                    eCurrency currency = NamedEnum<eCurrency>(row, "currency" + slot);
                    long amount = Integer(row, "amount" + slot);
                    bool isDynamic = dynamicGold && currency == eCurrency.Gold;
                    if (!currencies.Add(currency) || (isDynamic ? amount != 0 : amount <= 0))
                        throw Error(id.ToString(), "재화 중복 또는 잘못된 지급 수량입니다.");
                    entries.Add(new QuestRewardDefinition(currency, amount, isDynamic));
                }
                if (entries.Count == 0 || (dynamicGold && (entries.Count != 1 || !entries[0].IsDynamicGold)))
                    throw Error(id.ToString(), "빈 보상 또는 잘못된 동적 골드 그룹입니다.");
                result.Add(id, entries.AsReadOnly());
            }
            return result;
        }

        private void ValidateCompletionGoals()
        {
            foreach (eQuestCategory category in new[] { eQuestCategory.Daily, eQuestCategory.Weekly })
            {
                int basicGoals = 0;
                QuestDefinition completion = null;
                foreach (QuestDefinition definition in Definitions)
                {
                    if (definition.Category != category) continue;
                    if (definition.ObjectiveType != eQuestObjectiveType.QuestAllClear) basicGoals++;
                    else if (completion == null) completion = definition;
                    else throw Error(category.ToString(), "완료 집계 목표는 하나만 등록할 수 있습니다.");
                }
                if (completion != null && completion.RequiredCount > basicGoals)
                    throw Error(completion.QuestId.ToString(), "필요 완료 수가 활성 기본 목표 수보다 큽니다.");
            }
        }

        private static void ValidateObjective(QuestDefinition definition)
        {
            long target = definition.TargetId;
            bool state = definition.ProgressMode == eQuestProgressMode.CurrentState;
            bool valid = target >= 0;
            switch (definition.ObjectiveType)
            {
                case eQuestObjectiveType.StageClear:
                    valid &= state && ValidStage(target, false); break;
                case eQuestObjectiveType.DungeonEnter:
                    valid &= !state && (target == 0 || ValidStage(target, true)); break;
                case eQuestObjectiveType.DungeonClear:
                    valid &= state ? ValidStage(target, true) : target == 0 || ValidStage(target, true); break;
                case eQuestObjectiveType.EquipmentObtain:
                    // 획득 이벤트는 총수만 기록한다. 등급 조건은 실제 보유 상태 판정에서만 지원한다.
                    valid &= state ? target <= 3 : target == 0; break;
                case eQuestObjectiveType.EquipmentEquip:
                    valid &= state && target <= 3; break;
                case eQuestObjectiveType.StatEnhance:
                    valid &= state && target <= 2; break;
                case eQuestObjectiveType.GachaUse:
                    valid &= !state && target <= 2; break;
                case eQuestObjectiveType.JobChange:
                    valid &= state && (target == 1 || target == 2); break;
                case eQuestObjectiveType.QuestAllClear:
                    valid &= state && definition.IsRepeatable && target == (long)definition.Category; break;
                case eQuestObjectiveType.SkillObtain:
                case eQuestObjectiveType.SkillEquip:
                case eQuestObjectiveType.SkillEnhance:
                case eQuestObjectiveType.SkillAwaken:
                case eQuestObjectiveType.LevelUp:
                case eQuestObjectiveType.PlayerLevel:
                case eQuestObjectiveType.ReincarnationLevel:
                case eQuestObjectiveType.EquipmentEnhance:
                    valid &= state && target == 0; break;
                case eQuestObjectiveType.MonsterKill:
                case eQuestObjectiveType.BossKill:
                case eQuestObjectiveType.Reincarnate:
                case eQuestObjectiveType.OfflineClaim:
                case eQuestObjectiveType.BattleTime:
                case eQuestObjectiveType.MainWaveClear:
                case eQuestObjectiveType.SkillCast:
                    valid &= !state && target == 0; break;
                default:
                    // 원래 enum 번호는 보존하지만 아직 의미가 확정되지 않은 Enhance/ItemUse는 등록하지 않는다.
                    valid = false; break;
            }
            if (definition.ProgressMode == eQuestProgressMode.EventCount && !definition.IsRepeatable) valid = false;
            if (definition.IsRepeatable && definition.ProgressMode == eQuestProgressMode.LifetimeTotal) valid = false;
            if (!valid) throw Error(definition.QuestId.ToString(), "목표 타입에 맞지 않는 TargetId 또는 ProgressMode입니다.");
        }

        private static bool ValidStage(long target, bool dungeon)
        {
            long kind = target & 0x30000000L;
            long stage = (target >> 16) & 0xFFFL;
            long wave = target & 0xFFFFL;
            if (target != (0x200000000L | kind | (stage << 16) | wave)) return false;
            // 현재 베타가 실제 제공하는 메인 33개와 던전 10개만 허용한다. enum에는 3장이 아직 없다.
            return dungeon ? (kind == 0x10000000L || kind == 0x20000000L) && stage >= 1 && stage <= 5 && wave == 1
                : kind == 0 && stage >= 1 && stage <= 3 && wave >= 1 && wave <= 11;
        }

        private static eQuestCategory ReadPeriodicCategory(JObject row)
        {
            string category = Text(row, "category");
            if (category == "일일") return eQuestCategory.Daily;
            if (category == "주간") return eQuestCategory.Weekly;
            throw Error("quests", "알 수 없는 category: " + category);
        }

        private static IEnumerable<JObject> Rows(JObject source, string section)
        {
            if (!(source[section] is JArray rows)) throw Error(section, "배열이 필요합니다.");
            foreach (JToken token in rows)
            {
                if (!(token is JObject row)) throw Error(section, "행은 JSON 객체여야 합니다.");
                yield return row;
            }
        }

        private static string OptionalText(JObject row, string key)
        {
            JToken token = row[key];
            if (token == null || token.Type == JTokenType.Null) return null;
            if (token.Type != JTokenType.String) throw Error(key, "문자열이 필요합니다.");
            string value = ((string)token).Trim();
            return value.Length == 0 ? null : value;
        }

        private static string Text(JObject row, string key) => OptionalText(row, key) ?? throw Error(key, "값이 누락되었습니다.");
        private static long Integer(JObject row, string key)
        {
            JToken token = row[key];
            if (token == null || token.Type != JTokenType.Integer || !long.TryParse(token.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out long value))
                throw Error(key, "64비트 정수가 필요합니다.");
            return value;
        }

        private static int NonNegativeInt(JObject row, string key)
        {
            long value = Integer(row, key);
            if (value < 0 || value > int.MaxValue) throw Error(key, "0 이상의 32비트 정수가 필요합니다.");
            return (int)value;
        }

        private static int PositiveInt(JObject row, string key)
        {
            int value = NonNegativeInt(row, key);
            return value > 0 ? value : throw Error(key, "양수가 필요합니다.");
        }

        private static T NamedEnum<T>(JObject row, string key) where T : struct, Enum
        {
            string name = Text(row, key);
            if (!Enum.TryParse(name, false, out T value) || !Enum.IsDefined(typeof(T), value) || value.ToString() != name)
                throw Error(key, "정의되지 않은 enum 이름: " + name);
            return value;
        }

        private static InvalidDataException Error(string location, string message) =>
            new InvalidDataException("퀘스트 카탈로그 [" + location + "] " + message);
    }
}
