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
        private static double _seconds, _unpaidSeconds;
        public static void Tick(double delta)
        {
            if (delta <= 0) return; _seconds += delta;
            var ui = KingdomIdle.UGUI.UIManager.Instance;
            if (ui != null && (ui.HasBlockingPanel || ui.HasActiveTabPanel)) return;
            _unpaidSeconds += delta;
            if (_unpaidSeconds >= 10) FlushTime();
        }
        private static void FlushTime()
        {
            long seconds = (long)_unpaidSeconds; if (seconds <= 0) return;
            if (LocalProgression.Execute("battle-time", s => { QuestEconomy.Count(s,eQuestObjectiveType.BattleTime,0,seconds); return true; })) _unpaidSeconds -= seconds;
        }
        public static void End(StageSession session) { if (LocalProgression.State.ActiveBattleId == session.RunId) FlushTime(); }

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
