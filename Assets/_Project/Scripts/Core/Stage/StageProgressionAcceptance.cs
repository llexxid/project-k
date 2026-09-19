#if UNITY_EDITOR || LOBBY_DEVICE_QA
using System;
using System.Collections.Generic;
using System.Linq;
using Core.Stage;
using KingdomIdle.Balance;
using KingdomIdle.OfflineRewards;
using Scripts.Core.Manager;

namespace Scripts.Core
{
    public static class StageProgressionAcceptance
    {
        public static object Run()
        {
            var checks = new List<string>();
            void Check(bool valid, string name)
            { if (!valid) throw new InvalidOperationException("Stage acceptance: " + name); checks.Add(name); }
            foreach (var type in new[] { eStageType.Main, eStageType.GoldDungeon, eStageType.RubyDungeon })
                for (int chapter = 1; chapter <= 3; chapter++)
                    Check((long)StageParser.MakeStage(type, chapter) == (0x200000001L | ((long)type << 28) | ((long)chapter << 16)), $"preserve ID {type}/{chapter}");
            MainStageRule.GetNextWave(StageParser.MakeStage(eStageType.Main, 3, 11), out var next);
            Check(next == StageParser.MakeStage(eStageType.Main, 4), "3 boss enters 4-1");
            foreach (int chapter in new[] { 4, 5, 6, 99, 4095, 4096, 10000, 1000000 })
            {
                var id = StageParser.MakeStage(eStageType.Main, chapter, 11);
                MainStageRule.GetNextWave(id, out next);
                Check(StageParser.GetStageNumber(next) == chapter + 1 && StageParser.GetStageType(next) == eStageType.Main && StageParser.GetWaveNumber(next) == 1, $"chapter boundary {chapter}");
                var record = StageCatalogRules.Get(eStageType.Main, chapter);
                var template = StageCatalogRules.Get(eStageType.Main, (chapter - 1) % 3 + 1);
                Check(record.MonsterEntries.Select(x => x.MonsterType).SequenceEqual(template.MonsterEntries.Select(x => x.MonsterType)), $"theme rotation {chapter}");
                var numbers = record.MonsterEntries[0].Combat.Numbers;
                Check(numbers.HP > 0 && numbers.Attack > 0 && numbers.Gold > 0 && numbers.Experience > 0, $"valid late-game stats {chapter}");
                var plan = OfflineRewardCalculator.CreatePlan(TimeSpan.FromHours(1), (long)record.Id, 30);
                Check(plan.estimatedKillCount == 1080, $"offline progression {chapter}");
            }
            for (int chapter = 1; chapter <= 3; chapter++)
                for (int wave = 1; wave <= 11; wave++)
                    Check(StageCatalogRules.MainEnemy(chapter, wave).HP == BalanceMath.MainEnemy(chapter, wave).HP, $"opening HP preserved {chapter}-{wave}");
            for (int chapter = 4; chapter <= 100; chapter++)
                Check(StageCatalogRules.MainEnemy(chapter, 10).HP > StageCatalogRules.MainEnemy(chapter - 1, 10).HP, $"continuous growth {chapter}");
            foreach (string flow in new[] { "GoldClear", "RubyClear" })
                Check(StageCatalogRules.Database.TryGetKillCountFlow(flow, out var record) && record.DefeatAction == eStageFlowAction.AwaitDefeatChoice, $"timed defeat result {flow}");
            return new { passed = true, count = checks.Count, checks };
        }
    }
}
#endif
