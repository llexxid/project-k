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

#if UNITY_EDITOR
        /// <summary>
        /// 지정한 안내 ID의 진행·실습 성공·주화 지급 기록만 하나의 거래에서 지운다. Manager가 기존 실행 정리를 끝낸 뒤 호출한다.
        /// 지급 키 제거로 다음 실습은 다시 지급·소비·획득한다. 현재 재화·강화·장비·스킬과 다른 안내·퀘스트 기록은 보존한다.
        /// 계정 세대 불일치·잘못된 입력·저장 실패 시 false이며 기존 기록은 확정 상태로 유지한다.
        /// </summary>
        public bool ResetForTesting(IReadOnlyList<string> ids, long generation)
        {
            if (ids == null || ids.Count == 0 || !LocalProgression.IsReady || generation != LocalProgression.AccountGeneration) return false;
            var selected = new HashSet<string>(StringComparer.Ordinal);
            foreach (var id in ids)
            {
                if (string.IsNullOrWhiteSpace(id)) return false;
                selected.Add(id);
            }
            return LocalProgression.Execute("game-direct-test-reset", state =>
            {
                if (generation != LocalProgression.AccountGeneration) return false;
                var removals = new List<string>();
                foreach (string id in selected)
                {
                    state.Modules.Remove(Prefix + id);
                    string actionPrefix = "game-direct-action:" + id + ":";
                    foreach (var key in state.Modules.Keys)
                        if (key.StartsWith(actionPrefix, StringComparison.Ordinal)) removals.Add(key);
                    string grantPrefix = "game-direct-grant:" + id + ":";
                    state.Claims.RemoveWhere(key => key.StartsWith(grantPrefix, StringComparison.Ordinal));
                }
                foreach (var key in removals) state.Modules.Remove(key);
                return true;
            });
        }
#endif

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
            return LocalProgression.Execute("game-direct-progress", state =>
            {
                if (LocalProgression.AccountGeneration != generation) return false;
                var merged = progress.Copy();
                // 입력 대기 중 확정된 정보를 과거 복사본으로 되돌리지 않는다. 거래의 최신 초안에서 단계·종료 상태를 합친다.
                if (state.Modules.TryGetValue(Prefix + id, out string raw))
                {
                    GameDirectProgress latest;
                    try { latest = JsonConvert.DeserializeObject<GameDirectProgress>(raw); }
                    catch (JsonException) { return false; }
                    if (latest == null || latest.Version != 1 || latest.ConfirmedSteps == null) return false;
                    foreach (string step in latest.ConfirmedSteps)
                        if (!merged.ConfirmedSteps.Contains(step)) merged.ConfirmedSteps.Add(step);
                    merged.Skipped |= latest.Skipped;
                    merged.Completed = !merged.Skipped && (merged.Completed || latest.Completed);
                }
                state.Modules[Prefix + id] = JsonConvert.SerializeObject(merged);
                return true;
            });
        }
    }
}
