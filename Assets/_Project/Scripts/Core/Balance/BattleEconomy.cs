using System;
using System.Linq;
using System.Collections.Generic;
using KingdomIdle.MageTower;
using Scripts.Core;
using Scripts.Monster;

namespace KingdomIdle.Balance
{
    /// <summary>
    /// 전투 세션의 로컬 실행 상태와 오프라인 KPM 표본을 다룬다.
    /// 전투 보상과 던전 입장권·클리어는 서버 응답을 적용하며 여기서 지급하지 않는다.
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

        public static int Tickets(eStageType type)
        {
            DungeonTicketRefresh.RequestIfDue();
            var s = LocalProgression.State;
            return type == eStageType.GoldDungeon ? s.GoldTickets : s.RubyTickets;
        }
        public static bool Begin(StageSession session)
        {
            _seconds = 0;
            return LocalProgression.Execute("battle-start", s => {
                var type = session.Definition.Type;
                if (type == eStageType.Main) s.CycleFrontierStage = Math.Max(s.CycleFrontierStage, (long)session.Definition.Id);
                if (type != eStageType.Main && (s.ServerDungeonId != (long)session.Definition.Id || string.IsNullOrEmpty(s.ActiveDungeon))) return false;
                s.ActiveBattleId = session.RunId; s.LastKillSequence = 0;
                s.LastActiveUtc = LocalProgression.UtcNow;
                return true;
            });
        }
        public static bool Kill(StageSession session, Monster monster)
        {
            long committedGold = 0;
            // 정의 보상은 전투 HUD에만 표시한다. 지갑·경험치·드롭은 OnHuntReward 응답으로 확정한다.
            bool ok = LocalProgression.Execute("battle-kill", s => {
                if (s.ActiveBattleId != session.RunId || session.TotalKillCount <= s.LastKillSequence) return false;
                committedGold = monster.BalanceReward.Gold;
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
            if (d.Type != eStageType.Main) return false;
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
                // 환생은 서버(OnReincarnation)가 요청 즉시 적용한다. 웨이브 경계의 로컬 환생 처리는 없다.
                return true;
            });
            if (!result) { UnityEngine.Time.timeScale = 0; KingdomIdle.UGUI.UIManager.Instance?.ShowToast("클리어 보상을 저장할 수 없습니다. 저장 공간 확인 후 재접속해 주세요."); }
            if (result) { EquipmentManager.Instance?.RestoreEquipment(); MageTowerManager.Instance?.NotifyCommitted(); }
            return result;
        }
    }
}
