using Newtonsoft.Json;

namespace KingdomIdle.Balance
{
    internal static class LegacyDungeonProgression
    {
        internal static void Preserve(ProgressionState state)
        {
            if (state.Modules.ContainsKey("legacy-dungeon")) return;
            state.Wallet.TryGetValue(eCurrency.Ruby, out long ruby);
            state.Modules["legacy-dungeon"] = JsonConvert.SerializeObject(new
            {
                state.GoldTickets, state.RubyTickets, state.TicketDay,
                state.GoldDungeonClear, state.RubyDungeonClear,
                state.ActiveDungeon, state.ActiveBattleId, state.LastClearedBattle,
                state.LastDungeonGold, state.LastDungeonRuby, Ruby = ruby, state.Claims
            });
        }
    }
}
