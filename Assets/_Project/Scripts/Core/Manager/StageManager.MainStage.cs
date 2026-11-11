using KingdomIdle.Balance;
using Protocol.Message.Dto;
using Scripts.Core;
using UnityEngine;

namespace Scripts.Core.Manager
{
    public partial class StageManager
    {
        private bool _mainStageRequestPending;

        /// <summary>기존 처치를 정산한 뒤 서버가 확정한 메인 위치로 이동한다.</summary>
        private bool RequestMainStageChange(eStage target, bool loop)
        {
            var network = NetworkManager.Instance;
            var user = UserManager.Instance;
            if (network == null || !network.IsGameDataReady || user == null ||
                CurrentDefinition?.Type != eStageType.Main || StageParser.GetStageType(target) != eStageType.Main ||
                (_currentState != eStageRunState.Running && _currentState != eStageRunState.DefeatPending &&
                    _currentState != eStageRunState.Resolving) ||
                _mainStageRequestPending || _dungeonRequestPending || _huntSyncFailed || _onHuntsSettled != null ||
                _huntsHeld || RubyProgression.IsBusy || GameManager.Instance?.Reincarnation?.IsBusy == true ||
                LocalProgression.State.PendingMainCompletion != null ||
                (_currentState == eStageRunState.Resolving && _currentSession?.IsCleared == true && !IsMainDefeatSettlement) ||
                !string.IsNullOrEmpty(LocalProgression.State.ActiveDungeon) ||
                !TryGetStageDefinition(target, out var definition) || !definition.Enabled)
                return false;

            long generation = LocalProgression.AccountGeneration, networkGeneration = network.AccountGeneration;
            var token = _token;
            var previousState = _currentState;
            bool previousLoop = _isLoopMode;
            long fromStage = 0;
            _mainStageRequestPending = true;
            bool IsCurrent() => this != null && _mainStageRequestPending && ReferenceEquals(_token, token) &&
                generation == LocalProgression.AccountGeneration && NetworkManager.Instance == network &&
                network.AccountGeneration == networkGeneration;

            void Stop(string message)
            {
                if (!IsCurrent()) return;
                _mainStageRequestPending = false;
                _currentState = previousState;
                _huntSyncFailed = true;
                Time.timeScale = 0;
                Debug.LogWarning("[Stage] " + message);
                KingdomIdle.UGUI.UIManager.Instance?.ShowToast(message);
            }

            void ResumeConfirmedStage()
            {
                if (!IsCurrent()) return;
                var state = LocalProgression.State;
                var confirmed = (eStage)state.MainStage;
                if (!string.IsNullOrEmpty(state.ActiveDungeon) ||
                    !TryGetStageDefinition(confirmed, out var confirmedDefinition) ||
                    confirmedDefinition.Type != eStageType.Main || !confirmedDefinition.Enabled)
                {
                    Stop("서버의 메인 스테이지를 확인하지 못했습니다. 재접속해 주세요.");
                    return;
                }
                // 같은 위치 요청은 서버에서 no-op이므로 이미 정산한 부분 처치도 그대로 유지한다.
                if (state.MainStage != fromStage) _saturatedTypes.Clear();
                _isLoopMode = confirmed == target ? loop : previousLoop;
                _mainStageRequestPending = false;
                _currentState = previousState;
                if (!TransitionStage(confirmed))
                {
                    _huntSyncFailed = true;
                    Time.timeScale = 0;
                    KingdomIdle.UGUI.UIManager.Instance?.ShowToast("서버 스테이지로 이동하지 못했습니다. 재접속해 주세요.");
                }
                OnLoopModeChanged?.Invoke(_isLoopMode);
            }

            void RefreshConfirmedStage(string reason)
            {
                if (!IsCurrent()) return;
                // 이동 요청을 다시 보내면 부분 처치를 지울 수 있으므로 상태 조회만 한다.
                Debug.LogWarning("[Stage] 이동 결과를 서버에서 확인합니다. " + reason);
                user.RefreshFromServer(ok =>
                {
                    if (!IsCurrent()) return;
                    if (!ok) { Stop("스테이지 이동 결과를 확인하지 못했습니다. 재접속해 주세요."); return; }
                    ResumeConfirmedStage();
                });
            }

            if (FlushPendingHunts(ok =>
            {
                if (!IsCurrent()) return;
                if (!ok) { Stop("전투 보상 정산을 확인하지 못해 이동을 중단했습니다. 재접속해 주세요."); return; }
                // 정산 중 갱신될 수 있으므로 FromStage는 큐가 모두 끝난 뒤 읽는다.
                fromStage = LocalProgression.State.MainStage;
                _currentState = eStageRunState.Entering;
                Time.timeScale = 0;
                network.Call<OnChangeMainStageRequestDTO, OnChangeMainStageResponseDTO>(
                    new OnChangeMainStageRequestDTO
                    {
                        FromStage = (Protocol.Message.Data.eStage)fromStage,
                        TargetStage = (Protocol.Message.Data.eStage)(long)target
                    },
                    result =>
                    {
                        if (!IsCurrent()) return;
                        if (result.StageState == null || (long)result.StageState.CurrentStage != (long)target ||
                            !LocalProgression.Execute("server-main-stage", state => ApplyMainStageState(state, result.StageState) &&
                                MainWaveProgression.ApplyServerState(state, result.MainBattle)))
                        {
                            RefreshConfirmedStage("이동 응답을 적용하지 못했습니다.");
                            return;
                        }
                        ResumeConfirmedStage();
                    },
                    error => RefreshConfirmedStage(NetworkManager.DescribeFailure(error, "스테이지 이동")));
            })) return true;

            _mainStageRequestPending = false;
            return false;
        }

        /// <summary>현재 위치·주기 최고 위치·영구 클리어를 서버 응답으로 함께 확정한다.</summary>
        private static bool ApplyMainStageState(ProgressionState state, UserStageQuery stage)
        {
            if (state == null || !IsValidMainStageState(stage)) return false;
            state.MainStage = (long)stage.CurrentStage;
            state.CycleFrontierStage = (long)stage.HighestStage;
            state.CycleBossStage = UserManager.CycleBossClears(state.CycleFrontierStage);
            state.HighestMainClear = (long)stage.MainClear;
            return true;
        }

        private static bool IsValidMainStageState(UserStageQuery stage)
        {
            bool Valid(long value)
            {
                var id = (eStage)value;
                int chapter = StageParser.GetStageNumber(id), wave = StageParser.GetWaveNumber(id);
                return value > 0 && chapter > 0 && wave >= 1 && wave <= 11 &&
                    StageParser.GetStageType(id) == eStageType.Main &&
                    (long)StageParser.MakeStage(eStageType.Main, chapter, wave) == value;
            }
            if (stage == null) return false;
            long current = (long)stage.CurrentStage, highest = (long)stage.HighestStage, clear = (long)stage.MainClear;
            return Valid(current) && Valid(highest) && current <= highest && (clear == 0 || Valid(clear));
        }
    }
}
