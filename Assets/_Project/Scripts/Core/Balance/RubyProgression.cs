using System;
using System.Collections;
using System.Globalization;
using Newtonsoft.Json;
using PlayFab;
using Protocol.Message.Dto;
using Scripts.Core;
using Scripts.Core.Manager;
using UnityEngine;

namespace KingdomIdle.Balance
{
    public static class RubyProgression
    {
        public static event Action Changed;
        public static event Action<bool, string> Completed;
        private static bool _available, _busy, _battlePaused;
        private static long _localGeneration = -1, _networkGeneration = -1;
        private static float _resumeTimeScale;
        public static int Revision { get; private set; }
        public static string Status { get; private set; }
        public static bool Unlocked => LocalProgression.State.HighestMainClear >= 0x200020005;
        private static bool CurrentSession => _localGeneration == LocalProgression.AccountGeneration &&
            NetworkManager.Instance != null && _networkGeneration == NetworkManager.Instance.AccountGeneration;
        public static bool Available => _available && CurrentSession && NetworkManager.Instance.IsGameDataReady;
        public static bool IsBusy => _busy && CurrentSession;
        public static bool HasPendingRequest => !string.IsNullOrEmpty(LocalProgression.State.PendingRubyRequestId);
        public static bool WaitingForWave => LocalProgression.State.PendingRubyReset != 0;

        static RubyProgression()
        {
            LocalProgression.AccountChanged += () =>
            {
                if (_battlePaused) Time.timeScale = _resumeTimeScale;
                _battlePaused = _busy = _available = false;
                Status = null;
                NotifyChanged();
            };
        }

        // 저장된 지원 여부는 사용하지 않고 로그인 때 받은 응답에만 거래를 허용한다.
        public static void ReceiveServerState(bool available)
        {
            long generation = LocalProgression.AccountGeneration;
            long networkGeneration = NetworkManager.Instance != null ? NetworkManager.Instance.AccountGeneration : -1;
            if (generation != _localGeneration || networkGeneration != _networkGeneration)
            {
                if (_battlePaused) Time.timeScale = _resumeTimeScale;
                _battlePaused = _busy = false;
                Status = null;
            }
            _localGeneration = generation;
            _networkGeneration = networkGeneration;
            _available = available;
            NotifyChanged();
        }

        public static bool ApplyServerState(ProgressionState s, RubyGrowthStateDTO growth)
        {
            if (growth == null) return true;
            try { NetworkManager.ValidateRubyGrowth(growth); }
            catch (JsonSerializationException) { return false; }
            if (!s.Modules.ContainsKey("legacy-ruby-growth"))
            {
                s.Modules["legacy-ruby-growth"] = JsonConvert.SerializeObject(new
                {
                    s.RubyGoldLevel, s.RubyExpLevel, s.RubyGoldSpent, s.RubyExpSpent,
                    s.BestRubyGoldLevel, s.BestRubyExpLevel, s.RubyResetDay, s.PendingRubyReset,
                    Ruby = s.Wallet.TryGetValue(eCurrency.Ruby, out long ruby) ? ruby : 0
                });
                s.PendingRubyReset = 0;
            }
            s.RubyGoldLevel = growth.GoldLevel;
            s.RubyExpLevel = growth.ExpLevel;
            s.RubyGoldSpent = growth.GoldSpent;
            s.RubyExpSpent = growth.ExpSpent;
            s.BestRubyGoldLevel = growth.BestGoldLevel;
            s.BestRubyExpLevel = growth.BestExpLevel;
            s.RubyResetDay = growth.LastResetDay;
            s.RubyNextRequestSequence = growth.NextRequestSequence;
            // 순번이 소비됐으면 옛 요청은 다시 적용될 수 없다. 같은 순번은 결과 미확정으로 보존한다.
            if (NetworkManager.TryParseRubyRequestId(s.PendingRubyRequestId, out long sequence) &&
                growth.NextRequestSequence > sequence) ClearRequest(s);
            return true;
        }

        public static bool Enhance(bool experience)
        {
            if (!CanStart() || !Unlocked) return false;
            var s = LocalProgression.State;
            long? cost = BalanceMath.RubyCost(experience ? s.RubyExpLevel : s.RubyGoldLevel);
            if (!cost.HasValue || LocalProgression.Balance(eCurrency.Ruby) < cost.Value ||
                (experience && s.AccountLevel >= BalanceMath.AccountCap)) return false;
            return BeginRequest(false, experience);
        }

        public static bool Reset(bool experience)
        {
            if (!CanStart() || !ChangeJob.CanQueueChange) return false;
            var state = LocalProgression.State;
            if (!QuestPeriod.IsLaterDay(LocalProgression.KstDay, state.RubyResetDay) ||
                (experience ? state.RubyExpLevel : state.RubyGoldLevel) == 0) return false;
            if (StageManager.Instance?.CurrentRunState != eStageRunState.Running)
                return BeginRequest(true, experience);
            bool saved = LocalProgression.Execute("ruby-reset-request", s =>
            {
                s.PendingRubyReset = experience ? 2 : 1;
                return true;
            });
            if (saved) { Status = "일반 웨이브 종료 후 서버에 초기화를 요청합니다."; NotifyChanged(); }
            return saved;
        }

        public static void OnWaveCleared(StageDefinition definition)
        {
            if (!WaitingForWave || IsBusy || definition.Type != eStageType.Main || definition.WaveNumber > 10) return;
            // 클리어 처리의 스테이지 전환이 먼저 끝나야 정산 가드가 전환을 막지 않는다.
            StageManager.Instance?.StartCoroutine(CommitQueuedReset(LocalProgression.AccountGeneration));
        }

        private static IEnumerator CommitQueuedReset(long generation)
        {
            yield return null;
            while (generation == LocalProgression.AccountGeneration &&
                (StageManager.Instance?.CurrentRunState == eStageRunState.Transitioning || Time.timeScale <= 0f)) yield return null;
            if (generation != LocalProgression.AccountGeneration || !WaitingForWave || IsBusy) yield break;
            bool experience = LocalProgression.State.PendingRubyReset == 2;
            if (!BeginRequest(true, experience))
            {
                ClearQueuedReset();
                Complete(false, "전투 보상 정산 후 초기화를 다시 요청해 주세요.");
            }
        }

        public static bool RetryPending()
        {
            if (!Available || IsBusy || !HasPendingRequest) return false;
            var s = LocalProgression.State;
            return BeginRequest(s.PendingRubyRequestIsReset, s.PendingRubyRequestExperience);
        }

        public static bool RefreshPending()
        {
            if (!Available || IsBusy || !HasPendingRequest) return false;
            return SettleBeforeRequest(() => RefreshResult(null, false));
        }

        private static bool CanStart() => Available && !IsBusy && !HasPendingRequest && !WaitingForWave;

        private static bool BeginRequest(bool reset, bool experience)
        {
            if (!Available || IsBusy) return false;
            string retryId = LocalProgression.State.PendingRubyRequestId;
            return SettleBeforeRequest(() =>
            {
                if (!string.IsNullOrEmpty(retryId) && LocalProgression.State.PendingRubyRequestId != retryId)
                {
                    Complete(true, "서버 루비 상태를 불러왔습니다.");
                    return;
                }
                bool saved = LocalProgression.Execute("ruby-server-request", s => PrepareRequest(s, reset, experience, retryId));
                if (!saved)
                {
                    Complete(false, "루비 요청을 저장하지 못했습니다. 저장 공간을 확인해 주세요.");
                    return;
                }
                var s = LocalProgression.State;
                string requestId = s.PendingRubyRequestId;
                long generation = _localGeneration, networkGeneration = _networkGeneration;
                bool IsCurrent() => IsCurrentRequest(generation, networkGeneration);
                if (s.PendingRubyRequestIsReset)
                    NetworkManager.Instance.Call<OnResetRubyEnhancementRequestDTO, OnResetRubyEnhancementResponseDTO>(
                        new OnResetRubyEnhancementRequestDTO { RequestId = requestId, ExpectedLevel = s.PendingRubyExpectedLevel,
                            Type = s.PendingRubyRequestExperience ? RubyGrowthType.Exp : RubyGrowthType.Gold },
                        r => { if (IsCurrent()) ApplyResult(r, r.CurrentRuby, "루비 성장 초기화가 완료되었습니다."); },
                        e => { if (IsCurrent()) RejectResult(e); });
                else
                    NetworkManager.Instance.Call<OnEnchantRubyRequestDTO, OnEnchantRubyResponseDTO>(
                        new OnEnchantRubyRequestDTO { RequestId = requestId, ExpectedLevel = s.PendingRubyExpectedLevel,
                            Type = s.PendingRubyRequestExperience ? RubyGrowthType.Exp : RubyGrowthType.Gold },
                        r => { if (IsCurrent()) ApplyResult(r, r.CurrentRuby, "루비 강화가 완료되었습니다."); },
                        e => { if (IsCurrent()) RejectResult(e); });
            });
        }

        internal static bool PrepareRequest(ProgressionState s, bool reset, bool experience, string retryId = null)
        {
            if (!string.IsNullOrEmpty(retryId) && s.PendingRubyRequestId != retryId) return false;
            if (!string.IsNullOrEmpty(s.PendingRubyRequestId))
                return NetworkManager.TryParseRubyRequestId(s.PendingRubyRequestId, out _) &&
                    s.PendingRubyRequestIsReset == reset && s.PendingRubyRequestExperience == experience &&
                    s.PendingRubyExpectedLevel >= 0 && s.PendingRubyExpectedLevel <= 50;
            if (s.RubyNextRequestSequence < 1 || s.RubyNextRequestSequence == long.MaxValue) return false;
            s.PendingRubyRequestId = s.RubyNextRequestSequence.ToString(CultureInfo.InvariantCulture) + ":" + Guid.NewGuid().ToString("N");
            s.PendingRubyRequestIsReset = reset;
            s.PendingRubyRequestExperience = experience;
            s.PendingRubyExpectedLevel = experience ? s.RubyExpLevel : s.RubyGoldLevel;
            s.PendingRubyReset = 0;
            return true;
        }

        private static bool SettleBeforeRequest(Action send)
        {
            var offline = GameManager.Instance?.OfflineReward;
            if (offline != null && (offline.IsBusy || offline.LastFailure != null)) return false;
            var manager = StageManager.Instance;
            if (manager == null || manager.CurrentRunState == eStageRunState.Transitioning || Time.timeScale <= 0f) return false;
            long generation = _localGeneration, networkGeneration = _networkGeneration;
            _busy = true;
            Status = "서버에 확인 중입니다.";
            NotifyChanged();
            if (manager.FlushPendingHunts(ok =>
            {
                if (!CurrentSession || generation != _localGeneration || networkGeneration != _networkGeneration) return;
                if (!ok)
                {
                    ClearQueuedReset();
                    Complete(false, "전투 보상을 정산하지 못했습니다. 연결 상태를 확인해 주세요.");
                    return;
                }
                _resumeTimeScale = Time.timeScale;
                _battlePaused = true;
                Time.timeScale = 0f;
                send();
            })) return true;
            Complete(false, "전투 보상을 정산 중입니다. 잠시 후 다시 시도해 주세요.");
            return false;
        }

        private static bool IsCurrentRequest(long generation, long networkGeneration) =>
            IsBusy && generation == _localGeneration && networkGeneration == _networkGeneration;

        internal static bool ApplyResponse(ProgressionState s, RubyActionResponseDTO response, long ruby)
        {
            if (response == null || response.RequestId != s.PendingRubyRequestId || ruby < 0 ||
                !NetworkManager.TryParseRubyRequestId(response.RequestId, out long sequence) || sequence == long.MaxValue ||
                response.RubyGrowthState == null || response.RubyGrowthState.NextRequestSequence != sequence + 1 ||
                !ApplyServerState(s, response.RubyGrowthState)) return false;
            s.Wallet[eCurrency.Ruby] = ruby;
            ClearRequest(s);
            return true;
        }

        private static void ApplyResult(RubyActionResponseDTO response, long ruby, string message)
        {
            if (!HasPendingRequest) { Complete(true, "서버 루비 상태를 불러왔습니다."); return; }
            if (LocalProgression.Execute("server-ruby-result", s => ApplyResponse(s, response, ruby)))
            {
                Complete(true, message);
                return;
            }
            Complete(false, "서버 결과를 저장하지 못했습니다. 같은 요청 재시도 또는 서버 상태 확인을 눌러 주세요.");
        }

        private static void RejectResult(PlayFabError error)
        {
            int code = NetworkManager.TryGetBusinessError(error, out var business) ? (int)business.ErrorCode : 0;
            // 저장소 오류(1014)는 이미 적용됐을 수 있으므로 요청을 버리지 않는다.
            if (code == 1031 || (code >= 1052 && code <= 1064)) { RefreshResult(error, true); return; }
            Complete(false, "루비 요청 결과를 확인하지 못했습니다. 같은 요청 재시도 또는 서버 상태 확인을 눌러 주세요.");
        }

        private static void RefreshResult(PlayFabError rejection, bool clearRejected)
        {
            long generation = _localGeneration, networkGeneration = _networkGeneration;
            string requestId = LocalProgression.State.PendingRubyRequestId;
            void Done(bool ok)
            {
                if (!CurrentSession || generation != _localGeneration || networkGeneration != _networkGeneration) return;
                if (ok && clearRejected && LocalProgression.State.PendingRubyRequestId == requestId)
                    ok = LocalProgression.Execute("ruby-request-rejected", s => { ClearRequest(s); return true; });
                string message = !ok ? "서버 상태를 불러오지 못했습니다. 같은 요청을 다시 확인해 주세요." :
                    rejection != null ? NetworkManager.DescribeFailure(rejection, "루비 성장") :
                    HasPendingRequest ? "아직 결과가 확정되지 않았습니다. 같은 요청을 재시도해 주세요." : "서버 루비 상태를 불러왔습니다.";
                Complete(ok && rejection == null && !HasPendingRequest, message);
            }
            if (UserManager.Instance != null) UserManager.Instance.RefreshFromServer(Done);
            else Done(false);
        }

        private static void ClearRequest(ProgressionState s)
        {
            s.PendingRubyRequestId = null;
            s.PendingRubyRequestIsReset = s.PendingRubyRequestExperience = false;
            s.PendingRubyExpectedLevel = 0;
            s.PendingRubyReset = 0;
        }

        private static void ClearQueuedReset()
        {
            if (WaitingForWave) LocalProgression.Execute("ruby-reset-cancel", s => { s.PendingRubyReset = 0; return true; });
        }

        private static void Complete(bool ok, string message)
        {
            _busy = false;
            if (_battlePaused && CurrentSession) Time.timeScale = _resumeTimeScale;
            _battlePaused = false;
            Status = message;
            NotifyChanged();
            Completed?.Invoke(ok, message);
        }

        private static void NotifyChanged() { Revision++; Changed?.Invoke(); }
    }
}
