using System;
using System.Linq;
using System.Collections.Generic;
using KingdomIdle.MageTower;
using Scripts.Core;
using Scripts.Monster;

namespace KingdomIdle.Balance
{
    /// <summary>
    /// 전투 세션의 로컬 상태(활성 전투·처치 순번·던전 티켓·오프라인 KPM 표본)만 다룬다.
    /// 메인 스테이지의 골드·경험치·드롭·최초 클리어 보상은 서버(OnHuntReward·OnStageClear)가 확정해 응답의 플레이어 데이터로 돌아오고,
    /// 퀘스트 집계도 서버가 한다(클라이언트는 OnGetQuestState로 받아 표시만 한다). 던전 보상은 아직 로컬이다.
    /// </summary>
    public static class BattleEconomy
    {
        private sealed class Sample { public double Seconds; public int Kills; }
        private static readonly Dictionary<long, Queue<Sample>> Samples = new();
        private static double _seconds;

        /// <summary>전투 경과 시간을 누적한다. 오프라인 보상의 KPM 표본에만 쓴다.</summary>
        public static void Tick(double delta)
        {
            if (delta > 0) _seconds += delta;
        }

        /// <summary>새 계정을 바인딩한다. 계정 간 KPM 표본을 공유하지 않는다.</summary>
        public static void OnAccountOpened(long generation)
        {
            Samples.Clear();
            _seconds = 0;
        }

#if UNITY_EDITOR || LOBBY_DEVICE_QA
        // 옛 온라인 퀘스트 시간(BattleTime) 인수 검사 호환용. 퀘스트 집계가 서버로 옮겨져 더 이상 시간을 모으지 않는다(검사는 무효화됨).
        public static void TestRecordQuestTime(long fromUtc, long toUtc, double seconds) { }
        public static double TestPendingQuestSeconds => 0;
        public static void TestCaptureQuestTime(long utc, double monotonic, bool counting, bool reanchor = false) { }
        public static long PrepareQuestTime(ProgressionState draft, long nowUtc) => 0;
        public static void AcknowledgeQuestTime(long token) { }
#endif

        public static void RefreshTickets(ProgressionState state)
        {
            string day = LocalProgression.KstDay;
            if (!QuestPeriod.IsLaterDay(day, state.TicketDay)) return;
            state.TicketDay = day; state.GoldTickets = state.RubyTickets = StageCatalogRules.Database.DailyTickets;
        }
        public static int Tickets(eStageType type)
        {
            var s = LocalProgression.State;
            if (QuestPeriod.IsLaterDay(LocalProgression.KstDay, s.TicketDay)) return StageCatalogRules.Database.DailyTickets;
            return type == eStageType.GoldDungeon ? s.GoldTickets : s.RubyTickets;
        }
        public static bool Begin(StageSession session)
        {
            _seconds = 0;
            return LocalProgression.Execute("battle-start", s => {
                RefreshTickets(s);
                var type = session.Definition.Type;
                if (type == eStageType.Main) s.CycleFrontierStage = Math.Max(s.CycleFrontierStage, (long)session.Definition.Id);
                if (type == eStageType.GoldDungeon) { if (s.GoldTickets <= 0) return false; s.GoldTickets--; }
                if (type == eStageType.RubyDungeon) { if (s.RubyTickets <= 0) return false; s.RubyTickets--; }
                s.ActiveBattleId = session.RunId; s.LastKillSequence = 0;
                s.ActiveDungeon = type == eStageType.Main ? null : session.RunId;
                s.LastActiveUtc = LocalProgression.UtcNow;
                return true;
            });
        }
        public static bool Kill(StageSession session, Monster monster)
        {
            var definition = session.Definition;
            long committedGold = 0;
            // 메인 스테이지의 전장 드롭은 서버(OnHuntReward)가 굴려 응답의 Drops·Equipment로 돌려준다. 로컬은 굴리지도 지급하지도 않는다.
            bool ok = LocalProgression.Execute("battle-kill", s => {
                if (s.ActiveBattleId != session.RunId || session.TotalKillCount <= s.LastKillSequence) return false;
                var reward = monster.BalanceReward;
                long gold = reward.Gold, exp = reward.Experience;
                if (definition.Type == eStageType.Main)
                {
                    // 메인 스테이지의 골드·경험치는 서버(OnHuntReward)가 확정해 절대값으로 저장된다. 로컬은 처치 수만 기록하고,
                    // 정의 보상은 웨이브 HUD의 EarnedGold 표시에만 더한다. 처치·보스 퀘스트도 서버가 집계한다.
                    committedGold = gold;
                }
                else
                {
                    LocalProgression.Credit(s, eCurrency.Gold, gold);
                    committedGold = gold;
                    BalanceMath.GainExperience(ref s.AccountLevel, ref s.Experience, exp);
                }
                s.Kills = checked(s.Kills + 1); s.LastKillSequence = session.TotalKillCount;
                s.LastActiveUtc = LocalProgression.UtcNow;
                return true;
            });
            if (!ok) { UnityEngine.Time.timeScale = 0; KingdomIdle.UGUI.UIManager.Instance?.ShowToast("보상 저장에 실패해 전투를 멈췄습니다. 저장 공간 확인 후 재접속해 주세요."); }
            if (ok) session.EarnedGold = checked(session.EarnedGold + committedGold);
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
                if (d.Type == eStageType.Main)
                {
                    s.HighestMainClear = Math.Max(s.HighestMainClear, id);
                    Core.Stage.MainStageRule.GetNextWave(d.Id, out eStage next);
                    s.CycleFrontierStage = Math.Max(s.CycleFrontierStage, (long)next);
                    if (d.WaveNumber == 11) s.CycleBossStage = Math.Max(s.CycleBossStage, d.StageNumber);
                    // 최초 클리어 보상(무기·스킬·재화)은 서버(OnStageClear)가 지급하고 응답의 Equipment·MageTowerState·Currency로 돌려준다.
                    s.MainClears.Add(id);
                    if (d.WaveNumber <= 10 && id >= s.OfflineStage)
                    { s.OfflineStage = id; s.OfflineKpm = kpm; s.OfflineRubyGold = s.RubyGoldLevel; s.OfflineRubyExp = s.RubyExpLevel; }
                }
                else if (d.Type == eStageType.GoldDungeon)
                { s.GoldDungeonClear = Math.Max(s.GoldDungeonClear, d.StageNumber);s.LastDungeonGold=StageCatalogRules.DungeonReward(true,d.StageNumber);s.LastDungeonRuby=0; }
                else
                {
                    long ruby = d.Encounter.ClearRuby;
                    if (s.Claims.Add("ruby-first:" + d.StageNumber)) ruby = checked(ruby + d.Encounter.FirstClearRuby);
                    LocalProgression.Credit(s, eCurrency.Ruby, ruby);
                    s.LastDungeonGold=0;s.LastDungeonRuby=ruby;
                    s.RubyDungeonClear = Math.Max(s.RubyDungeonClear, d.StageNumber);
                }
                // 환생은 서버(OnReincarnation)가 요청 즉시 적용한다. 웨이브 경계의 로컬 환생 처리는 없다.
                if (d.Type == eStageType.Main && d.WaveNumber <= 10) RubyProgression.CommitReset(s);
                s.ActiveDungeon = null;
                return true;
            });
            if (!result) { UnityEngine.Time.timeScale = 0; KingdomIdle.UGUI.UIManager.Instance?.ShowToast("클리어 보상을 저장할 수 없습니다. 저장 공간 확인 후 재접속해 주세요."); }
            if (result) { EquipmentManager.Instance?.RestoreEquipment(); MageTowerManager.Instance?.NotifyCommitted(); }
            return result;
        }
    }
}
