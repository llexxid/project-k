using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Scripts.Core;

namespace KingdomIdle.Balance
{
    /// <summary>기존 게임플레이 진입점을 유지하며 퀘스트 변경을 경제 거래 안에서 조율한다.</summary>
    public static class QuestEconomy
    {
        /// <summary>퀘스트 하위 저장 버전. 전체 경제 스키마와 독립적으로 이관한다.</summary>
        public const int SchemaVersion = 1;
        /// <summary>런타임에서 사용하는 단일 카탈로그의 활성 정의다.</summary>
        public static IReadOnlyList<QuestDefinition> Definitions => QuestCatalog.Instance.Definitions;
        /// <summary>기존 호출 호환용 주간 키. 거래 내부에서는 확보한 시각을 인자로 전달한다.</summary>
        public static string Week => QuestPeriod.At(LocalProgression.UtcNow).Week;

        /// <summary>기존 지급 원장의 식별자 형식을 보존한다.</summary>
        public static string Key(QuestDefinition quest, ProgressionState state) => Key(quest.QuestId, Period(quest, state));
        public static string Key(long id, string period) => $"quest:{id}:{period}";
        public static string Period(QuestDefinition quest, ProgressionState state) => quest.Category == eQuestCategory.Daily
            ? state.QuestDay : quest.Category == eQuestCategory.Weekly ? state.QuestWeek : "permanent";

        /// <summary>승인된 거래 복제본의 영구/일일/주간 집계를 함께 변경한다.</summary>
        public static void Count(ProgressionState state, eQuestObjectiveType type, long target, long amount)
        {
            if (amount <= 0) return;
            foreach (string period in new[] { "L", "D" + state.QuestDay, "W" + state.QuestWeek })
            {
                AddCounter(state, period, type, 0, amount);
                if (target != 0) AddCounter(state, period, type, target, amount);
            }
        }

        /// <summary>최대값에서 조용히 순환하지 않도록 checked로 누적한다.</summary>
        private static void AddCounter(ProgressionState state, string period, eQuestObjectiveType type, long target, long amount)
        {
            string key = QuestProgressEvaluator.CounterKey(period, type, target);
            state.Counters[key] = checked(QuestProgressEvaluator.Read(state, period, type, target) + amount);
        }

        /// <summary>기존 저장에 없는 완료/보상 정보를 확인 가능한 자료로만 채운다.</summary>
        public static bool Migrate(ProgressionState state)
        {
            if (state.QuestSchemaVersion < 0 || state.QuestSchemaVersion > SchemaVersion)
                throw new InvalidDataException("Unsupported quest schema.");
            if (state.QuestSchemaVersion == SchemaVersion) return false;
            // 최신 버전의 명시적 null은 손상으로 검증에서 거절한다. legacy에 없는 필드만 보완한다.
            state.CompletedQuests ??= new HashSet<long>();
            // 이전 버전에는 watermark가 없으므로 저장된 기간의 시작보다 과거로 되돌리지 않는다.
            if (!string.IsNullOrEmpty(state.QuestDay)) state.QuestLastObservedUtc = Math.Max(state.QuestLastObservedUtc,
                QuestPeriod.EndUtc(eQuestCategory.Daily, state.QuestDay) - 86400);
            if (!string.IsNullOrEmpty(state.QuestWeek)) state.QuestLastObservedUtc = Math.Max(state.QuestLastObservedUtc,
                QuestPeriod.EndUtc(eQuestCategory.Weekly, state.QuestWeek) - 7 * 86400L);
            foreach (var quest in Definitions.Where(x => !x.IsRepeatable))
                if (state.Claims.Contains(Key(quest, state))) state.CompletedQuests.Add(quest.QuestId);
            foreach (var pair in state.PendingQuests)
            {
                QuestDefinition quest = QuestCatalog.Instance.Get(pair.Value.Id);
                if (quest == null) throw new InvalidDataException("Unresolved legacy pending quest: " + pair.Key);
                // 기존 Gold 필드는 달성 당시 확정액이다. 새 동적 계산으로 덮어쓰지 않는다.
                FillPending(pair.Value, quest);
                pair.Value.HasFixedGold = quest.RewardGroupId == 2001;
            }
            state.QuestSchemaVersion = SchemaVersion;
            return true;
        }

        /// <summary>옛 호출은 유지하며 새 거래 경로에서는 단일 now를 명시한다.</summary>
        public static void Before(ProgressionState state) => Before(state, LocalProgression.UtcNow);
        public static void Before(ProgressionState state, long nowUtc) => AdvancePeriod(state, nowUtc);

        /// <summary>옛 기간을 먼저 봉인한 후 카운터를 교체한다. 역행한 시각은 마지막 승인 시각으로 제한한다.</summary>
        public static void AdvancePeriod(ProgressionState state, long nowUtc)
        {
            nowUtc = Math.Max(nowUtc, state.QuestLastObservedUtc);
            QuestPeriod period = QuestPeriod.At(nowUtc);
            if (state.QuestDay != period.Day || state.QuestWeek != period.Week)
            {
                // 복원 및 자정 직전 시간 반영으로 마지막 목표를 달성한 경우도 보존한다.
                if (!string.IsNullOrEmpty(state.QuestDay) && !string.IsNullOrEmpty(state.QuestWeek)) After(state, nowUtc);
                state.QuestDay = period.Day;
                state.QuestWeek = period.Week;
                foreach (string key in state.Counters.Keys.Where(k => !k.StartsWith("L|", StringComparison.Ordinal) &&
                    !k.StartsWith("D" + period.Day + "|", StringComparison.Ordinal) &&
                    !k.StartsWith("W" + period.Week + "|", StringComparison.Ordinal)).ToArray()) state.Counters.Remove(key);
            }
            // 만료 시각은 exclusive이며, 화면이 닫혀 있어도 정리된다.
            foreach (string key in state.PendingQuests.Where(x => x.Value.ExpiresUtc <= nowUtc).Select(x => x.Key).ToArray())
                state.PendingQuests.Remove(key);
            state.QuestLastObservedUtc = nowUtc;
        }

        /// <summary>게임플레이 변경 이후 최고 기록과 완료 권리를 거래 복제본에 확정한다.</summary>
        public static void After(ProgressionState state) => After(state, LocalProgression.UtcNow);
        public static void After(ProgressionState state, long nowUtc)
        {
            state.BestStatTotal = Math.Max(state.BestStatTotal, state.AttackLevel + state.HealthLevel);
            state.BestMageTotal = Math.Max(state.BestMageTotal, state.MageSkills.Values.Sum(x => x.Enhance));
            state.BestEquipmentTotal = Math.Max(state.BestEquipmentTotal, state.Equipment.Sum(x => x.Level));
            QuestDefinition guide = ActiveGuide(state);
            foreach (QuestDefinition quest in Definitions)
            {
                // 미래 가이드의 현재 조건을 미리 latch하지 않는다. 업적은 모든 단계가 수집 대상이다.
                if (quest.Category == eQuestCategory.Guide && quest != guide) continue;
                if (state.Claims.Contains(Key(quest, state))) continue;
                if (quest.IsRepeatable)
                {
                    if (string.IsNullOrEmpty(state.QuestDay) || string.IsNullOrEmpty(state.QuestWeek)) continue;
                    string key = Key(quest, state);
                    if (state.PendingQuests.ContainsKey(key) || Progress(quest, state) < quest.RequiredCount) continue;
                    var pending = new QuestPending { Id = quest.QuestId,
                        ExpiresUtc = QuestPeriod.EndUtc(quest.Category, Period(quest, state)) + 7 * 86400L };
                    FillPending(pending, quest);
                    state.PendingQuests.Add(key, pending);
                }
                else if (!state.CompletedQuests.Contains(quest.QuestId) && Progress(quest, state) >= quest.RequiredCount)
                    state.CompletedQuests.Add(quest.QuestId);
            }
        }

        /// <summary>달성 당시의 표시/보상 규칙을 저장 객체로 복사한다.</summary>
        private static void FillPending(QuestPending pending, QuestDefinition quest)
        {
            pending.DefinitionVersion = QuestCatalog.Instance.Version;
            pending.Title = quest.Title;
            pending.Description = quest.Description;
            pending.Category = quest.Category;
            pending.ObjectiveType = quest.ObjectiveType;
            pending.PresentationType = quest.PresentationType;
            pending.TargetId = quest.TargetId;
            pending.RequiredCount = quest.RequiredCount;
            pending.Rewards = QuestCatalog.Instance.GetRewards(quest.RewardGroupId)
                .Select(x => new QuestRewardSave { Currency = x.Currency, Amount = x.Amount, IsDynamicGold = x.IsDynamicGold }).ToList();
        }

        /// <summary>장비 코드의 메타데이터 조회는 조율 계층에서 주입하고 순수 판정기에서 Unity를 참조하지 않는다.</summary>
        private static int EquipmentRarity(int code)
        {
            var data = EquipmentManager.Instance?.GetData(code);
            return data == null ? 0 : (int)data.rarity + 1;
        }

        /// <summary>완료를 보존한 목표는 조건이 내려가도 필요값까지 표시한다.</summary>
        public static long Progress(QuestDefinition quest, ProgressionState state)
        {
            if (quest == null) return 0;
            if (!quest.IsRepeatable && state.CompletedQuests.Contains(quest.QuestId)) return quest.RequiredCount;
            if (quest.IsRepeatable && state.PendingQuests.ContainsKey(Key(quest, state))) return quest.RequiredCount;
            return QuestProgressEvaluator.Evaluate(quest, state, QuestCatalog.Instance, EquipmentRarity);
        }

        /// <summary>순서에 의존하지 않고 명시 체인의 첫 미수령 가이드를 반환한다.</summary>
        public static QuestDefinition ActiveGuide(ProgressionState state) => Definitions.FirstOrDefault(q =>
            q.Category == eQuestCategory.Guide && !state.Claims.Contains(Key(q, state)) && PredecessorClaimed(q, state));

        private static bool PredecessorClaimed(QuestDefinition quest, ProgressionState state)
        {
            long previous = QuestCatalog.Instance.GetPredecessor(quest.QuestId);
            // 이관된 원장에 중간 수령만 있어도 계열의 앞 단계를 건너뛰지 않는다.
            while (previous != 0)
            {
                if (!state.Claims.Contains(Key(previous, "permanent"))) return false;
                previous = QuestCatalog.Instance.GetPredecessor(previous);
            }
            return true;
        }

        /// <summary>해금과 체인 정책을 공통 적용한다. 수령 검사는 거래 안에서 다시 수행한다.</summary>
        public static bool CanClaim(QuestDefinition quest, ProgressionState state) => quest != null &&
            (!quest.IsRepeatable || state.MainClears.Contains(0x20001000B)) && PredecessorClaimed(quest, state) &&
            !state.Claims.Contains(Key(quest, state)) && Progress(quest, state) >= quest.RequiredCount;

        /// <summary>기존 호출 호환. 새 UI는 기간과 계정 세대가 고정된 token을 전달한다.</summary>
        public static bool Claim(long id, string pendingKey = null)
        {
            if (!LocalProgression.TryGetCommittedState(out var state)) return false;
            QuestDefinition quest = QuestCatalog.Instance.Get(id);
            string prefix = $"quest:{id}:";
            if (pendingKey != null && !pendingKey.StartsWith(prefix, StringComparison.Ordinal)) return false;
            string period = pendingKey != null ? pendingKey.Substring(prefix.Length) : quest == null ? null : Period(quest, state);
            return period != null && TryClaim(new QuestClaimToken(LocalProgression.AccountGeneration, id, period)).Status == QuestClaimStatus.Success;
        }

        /// <summary>지급과 영수증을 하나의 내구 거래로 확정한다. 오래된 token을 현재 기간으로 바꾸지 않는다.</summary>
        public static QuestClaimResult TryClaim(QuestClaimToken token)
        {
            if (!LocalProgression.IsReady) return new QuestClaimResult(QuestClaimStatus.NotReady, token);
            if (token.AccountGeneration != LocalProgression.AccountGeneration) return new QuestClaimResult(QuestClaimStatus.StaleAccount, token);
            QuestClaimStatus status = QuestClaimStatus.SaveFailed;
            bool committed = LocalProgression.Execute("quest-claim", state =>
            {
                if (token.AccountGeneration != LocalProgression.AccountGeneration) { status = QuestClaimStatus.StaleAccount; return false; }
                string key = Key(token.QuestId, token.Period);
                if (state.Claims.Contains(key)) { status = QuestClaimStatus.AlreadyClaimed; return false; }
                QuestDefinition quest = QuestCatalog.Instance.Get(token.QuestId);
                state.PendingQuests.TryGetValue(key, out QuestPending pending);
                if (pending != null && pending.Id != token.QuestId) throw new InvalidDataException("Pending quest identity mismatch.");
                if (pending != null && !state.MainClears.Contains(0x20001000B))
                { status = QuestClaimStatus.Locked; return false; }
                if (pending == null)
                {
                    if (quest == null) { status = QuestClaimStatus.UnknownQuest; return false; }
                    if (token.Period != Period(quest, state)) { status = QuestClaimStatus.Expired; return false; }
                    if ((quest.IsRepeatable && !state.MainClears.Contains(0x20001000B)) || !PredecessorClaimed(quest, state))
                    { status = QuestClaimStatus.Locked; return false; }
                    if (Progress(quest, state) < quest.RequiredCount) { status = QuestClaimStatus.Incomplete; return false; }
                }
                else if (pending.ExpiresUtc <= state.QuestLastObservedUtc) { status = QuestClaimStatus.Expired; return false; }

                IEnumerable<QuestRewardSave> rewards = pending?.Rewards ?? QuestCatalog.Instance.GetRewards(quest.RewardGroupId)
                    .Select(x => new QuestRewardSave { Currency = x.Currency, Amount = x.Amount, IsDynamicGold = x.IsDynamicGold });
                foreach (QuestRewardSave reward in rewards)
                {
                    long amount = reward.Amount;
                    if (reward.IsDynamicGold)
                    {
                        // 새 규칙은 수령 시점 수입을 사용하고 legacy fixed Gold는 그대로 보존한다.
                        decimal value = (pending?.HasFixedGold == true ? pending.Gold : DynamicGold(state)) + state.GoldRemainder / 1000000m;
                        amount = BalanceMath.Floor(value);
                        state.GoldRemainder = (long)((value - amount) * 1000000m);
                    }
                    LocalProgression.Credit(state, reward.Currency, amount);
                }
                state.Claims.Add(key);
                state.PendingQuests.Remove(key);
                if (token.Period == "permanent") state.CompletedQuests.Add(token.QuestId);
                status = QuestClaimStatus.Success;
                return true;
            });
            return new QuestClaimResult(committed ? QuestClaimStatus.Success : status == QuestClaimStatus.Success ? QuestClaimStatus.SaveFailed : status, token);
        }

        /// <summary>확정된 상태만 읽는다. 로드, 기간 전환, 파일 저장은 수행하지 않는다.</summary>
        public static QuestBoardSnapshot GetSnapshot(eQuestCategory category)
        {
            long generation = LocalProgression.AccountGeneration;
            if (!LocalProgression.TryGetCommittedState(out var state)) return QuestBoardSnapshot.NotReady(category, generation);
            var rows = new List<QuestRowSnapshot>();
            foreach (QuestDefinition quest in Definitions.Where(x => x.Category == category))
            {
                // 수령 기록도 목록에 남긴다. 아직 해금되지 않은 체인만 제외하고,
                // 실제 진행 중 가이드 선택은 QuestManager의 미수령 필터가 담당한다.
                if ((category == eQuestCategory.Guide || category == eQuestCategory.Achievement) &&
                    !state.Claims.Contains(Key(quest, state)) && !PredecessorClaimed(quest, state)) continue;
                string key = Key(quest, state);
                state.PendingQuests.TryGetValue(key, out var pending);
                bool claimed = state.Claims.Contains(key);
                bool locked = quest.IsRepeatable && !state.MainClears.Contains(0x20001000B);
                // 현재 조건이 내려가거나 수령 시 pending이 제거되어도 완료 게이지는 되돌리지 않는다.
                long progress = claimed ? quest.RequiredCount : Math.Min(quest.RequiredCount, Progress(quest, state));
                QuestRowState rowState = claimed ? QuestRowState.Claimed : locked ? QuestRowState.Locked :
                    progress >= quest.RequiredCount ? QuestRowState.Claimable : QuestRowState.InProgress;
                var rewards = pending == null ? QuestCatalog.Instance.GetRewards(quest.RewardGroupId)
                    .Select(x => new QuestRewardSnapshot(x.Currency, x.Amount, x.IsDynamicGold)) : RewardSnapshot(pending);
                rows.Add(new QuestRowSnapshot(new QuestClaimToken(generation, quest.QuestId, Period(quest, state)), category,
                    pending?.Title ?? quest.Title, pending?.Description ?? quest.Description, pending?.ObjectiveType ?? quest.ObjectiveType, pending?.TargetId ?? quest.TargetId,
                    pending?.RequiredCount ?? quest.RequiredCount, pending?.RequiredCount ?? progress, rowState, false, pending?.ExpiresUtc ?? 0, rewards, pending?.PresentationType ?? quest.PresentationType));
            }
            // 카탈로그에서 비활성화된 항목도 저장된 지급 명세만으로 복원한다.
            foreach (var entry in state.PendingQuests.OrderBy(x => x.Value.ExpiresUtc).ThenBy(x => x.Value.Id))
            {
                QuestPending pending = entry.Value;
                if (pending.Category != category || pending.ExpiresUtc <= state.QuestLastObservedUtc || rows.Any(x => Key(x.Token.QuestId, x.Token.Period) == entry.Key)) continue;
                string prefix = $"quest:{pending.Id}:";
                if (!entry.Key.StartsWith(prefix, StringComparison.Ordinal)) continue;
                rows.Add(new QuestRowSnapshot(new QuestClaimToken(generation, pending.Id, entry.Key.Substring(prefix.Length)), category,
                    pending.Title, pending.Description, pending.ObjectiveType, pending.TargetId, pending.RequiredCount,
                    pending.RequiredCount, state.MainClears.Contains(0x20001000B) ? QuestRowState.Claimable : QuestRowState.Locked,
                    true, pending.ExpiresUtc, RewardSnapshot(pending), pending.PresentationType));
            }
            string period = category == eQuestCategory.Daily ? state.QuestDay : category == eQuestCategory.Weekly ? state.QuestWeek : "permanent";
            long reset = period == "permanent" ? 0 : QuestPeriod.EndUtc(category, period);
            return new QuestBoardSnapshot(generation, state.Revision, category, period, reset, rows);
        }

        private static IEnumerable<QuestRewardSnapshot> RewardSnapshot(QuestPending pending) => pending.Rewards.Select(x =>
            new QuestRewardSnapshot(x.Currency, x.IsDynamicGold && pending.HasFixedGold ? pending.Gold : x.Amount, x.IsDynamicGold && !pending.HasFixedGold));

        /// <summary>동적 골드의 기존 수입 계산과 소수 정밀도를 유지한다.</summary>
        public static decimal DynamicGold(ProgressionState state)
        {
            int stage = Scripts.Core.Manager.StageParser.GetStageNumber((eStage)state.OfflineStage);
            int wave = Scripts.Core.Manager.StageParser.GetWaveNumber((eStage)state.OfflineStage);
            // 안전 웨이브만 복원되고 검증 KPM 표본이 없으면 기존 1-1/3KPM bootstrap을 사용한다.
            if (stage < 1 || wave < 1 || wave > 10 || state.OfflineKpm <= 0) return 60;
            // 전투와 동일한 카탈로그를 사용해 4장 이후 및 확장 ID의 수입도 반영한다.
            return 2m * Math.Min(30m, state.OfflineKpm) * Scripts.Core.StageCatalogRules.MainEnemy(stage, wave).Gold * BalanceMath.RubyMultiplier(state.RubyGoldLevel);
        }

        /// <summary>기존 UI 문자열 요청을 typed 보상 정의에 연결한다.</summary>
        public static string RewardText(int groupId, Func<long, string> format = null)
        {
            format ??= value => value.ToString("N0", System.Globalization.CultureInfo.InvariantCulture);
            return string.Join(" · ", QuestCatalog.Instance.GetRewards(groupId).Select(x => x.IsDynamicGold ? "안전 사냥 2분 골드" :
                (x.Currency == eCurrency.AncientCoin ? "주화" : x.Currency == eCurrency.ClassFragment ? "전직 파편" :
                 x.Currency == eCurrency.ArcaneKnowledge ? "마법 지식" : x.Currency.ToString()) + " " + format(x.Amount)));
        }
    }
}
