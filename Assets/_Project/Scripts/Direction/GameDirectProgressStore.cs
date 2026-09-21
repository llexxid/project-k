using System;
using System.Collections.Generic;
using KingdomIdle.Balance;
using Newtonsoft.Json;

namespace Direction
{
    /// <summary>계정 저장 안에 들어가는 안내 전용 상태다. 숫자 인덱스 대신 단계 ID를 저장해 순서 변경에 대비한다.</summary>
    public sealed class GameDirectProgress
    {
        public int Version = 1;
        public List<string> ConfirmedSteps = new();
        public bool Completed;
        public bool Skipped;

        /// <summary>저장 전 원본과 분리된 후보 상태를 만든다. 실패한 저장을 메모리 완료 상태로 공개하지 않는다.</summary>
        public GameDirectProgress Copy() => new()
        {
            Version = Version, ConfirmedSteps = new List<string>(ConfirmedSteps),
            Completed = Completed, Skipped = Skipped
        };
    }

    /// <summary>기존 내구 저장 경계를 재사용한다. 조회는 계정을 열거나 local-guest 상태를 생성하지 않는다.</summary>
    public sealed class GameDirectProgressStore
    {
        private const string Prefix = "game-direct:";

        /// <summary>준비된 동일 계정의 안내 상태를 읽는다. 손상/새 버전은 처음 상태로 덮어쓰지 않고 거절한다.</summary>
        public bool TryLoad(string id, long generation, out GameDirectProgress progress)
        {
            progress = null;
            if (LocalProgression.AccountGeneration != generation || !LocalProgression.TryGetCommittedState(out var state))
                return false;
            if (!state.Modules.TryGetValue(Prefix + id, out string raw))
            { progress = new GameDirectProgress(); return true; }
            try
            {
                progress = JsonConvert.DeserializeObject<GameDirectProgress>(raw);
                return progress != null && progress.Version == 1 && progress.ConfirmedSteps != null;
            }
            catch (JsonException) { return false; }
        }

        /// <summary>계정 세대를 거래 안에서도 확인한다. Ensure로 계정이 바뀌거나 늦은 응답이 도착해도 저장하지 않는다.</summary>
        public bool TrySave(string id, long generation, GameDirectProgress progress)
        {
            if (!LocalProgression.IsReady || LocalProgression.AccountGeneration != generation) return false;
            string json = JsonConvert.SerializeObject(progress);
            return LocalProgression.Execute("game-direct-progress", state =>
            {
                if (LocalProgression.AccountGeneration != generation) return false;
                state.Modules[Prefix + id] = json;
                return true;
            });
        }
    }
}
