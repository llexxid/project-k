#if UNITY_EDITOR || LOBBY_DEVICE_QA
using System;
using System.Collections.Generic;
using Reincarnation;
using Scripts.Core.Manager;
using Scripts.Core;

namespace KingdomIdle.Balance
{
    public static class ServerReincarnationAcceptance
    {
        public static object Run()
        {
            var checks = new List<string>();
            void Check(bool valid, string name)
            { if (!valid) throw new InvalidOperationException("Server reincarnation acceptance: " + name); checks.Add(name); }
            var state = new ProgressionState
            {
                MainStage = 0x200010001, CycleFrontierStage = 0x200040001,
                HighestMainClear = 0x20007000B, LastReincarnationUtc = LocalProgression.UtcNow - 601
            };
            Check(ReincarnationService.Eligibility(state) == eReincarnationFailureReason.None,
                "returning to 1-1 does not remove eligibility earned this cycle");
            int bosses = UserManager.CycleBossClears(Math.Max(state.MainStage, state.CycleFrontierStage));
            var preview = new ReincarnationPolicy().Evaluate(new ReincarnationState(0, 0), true, bosses);
            Check(bosses == 3 && preview.CanReincarnate && preview.LevelGain == 15,
                "preview uses three bosses from this cycle instead of current or permanent progress");
            state.CycleFrontierStage = state.MainStage;
            Check(ReincarnationService.Eligibility(state) == eReincarnationFailureReason.StageRequirementNotMet,
                "a new cycle cannot reuse permanent boss clears");
            state.MainStage = 0x200020001; state.CycleFrontierStage = 0;
            Check(ReincarnationService.Eligibility(state) == eReincarnationFailureReason.None,
                "legacy snapshots without a frontier retain current-stage eligibility");
            return new { passed = true, count = checks.Count, checks };
        }
    }
}
#endif
