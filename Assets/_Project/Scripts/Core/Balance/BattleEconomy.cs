using System;
using System.Linq;
using System.Collections.Generic;
using KingdomIdle.MageTower;
using Scripts.Core;
using Scripts.Monster;

namespace KingdomIdle.Balance
{
    public static class BattleEconomy
    {
        private sealed class Sample { public double Seconds; public int Kills; }
        private static readonly Dictionary<long, Queue<Sample>> Samples = new();
        private static double _seconds;

        // 전투 시간은 확정 전까지만 메모리에 둔다. 날짜별 소수 초를 합쳐 저장 주기마다 버려지는 시간을 막는다.
        private static readonly List<QuestTimeBucket> PendingQuestTime = new();
        // Prepare의 불변 차감 명세다. 저장 중 새 샘플이 생겨도 이번에 승인한 초만 제거한다.
        private static readonly List<QuestTimeDebit> PreparedQuestTime = new();
        private static long _accountGeneration, _nextBatchToken, _preparedBatchToken, _preparedGeneration, _preparedUtc;
        // 같은 샘플 지점을 Tick과 거래 prepare가 공유하므로 실행 순서와 무관하게 한 구간을 한 번만 수집한다.
        private static double _lastSampleMonotonic, _lastSampleUtc;
        private static bool _hasTimeAnchor, _wasCountingTime, _applicationSuspended;
        // 실제 전투 Task가 활성인 세션만 참조한다. 최초 스폰·보스 입장·결과 연출은 이 참조를 갖지 않는다.
        private static StageSession _questTimeSession;

        /// <summary>게임 규칙에는 기존 배율 시간을 사용하고, 퀘스트 시간은 공유 단조 시계로 별도 수집한다.</summary>
        public static void Tick(double delta)
        {
            if (delta > 0) _seconds += delta;
            long nowUtc = LocalProgression.UtcNow;
            CaptureUntil(nowUtc);

            // 보통 처치·시전 거래가 시간을 함께 저장한다. 거래 없는 전투와 날짜 경계만 별도 flush한다.
            if (PendingQuestTime.Sum(x => x.Seconds) >= 10 || PendingQuestTime.Any(x => x.EndUtc <= nowUtc))
                TryFlushQuestTime();
        }

        /// <summary>StageSession의 실제 전투 시작에 연결한다. 로딩과 보스 입장 연출의 시간을 포함하지 않는다.</summary>
        public static void StartQuestTime(StageSession session)
        {
            CaptureUntil(LocalProgression.UtcNow);
            _questTimeSession = session;
            ReanchorQuestTime();
        }

        /// <summary>결과 수락 직전에 마지막 전투 구간을 수집하고, 이후 결과 연출의 수집을 차단한다.</summary>
        public static void StopQuestTime(StageSession session)
        {
            if (_questTimeSession != session) return;
            CaptureUntil(LocalProgression.UtcNow);
            _questTimeSession = null;
            ReanchorQuestTime();
        }

        /// <summary>세션 종료 시 남은 시간을 확정한다. 저장 실패 버퍼는 다음 거래의 재시도를 위해 유지한다.</summary>
        public static void End(StageSession session)
        {
            StopQuestTime(session);
            if (LocalProgression.State.ActiveBattleId == session.RunId) TryFlushQuestTime();
        }

        /// <summary>
        /// LocalProgression.Execute가 기간을 넘기기 전에 호출한다. 이전 기간부터 순서대로 draft에만 반영하고
        /// 저장 성공 뒤 AcknowledgeQuestTime에 넘길 토큰을 반환한다. 내부에서 Execute를 중첩 호출하지 않는다.
        /// </summary>
        public static long PrepareQuestTime(ProgressionState draft, long nowUtc)
        {
            // 마탑 거래가 Stage Tick보다 먼저 실행되어도 자정 이전의 마지막 구간을 먼저 확보한다.
            CaptureUntil(nowUtc);
            if (_accountGeneration != LocalProgression.AccountGeneration)
                throw new InvalidOperationException("다른 계정의 전투 시간은 적용할 수 없습니다.");

            PreparedQuestTime.Clear();
            _preparedBatchToken = 0;
            if (PendingQuestTime.Count == 0) return 0;

            _preparedBatchToken = checked(++_nextBatchToken);
            _preparedGeneration = _accountGeneration;
            _preparedUtc = nowUtc;
            foreach (QuestTimeBucket bucket in PendingQuestTime)
            {
                // 진행도 단위는 초다. 같은 기간의 1초 미만 잔량은 버퍼에 남겨 다음 거래와 합친다.
                long seconds = (long)Math.Floor(bucket.Seconds + .000001d);
                if (seconds <= 0) continue;
                QuestEconomy.AdvancePeriod(draft, bucket.StartUtc);
                QuestEconomy.Count(draft, eQuestObjectiveType.BattleTime, 0, seconds);
                PreparedQuestTime.Add(new QuestTimeDebit(bucket, seconds));
            }
            return _preparedBatchToken;
        }

        /// <summary>내구성 있는 저장이 끝난 거래의 시간만 제거한다. 중복·지난 토큰은 다시 차감하지 않는다.</summary>
        public static void AcknowledgeQuestTime(long token)
        {
            if (token == 0 || token != _preparedBatchToken || _preparedGeneration != _accountGeneration) return;
            foreach (QuestTimeDebit debit in PreparedQuestTime)
                debit.Bucket.Seconds = Math.Max(0, debit.Bucket.Seconds - debit.Seconds);

            // 끝난 날짜의 소수 초를 새 날짜로 이월하면 기간을 섞게 된다. 해당 날짜를 확정한 뒤에만 버린다.
            PendingQuestTime.RemoveAll(x => x.Seconds < .000001d || (x.EndUtc <= _preparedUtc && x.Seconds < 1));
            PreparedQuestTime.Clear();
            _preparedBatchToken = 0;
        }

        /// <summary>기존 거래가 없는 때만 빈 변이를 실행하여 공통 prepare/save/ack 경로를 이용한다.</summary>
        public static bool TryFlushQuestTime()
        {
            CaptureUntil(LocalProgression.UtcNow);
            if (!PendingQuestTime.Any(x => x.Seconds >= 1)) return true;
            return LocalProgression.Execute("battle-time", _ => true);
        }

        /// <summary>앱 pause/focus 상실 직전에 마지막 활성 구간을 수집하고 백그라운드 수집을 막는다.</summary>
        public static void Suspend()
        {
            CaptureUntil(LocalProgression.UtcNow);
            _applicationSuspended = true;
            ReanchorQuestTime();
            TryFlushQuestTime();
        }

        /// <summary>복귀 시 기준 시각을 새로 잡아 백그라운드 동안의 경과시간을 제외한다.</summary>
        public static void Resume()
        {
            _applicationSuspended = false;
            ReanchorQuestTime();
        }

        /// <summary>이전 계정 flush 성공 뒤 새 계정을 바인딩한다. 계정 간 미확정 시간과 KPM 표본을 공유하지 않는다.</summary>
        public static void OnAccountOpened(long generation)
        {
            _accountGeneration = generation;
            _questTimeSession = null;
            PendingQuestTime.Clear();
            PreparedQuestTime.Clear();
            _preparedBatchToken = 0;
            Samples.Clear();
            _seconds = 0;
            ReanchorQuestTime();
        }

        /// <summary>UTC는 기간 위치만 정하고, 실제 더하는 시간은 배속과 무관한 Unity 단조 시계에서 얻는다.</summary>
        private static void CaptureUntil(long nowUtc)
        {
            double monotonic = UnityEngine.Time.realtimeSinceStartupAsDouble;
            double observedUtc = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000d;
            CaptureSample(nowUtc, monotonic, observedUtc, CanCountQuestTime());
        }

        /// <summary>Tick과 거래가 서로 다른 원시 시각을 받아도 승인된 기간 안에서 실제 경과시간을 한 번만 누산한다.</summary>
        private static void CaptureSample(long nowUtc, double monotonic, double observedUtc, bool counting)
        {
            // Execute뿐 아니라 Tick/일시중단/flush도 같은 하한을 사용한다. 서로 다른 기준을 섞으면
            // 과거 Tick과 미래 prepare가 번갈아 앵커를 되감아 정상 전투 시간을 반복해서 버리게 된다.
            if (LocalProgression.TryGetCommittedState(out ProgressionState state))
                nowUtc = Math.Max(nowUtc, state.QuestLastObservedUtc);

            // 거래의 정수 UTC 초 안에서 소수 위치를 복구하고, 이미 수집한 소수 시각도 되돌리지 않는다.
            // UTC는 기간 선택용이며 정지한 UTC에서도 monotonic 경과시간은 계속 누적한다.
            double utc = Math.Max(nowUtc, Math.Min(nowUtc + .999999d, observedUtc));
            if (_hasTimeAnchor) utc = Math.Max(utc, _lastSampleUtc);
            if (_hasTimeAnchor && _wasCountingTime && counting)
                RecordQuestTime(_lastSampleUtc, utc, Math.Max(0, monotonic - _lastSampleMonotonic));

            // 메뉴/정지 중에도 앵커를 옮겨 복귀 프레임에 제외 구간을 한꺼번에 더하지 않는다.
            _lastSampleMonotonic = monotonic;
            _lastSampleUtc = utc;
            _hasTimeAnchor = true;
            _wasCountingTime = counting;
        }

        /// <summary>전투 Task·앱 상태·메뉴 상태를 함께 확인한다. Time.timeScale은 양수 여부만 보고 배율로 곱하지 않는다.</summary>
        private static bool CanCountQuestTime()
        {
            if (_applicationSuspended || UnityEngine.Time.timeScale <= 0 || _questTimeSession == null ||
                !_questTimeSession.IsRunning || !_questTimeSession.IsBattleRunning || _questTimeSession.HasPendingResult) return false;
            var ui = KingdomIdle.UGUI.UIManager.Instance;
            return ui == null || !ui.BlocksQuestBattleTime;
        }

        /// <summary>전투 시작·중단·계정 전환은 시간을 추가하지 않고 현재 시각에서 새 구간을 시작한다.</summary>
        private static void ReanchorQuestTime()
        {
            _hasTimeAnchor = false;
            CaptureUntil(LocalProgression.UtcNow);
        }

        /// <summary>실제 경과시간을 KST 자정에서 나눠 날짜별로 누산한다. 주간 경계는 월요일 자정에 자연스럽게 분리된다.</summary>
        private static void RecordQuestTime(double fromUtc, double toUtc, double seconds)
        {
            if (seconds <= 0 || double.IsNaN(seconds) || double.IsInfinity(seconds) || toUtc < fromUtc) return;
            // 대부분의 프레임은 같은 날짜다. 매 프레임 날짜 문자열을 만들지 않고 기존 버킷에 바로 더한다.
            QuestTimeBucket latest = PendingQuestTime.Count == 0 ? null : PendingQuestTime[PendingQuestTime.Count - 1];
            if (latest != null && fromUtc >= latest.StartUtc && fromUtc < latest.EndUtc && toUtc <= latest.EndUtc)
            {
                latest.Seconds += seconds;
                return;
            }
            double cursor = fromUtc;
            double span = toUtc - fromUtc;
            do
            {
                QuestPeriod period = QuestPeriod.At((long)Math.Floor(cursor));
                double end = Math.Min(toUtc, period.DayEndUtc);
                // 같은 밀리초 안의 연속 호출도 실제 단조 시간은 누적한다.
                double part = span > 0 ? seconds * (end - cursor) / span : seconds;
                QuestTimeBucket bucket = PendingQuestTime.Count == 0 ? null : PendingQuestTime[PendingQuestTime.Count - 1];
                if (bucket == null || bucket.EndUtc != period.DayEndUtc)
                {
                    bucket = new QuestTimeBucket(period.DayEndUtc - 86400, period.DayEndUtc);
                    PendingQuestTime.Add(bucket);
                }
                bucket.Seconds += part;
                cursor = end;
            }
            while (cursor < toUtc);
        }

#if UNITY_EDITOR || LOBBY_DEVICE_QA
        /// <summary>실제 프레임 대기 없이 기간 경계·저장 실패·ack 재시도를 검증하기 위한 승인 전 시간 입력.</summary>
        public static void TestRecordQuestTime(long fromUtc, long toUtc, double seconds) => RecordQuestTime(fromUtc, toUtc, seconds);
        public static double TestPendingQuestSeconds => PendingQuestTime.Sum(x => x.Seconds);
        /// <summary>실제 Capture의 공통 경로에 시계/활성 입력만 주입하여 UTC 역행과 단조 경과시간을 함께 검증한다.</summary>
        public static void TestCaptureQuestTime(long utc, double monotonic, bool counting, bool reanchor = false)
        {
            if (reanchor) _hasTimeAnchor = false;
            CaptureSample(utc, monotonic, utc, counting);
        }
#endif

        /// <summary>계정 세대 내 하루에 수집한 미확정 실제 초. JSON의 퀘스트 진행도와는 분리한다.</summary>
        private sealed class QuestTimeBucket
        {
            public readonly long StartUtc, EndUtc;
            public double Seconds;
            public QuestTimeBucket(long startUtc, long endUtc) { StartUtc = startUtc; EndUtc = endUtc; }
        }

        /// <summary>한 저장 시도에서 특정 버퍼로부터 반영한 초의 불변 명세.</summary>
        private readonly struct QuestTimeDebit
        {
            public readonly QuestTimeBucket Bucket;
            public readonly long Seconds;
            public QuestTimeDebit(QuestTimeBucket bucket, long seconds) { Bucket = bucket; Seconds = seconds; }
        }

        public static void RefreshTickets(ProgressionState state)
        {
            if (state.TicketDay == LocalProgression.KstDay) return;
            state.TicketDay = LocalProgression.KstDay; state.GoldTickets = state.RubyTickets = 2;
        }
        public static int Tickets(eStageType type)
        {
            var s = LocalProgression.State;
            if (s.TicketDay != LocalProgression.KstDay) return 2;
            return type == eStageType.GoldDungeon ? s.GoldTickets : s.RubyTickets;
        }
        public static bool Begin(StageSession session)
        {
            _seconds = 0;
            return LocalProgression.Execute("battle-start", s => {
                RefreshTickets(s);
                var type = session.Definition.Type;
                if (type == eStageType.GoldDungeon) { if (s.GoldTickets <= 0) return false; s.GoldTickets--; }
                if (type == eStageType.RubyDungeon) { if (s.RubyTickets <= 0) return false; s.RubyTickets--; }
                if (type != eStageType.Main) QuestEconomy.Count(s,eQuestObjectiveType.DungeonEnter,(long)session.Definition.Id,1);
                s.ActiveBattleId = session.RunId; s.LastKillSequence = 0;
                s.ActiveDungeon = type == eStageType.Main ? null : session.RunId;
                s.LastActiveUtc = LocalProgression.UtcNow;
                return true;
            });
        }
        public static bool Kill(StageSession session, Monster monster)
        {
            var definition = session.Definition;
            var drop = definition.Type == eStageType.Main && definition.WaveNumber <= 10 ? EquipmentManager.Instance?.RollFieldDrop(definition.StageNumber) : null;
            bool ok = LocalProgression.Execute("battle-kill", s => {
                if (s.ActiveBattleId != session.RunId || session.TotalKillCount <= s.LastKillSequence) return false;
                var reward = monster.BalanceReward;
                long gold = reward.Gold, exp = reward.Experience;
                if (definition.Type == eStageType.Main)
                {
                    gold = BalanceMath.WithRemainder(gold, s.RubyGoldLevel, ref s.GoldRemainder);
                    exp = BalanceMath.WithRemainder(exp, s.RubyExpLevel, ref s.ExpRemainder);
                }
                LocalProgression.Credit(s, eCurrency.Gold, gold);
                BalanceMath.GainExperience(ref s.AccountLevel, ref s.Experience, exp);
                QuestEconomy.Count(s,eQuestObjectiveType.MonsterKill,0,1);
                if (monster.IsBalanceBoss) QuestEconomy.Count(s,eQuestObjectiveType.BossKill,0,1);
                s.Kills = checked(s.Kills + 1); s.LastKillSequence = session.TotalKillCount;
                if (drop != null && !EquipmentManager.Grant(s, drop, true))
                    throw new InvalidOperationException("Approved equipment reward requires space.");
                s.LastActiveUtc = LocalProgression.UtcNow;
                return true;
            });
            if (!ok) { UnityEngine.Time.timeScale = 0; KingdomIdle.UGUI.UIManager.Instance?.ShowToast("보상 저장에 실패해 전투를 멈췄습니다. 저장 공간 확인 후 재접속해 주세요."); }
            if (ok && drop != null)
            {
                EquipmentManager.Instance?.RestoreEquipment();
                KingdomIdle.UGUI.UIManager.Instance?.NotifyFieldEquipment(drop.Code);
            }
            return ok;
        }
        public static bool Clear(StageSession session)
        {
            var d = session.Definition;
            long id = (long)d.Id;
            decimal kpm = 0;
            if (d.Type == eStageType.Main && d.WaveNumber <= 10)
            {
                if (!Samples.TryGetValue(id, out var samples)) Samples[id] = samples = new Queue<Sample>();
                samples.Enqueue(new Sample { Seconds = Math.Max(.01, _seconds), Kills = session.TotalKillCount });
                while (samples.Count > 1 && samples.Sum(x => x.Seconds) - samples.Peek().Seconds >= 300) samples.Dequeue();
                double seconds = samples.Sum(x => x.Seconds);
                kpm = Math.Min(seconds < 300 ? 15m : 30m, (decimal)(samples.Sum(x => x.Kills) * 60d / seconds));
            }
            bool result = LocalProgression.Execute("battle-clear", s => {
                if (s.ActiveBattleId != session.RunId || s.LastClearedBattle == session.RunId) return false;
                s.LastClearedBattle = session.RunId;
                
                if (d.Type != eStageType.Main) QuestEconomy.Count(s,eQuestObjectiveType.DungeonClear,id,1);
                else if (d.WaveNumber <= 10) QuestEconomy.Count(s,eQuestObjectiveType.MainWaveClear,0,1);
                if (d.Type == eStageType.Main)
                {
                    s.HighestMainClear = Math.Max(s.HighestMainClear, id);
                    if (d.WaveNumber == 11) s.CycleBossStage = Math.Max(s.CycleBossStage, d.StageNumber);
                    bool first = s.MainClears.Add(id);
                    if (first) FirstClear(s, d.StageNumber, d.WaveNumber);
                    if (d.WaveNumber <= 10 && id >= s.OfflineStage)
                    { s.OfflineStage = id; s.OfflineKpm = kpm; s.OfflineRubyGold = s.RubyGoldLevel; s.OfflineRubyExp = s.RubyExpLevel; }
                }
                else if (d.Type == eStageType.GoldDungeon)
                { s.GoldDungeonClear = Math.Max(s.GoldDungeonClear, d.StageNumber);s.LastDungeonGold=checked(session.TotalKillCount*BalanceMath.Mimic(d.StageNumber).Gold);s.LastDungeonRuby=0; }
                else
                {
                    long ruby = BalanceMath.RubyClear(d.StageNumber);
                    if (s.Claims.Add("ruby-first:" + d.StageNumber)) ruby += 25L * d.StageNumber;
                    LocalProgression.Credit(s, eCurrency.Ruby, ruby);
                    s.LastDungeonGold=0;s.LastDungeonRuby=ruby;
                    s.RubyDungeonClear = Math.Max(s.RubyDungeonClear, d.StageNumber);
                }
                if (d.Type == eStageType.Main && d.WaveNumber <= 10) { RubyProgression.CommitReset(s); Reincarnation.ReincarnationService.CommitAtBoundary(s,session.RunId); }
                s.ActiveDungeon = null;
                return true;
            });
            if (!result) { UnityEngine.Time.timeScale = 0; KingdomIdle.UGUI.UIManager.Instance?.ShowToast("클리어 보상을 저장할 수 없습니다. 저장 공간 확인 후 재접속해 주세요."); }
            if (result) { EquipmentManager.Instance?.RestoreEquipment(); MageTowerManager.Instance?.NotifyCommitted(); }
            return result;
        }
        private static void FirstClear(ProgressionState s, int stage, int wave)
        {
            int node = stage * 100 + wave;
            if (node == 101 || node == 103 || node == 107)
            {
                string job = node == 101 ? "Knight" : node == 103 ? "Archer" : "Mage";
                int attack = node == 107 ? 15 : 10;
                var data = EquipmentManager.Instance?.GetByRarity(eEquipmentRarity.Normal).Find(x => x.IsAllowedForJob(job) && x.bonusAtk == attack);
                if (data == null || !EquipmentManager.Grant(s, new EquipmentSave { Id = Guid.NewGuid().ToString("N"), Code = data.itemCode }, true))
                    throw new InvalidOperationException("First-clear weapon unavailable.");
            }
            int skill = node == 105 ? 0 : node == 203 ? 1 : node == 303 ? 2 : -1;
            if (skill >= 0)
            {
                MageTowerManager.Grant(s, skill);
                if (!s.MageSlots.Contains(skill)) { int slot = Array.IndexOf(s.MageSlots, -1); if (slot >= 0) s.MageSlots[slot] = skill; }
            }
            long coins = node == 111 || node == 205 ? 100 : node == 211 ? 200 : node == 311 ? 300 : 0;
            if (coins > 0) LocalProgression.Credit(s, eCurrency.AncientCoin, coins);
            if (node == 111 || node == 311) LocalProgression.Credit(s, eCurrency.ClassFragment, 40);
            if (node == 211) MageTowerManager.Grant(s, 0);
        }
    }
}
