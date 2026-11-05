#if UNITY_EDITOR || LOBBY_DEVICE_QA
using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Protocol.Message.Dto;
using Scripts.Core;
using Scripts.Core.Manager;

namespace KingdomIdle.Balance
{
    public static class ServerDungeonAcceptance
    {
        public static object Run()
        {
            var checks = new List<string>();
            void Check(bool valid, string name)
            { if (!valid) throw new InvalidOperationException("Server dungeon acceptance: " + name); checks.Add(name); }
            long reset = new DateTimeOffset(2026, 11, 4, 0, 0, 0, TimeSpan.FromHours(9)).ToUnixTimeSeconds();
            DungeonStateDTO Snapshot() => new()
            {
                GoldTickets = 1, RubyTickets = 2, GoldDungeonClear = 2, RubyDungeonClear = 1,
                TicketResetUtc = reset, ActiveBattleId = "server-run",
                ActiveDungeonId = (long)StageParser.MakeStage(eStageType.GoldDungeon, 3)
            };
            var state = new ProgressionState
            {
                GoldTickets = 9, RubyTickets = 8, GoldDungeonClear = 5, RubyDungeonClear = 4,
                TicketDay = "2026-11-02", ActiveDungeon = "old-local-run", ActiveBattleId = "local-run"
            };
            state.Wallet[eCurrency.Ruby] = 125;
            state.Claims.Add("ruby-first:4");
            Check(UserManager.ApplyDungeonState(state, Snapshot()), "valid snapshot accepted");
            Check(state.GoldTickets == 1 && state.RubyTickets == 2 && state.GoldDungeonClear == 2 && state.RubyDungeonClear == 1,
                "server values replace stale local tickets and clears");
            Check(state.ActiveDungeon == "server-run" && state.ActiveBattleId == "local-run" && state.ServerDungeonId == Snapshot().ActiveDungeonId,
                "server battle identity does not replace the local session identity");
            Check(state.TicketDay == "2026-11-03" && state.DungeonTicketResetUtc == reset, "reset boundary belongs to the preceding KST day");
            string archive = state.Modules["legacy-dungeon"];
            var old = JObject.Parse(archive);
            Check((int)old["GoldTickets"] == 9 && (long)old["Ruby"] == 125 && (string)old["ActiveDungeon"] == "old-local-run" &&
                state.Claims.Contains("ruby-first:4") && state.Wallet[eCurrency.Ruby] == 125, "old save archived without granting or deleting rewards");
            var ended = Snapshot(); ended.ActiveBattleId = null; ended.ActiveDungeonId = null;
            Check(UserManager.ApplyDungeonState(state, ended) && state.ActiveDungeon == null && state.ServerDungeonId == 0 &&
                state.Modules["legacy-dungeon"] == archive, "completed run clears server identity and preserves the first archive");
            var invalid = new Action<DungeonStateDTO>[]
            {
                d => d.GoldTickets = -1, d => d.GoldTickets = 3, d => d.RubyTickets = -1, d => d.RubyTickets = 3,
                d => d.GoldDungeonClear = -1, d => d.GoldDungeonClear = 6,
                d => d.RubyDungeonClear = -1, d => d.RubyDungeonClear = 6,
                d => d.TicketResetUtc = 0, d => d.TicketResetUtc = long.MaxValue,
                d => d.ActiveBattleId = null, d => d.ActiveDungeonId = null, d => d.ActiveBattleId = " ",
                d => d.ActiveDungeonId = 0x230010001, d => d.ActiveDungeonId = -1,
                d => d.ActiveDungeonId = (long)StageParser.MakeStage(eStageType.Main, 1),
                d => d.ActiveDungeonId = (long)StageParser.MakeStage(eStageType.GoldDungeon, 6),
                d => d.ActiveDungeonId = (long)StageParser.MakeStage(eStageType.GoldDungeon, 1, 2)
            };
            string before = JsonConvert.SerializeObject(state);
            foreach (var mutate in invalid)
            {
                var snapshot = Snapshot(); mutate(snapshot);
                Check(!UserManager.ApplyDungeonState(state, snapshot) && JsonConvert.SerializeObject(state) == before,
                    "invalid snapshot rejected atomically " + checks.Count);
            }
            Check(!UserManager.ApplyDungeonState(state, null) && JsonConvert.SerializeObject(state) == before, "missing snapshot cannot overwrite save");
            var copy = state.DeepClone();
            Check(copy.DungeonTicketResetUtc == reset && copy.ServerDungeonId == state.ServerDungeonId,
                "transaction clone retains server dungeon fields");
            return new { passed = true, count = checks.Count, checks };
        }
    }
}
#endif
