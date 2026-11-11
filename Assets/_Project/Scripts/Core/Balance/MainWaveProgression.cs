using System;
using System.Collections.Generic;
using System.Linq;
using Protocol.Message.Dto;
using Scripts.Core;
using Scripts.Core.Manager;

namespace KingdomIdle.Balance
{
    [Serializable]
    public sealed class MainBattleSave
    {
        public string RunId;
        public long Stage;
        public Dictionary<ulong, int> AcceptedKills = new();
        public MainBattleSave Copy() => new() { RunId = RunId, Stage = Stage, AcceptedKills = new(AcceptedKills) };
    }

    [Serializable]
    public sealed class MainWaveCompletionSave
    {
        public string RunId, RequestId;
        public long Stage;
        public int NextAction;
        public Dictionary<ulong, int> TotalKills = new();
        public MainWaveCompletionSave Copy() => new()
        { RunId = RunId, RequestId = RequestId, Stage = Stage, NextAction = NextAction, TotalKills = new(TotalKills) };
    }

    /// <summary>메인 회차의 서버 스냅샷과 미확정 완료 요청만 보관한다. 보상·진행은 응답에서 확정한다.</summary>
    public static class MainWaveProgression
    {
        private static NetworkManager _network;
        private static long _localGeneration = -1, _networkGeneration = -1;
        private static bool _supported;
        public static bool Available => _supported && _network != null && _network == NetworkManager.Instance &&
            _network.IsGameDataReady && _localGeneration == LocalProgression.AccountGeneration &&
            _networkGeneration == _network.AccountGeneration;

        static MainWaveProgression() { LocalProgression.AccountChanged += () => _supported = false; }

        public static void ReceiveServerState(bool supported)
        {
            _network = NetworkManager.Instance;
            _localGeneration = LocalProgression.AccountGeneration;
            _networkGeneration = _network != null ? _network.AccountGeneration : -1;
            _supported = supported;
        }

        internal static bool ValidId(string value) => value != null && value.Length == 32 &&
            Guid.TryParseExact(value, "N", out var id) && value == id.ToString("N");

        internal static bool ValidStage(long value)
        {
            var stage = (eStage)value;
            int chapter = StageParser.GetStageNumber(stage), wave = StageParser.GetWaveNumber(stage);
            return chapter > 0 && wave >= 1 && wave <= 11 && StageParser.GetStageType(stage) == eStageType.Main &&
                (long)StageParser.MakeStage(eStageType.Main, chapter, wave) == value;
        }

        private static bool ValidKills(Dictionary<ulong, int> kills) => kills != null && kills.Count <= 10 &&
            kills.All(pair => pair.Key != 0 && pair.Value > 0 && pair.Value <= short.MaxValue);

        internal static bool ValidSaved(MainBattleSave battle) => battle == null ||
            (ValidId(battle.RunId) && ValidStage(battle.Stage) && ValidKills(battle.AcceptedKills));
        internal static bool ValidSaved(MainWaveCompletionSave pending) => pending == null ||
            (ValidId(pending.RunId) && ValidId(pending.RequestId) && ValidStage(pending.Stage) &&
             (pending.NextAction == 0 || pending.NextAction == 1) && ValidKills(pending.TotalKills) && pending.TotalKills.Count > 0);

        public static bool ApplyServerState(ProgressionState state, MainBattleStateDTO battle)
        {
            if (battle == null) { state.MainBattle = null; return true; }
            try { NetworkManager.ValidateMainBattle(battle); }
            catch (Newtonsoft.Json.JsonSerializationException) { return false; }
            state.MainBattle = new MainBattleSave
            {
                RunId = battle.RunId, Stage = (long)battle.Stage,
                AcceptedKills = battle.AcceptedKills.ToDictionary(kill => (ulong)kill.MonsterType, kill => (int)kill.Count)
            };
            // 전체 상태 조회가 새 회차를 보여도 이전 완료의 성공 여부는 같은 RequestId로 확인한다.
            return true;
        }

        internal static List<HuntResult> Kills(Dictionary<ulong, int> counts) => counts.OrderBy(pair => pair.Key)
            .Select(pair => new HuntResult { MonsterType = (Protocol.Message.Data.eMonsterType)pair.Key, Count = checked((short)pair.Value) }).ToList();

        internal static OnCompleteMainWaveRequestDTO Request(MainWaveCompletionSave pending) => new()
        {
            ClearedStage = (Protocol.Message.Data.eStage)pending.Stage, MainRunId = pending.RunId,
            RequestId = pending.RequestId, TotalKills = Kills(pending.TotalKills),
            NextAction = (Protocol.Message.Data.eStageClearAction)pending.NextAction
        };

        /// <summary>재접속 때 다시 관측한 처치에 서버 수락분을 더하지 않고, 아직 수락되지 않은 차이만 보낸다.</summary>
        internal static List<HuntResult> Delta(IReadOnlyDictionary<eMonsterType, int> observed, MainBattleSave battle)
        {
            var result = new List<HuntResult>();
            int remaining = 149;
            foreach (var pair in observed.OrderBy(pair => (ulong)pair.Key))
            {
                battle.AcceptedKills.TryGetValue((ulong)pair.Key, out int accepted);
                int count = Math.Min(remaining, Math.Max(0, pair.Value - accepted));
                if (count == 0) continue;
                result.Add(new HuntResult { MonsterType = (Protocol.Message.Data.eMonsterType)(ulong)pair.Key, Count = (short)count });
                remaining -= count;
                if (remaining == 0 || result.Count == 10) break;
            }
            return result;
        }
    }
}
