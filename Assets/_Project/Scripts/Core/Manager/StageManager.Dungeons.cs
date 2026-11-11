using System;
using System.Collections.Generic;
using KingdomIdle.Balance;
using KingdomIdle.MageTower;
using Protocol.Message.Dto;
using Scripts.Core;
using UnityEngine;

namespace Scripts.Core.Manager
{
    public partial class StageManager
    {
        private Action<bool> _onHuntsSettled;
        private float _beforeHuntSettlement;
        private bool _huntSyncFailed, _dungeonRequestPending;
        private Action _retryDungeon;
        private float _retryDungeonAt;
        private long _dungeonGeneration;

        /// <summary>전투를 멈추고 기존 처치·클리어 응답까지 기다린다. 실패하면 후속 거래를 실행하지 않는다.</summary>
        public bool FlushPendingHunts(Action<bool> onDone)
        {
            if (_onHuntsSettled != null || _huntSyncFailed || _huntsHeld || onDone == null ||
                NetworkManager.Instance == null || !NetworkManager.Instance.IsGameDataReady) return false;
            _beforeHuntSettlement = Time.timeScale;
            Time.timeScale = 0;
            _onHuntsSettled = onDone;
            FlushMainWaveHunts(true);
            foreach (long stage in new List<long>(_huntBuffer.Keys)) FlushHuntBuffer(stage);
            TrySendNextHunt();
            return true;
        }

        private void FinishHuntSettlement(bool success)
        {
            var callback = _onHuntsSettled;
            if (callback == null) return;
            _onHuntsSettled = null;
            Time.timeScale = _beforeHuntSettlement;
            callback(success);
        }

        private bool IsCurrentDungeonRequest => this != null && _dungeonRequestPending &&
            _dungeonGeneration == LocalProgression.AccountGeneration;

        private void RetryDungeonRequest()
        {
            DungeonTicketRefresh.RequestIfDue();
            if (_retryDungeon == null || !IsCurrentDungeonRequest || Time.realtimeSinceStartup < _retryDungeonAt) return;
            var retry = _retryDungeon;
            _retryDungeon = null;
            retry();
        }

        private void WaitForDungeonRetry(Action retry, string message)
        {
            if (!IsCurrentDungeonRequest) return;
            Time.timeScale = 0;
            _retryDungeon = retry;
            _retryDungeonAt = Time.realtimeSinceStartup + 5f;
            Debug.LogWarning("[Dungeon] " + message);
            KingdomIdle.UGUI.UIManager.Instance?.ShowToast("서버의 던전 상태를 확인하고 있습니다.");
        }

        private void DungeonRequestFailed(string message)
        {
            _dungeonRequestPending = false;
            _retryDungeon = null;
            Debug.LogWarning("[Dungeon] " + message);
            KingdomIdle.UGUI.UIManager.Instance?.ShowToast(message);
        }

        private bool BeginServerDungeon(eStage target)
        {
            if (_dungeonRequestPending || _mainStageRequestPending || !TryGetStageDefinition(target, out var definition) || !definition.Enabled ||
                definition.Type == eStageType.Main || UserManager.Instance == null) return false;
            _dungeonRequestPending = true;
            _dungeonGeneration = LocalProgression.AccountGeneration;
            var previousState = _currentState;
            float previousTime = Time.timeScale;
            void Reject(string message)
            {
                DungeonRequestFailed(message);
                _currentState = previousState;
                Time.timeScale = previousTime;
                if (previousState == eStageRunState.DefeatPending && string.IsNullOrEmpty(LocalProgression.State.ActiveDungeon))
                    ReturnToMainStage();
            }
            void StartAcceptedRun()
            {
                _saturatedTypes.Remove((long)target);
                _dungeonRequestPending = false;
                TransitionStage(target);
            }
            void CheckEntry(bool mayEnter)
            {
                if (!IsCurrentDungeonRequest) return;
                UserManager.Instance.RefreshFromServer(ok =>
                {
                    if (!IsCurrentDungeonRequest) return;
                    if (!ok) { WaitForDungeonRetry(() => CheckEntry(mayEnter), "입장 상태 조회 실패"); return; }
                    var state = LocalProgression.State;
                    if (_mainStageSnapshot != null) _mainStageSnapshot.StageId = (eStage)state.MainStage;
                    if (!string.IsNullOrEmpty(state.ActiveDungeon))
                    {
                        if (state.ServerDungeonId == (long)target) StartAcceptedRun();
                        else Reject("다른 던전 전투가 남아 있습니다. 재접속해 주세요.");
                        return;
                    }
                    if (!mayEnter) { Reject("던전에 입장하지 못했습니다. 입장권과 입장 조건을 확인해 주세요."); return; }
                    NetworkManager.Instance.Call<OnEnterDungeonRequestDTO, OnEnterDungeonResponseDTO>(
                        new OnEnterDungeonRequestDTO { DungeonId = (long)target },
                        response =>
                        {
                            if (!IsCurrentDungeonRequest) return;
                            // Enter는 재전송하면 티켓을 다시 쓸 수 있다. 조회로 전투 ID와 티켓을 확정한다.
                            if (response.DungeonId != (long)target || string.IsNullOrWhiteSpace(response.BattleId))
                                Debug.LogWarning("[Dungeon] 입장 응답이 요청과 일치하지 않아 서버 상태를 조회합니다.");
                            CheckEntry(false);
                        },
                        error => { if (IsCurrentDungeonRequest) CheckEntry(false); });
                });
            }
            if (!FlushPendingHunts(ok =>
            {
                if (!IsCurrentDungeonRequest) return;
                if (!ok) { Reject("전투 보상 정산을 확인하지 못해 입장을 중단했습니다. 재접속해 주세요."); return; }
                _currentState = eStageRunState.Entering;
                Time.timeScale = 0;
                CheckEntry(true);
            }))
            {
                Reject("전투 보상 정산을 확인한 뒤 입장할 수 있습니다. 재접속해 주세요.");
                return false;
            }
            return true;
        }

        private void CompleteServerDungeon(StageSession session)
        {
            if (_dungeonRequestPending) return;
            _dungeonRequestPending = true;
            _dungeonGeneration = LocalProgression.AccountGeneration;
            string battleId = LocalProgression.State.ActiveDungeon;
            long dungeonId = (long)session.Definition.Id;
            void Complete()
            {
                if (!IsCurrentDungeonRequest) return;
                NetworkManager.Instance.Call<OnCompleteDungeonRequestDTO, OnCompleteDungeonResponseDTO>(
                    new OnCompleteDungeonRequestDTO { DungeonId = dungeonId, BattleId = battleId },
                    result =>
                    {
                        if (!IsCurrentDungeonRequest) return;
                        bool saved = LocalProgression.Execute("server-dungeon-complete", s =>
                        {
                            if (!UserManager.ApplyDungeonState(s, result.DungeonState) || result.DungeonState.ActiveBattleId != null ||
                                result.Currency == null || result.TotalRunRewards == null) return false;
                            UserManager.ApplyCurrency(s, result.Currency);
                            UserManager.ApplyEquipment(s, result.Equipment);
                            UserManager.ApplyMageTower(s, result.MageTowerState);
                            s.LastDungeonGold = s.LastDungeonRuby = 0;
                            foreach (var reward in result.TotalRunRewards)
                            {
                                if (reward == null || reward.Amount < 0 || reward.Amount > long.MaxValue) return false;
                                if (reward.Kind != RewardKind.Currency || !reward.CurrencyType.HasValue) continue;
                                if (reward.CurrencyType.Value == Protocol.Message.Data.eCurrency.Gold) s.LastDungeonGold = checked(s.LastDungeonGold + (long)reward.Amount);
                                if (reward.CurrencyType.Value == Protocol.Message.Data.eCurrency.Ruby) s.LastDungeonRuby = checked(s.LastDungeonRuby + (long)reward.Amount);
                            }
                            s.LastClearedBattle = session.RunId;
                            return true;
                        });
                        if (!saved) { WaitForDungeonRetry(Complete, "클리어 결과 저장 실패"); return; }
                        _dungeonRequestPending = false;
                        EquipmentManager.Instance?.RestoreEquipment();
                        MageTowerManager.Instance?.NotifyCommitted();
                        QuestManager.Instance?.RequestRefresh("dungeon-clear");
                        OnStageCleared?.Invoke(session.Definition);
                        EnterResultPending();
                    },
                    error => HandleDungeonSettlementError(error, Complete));
            }
            if (!FlushPendingHunts(ok =>
            {
                if (!IsCurrentDungeonRequest) return;
                Time.timeScale = 0;
                if (ok) Complete();
                else DungeonRequestFailed("던전 처치 정산을 확인하지 못했습니다. 재접속해 주세요.");
            }))
            {
                Time.timeScale = 0;
                DungeonRequestFailed("던전 처치 정산을 확인하지 못했습니다. 재접속해 주세요.");
            }
        }

        private void ExitServerDungeon(Action onExited)
        {
            if (_dungeonRequestPending) return;
            _dungeonRequestPending = true;
            _dungeonGeneration = LocalProgression.AccountGeneration;
            string battleId = LocalProgression.State.ActiveDungeon;
            var reason = _currentState == eStageRunState.DefeatPending ? DungeonExitReason.Failed : DungeonExitReason.Abandoned;
            void Exit()
            {
                if (!IsCurrentDungeonRequest) return;
                NetworkManager.Instance.Call<OnExitDungeonRequestDTO, OnExitDungeonResponseDTO>(
                    new OnExitDungeonRequestDTO { BattleId = battleId, Reason = reason },
                    result =>
                    {
                        if (!IsCurrentDungeonRequest) return;
                        bool saved = LocalProgression.Execute("server-dungeon-exit", s =>
                            UserManager.ApplyDungeonState(s, result.DungeonState) && result.DungeonState.ActiveBattleId == null);
                        if (!saved) { WaitForDungeonRetry(Exit, "퇴장 결과 저장 실패"); return; }
                        _dungeonRequestPending = false;
                        onExited();
                    },
                    error => HandleDungeonSettlementError(error, Exit));
            }
            if (!FlushPendingHunts(ok =>
            {
                if (!IsCurrentDungeonRequest) return;
                Time.timeScale = 0;
                if (ok) Exit();
                else DungeonRequestFailed("던전 보상 정산을 확인하지 못했습니다. 재접속해 주세요.");
            })) DungeonRequestFailed("던전 보상 정산을 확인하지 못했습니다. 재접속해 주세요.");
        }

        private void HandleDungeonSettlementError(PlayFab.PlayFabError error, Action retry)
        {
            if (!IsCurrentDungeonRequest) return;
            int code = NetworkManager.TryGetBusinessError(error, out var business) ? (int)business.ErrorCode : 0;
            string kind = NetworkManager.GetFailureKind(error);
            // Complete/Exit는 같은 마지막 BattleId에 대해 멱등이다. 다음 입장 전까지만 재시도한다.
            if ((code == 0 && (string.IsNullOrEmpty(kind) || kind == "Timeout" || kind == "Transport")) ||
                code == 20 || code == 1014 || code == 1017)
                WaitForDungeonRetry(retry, error?.ErrorMessage);
            else
            {
                Time.timeScale = 0;
                DungeonRequestFailed("던전 정산을 확인하지 못했습니다. 재접속해 주세요. (" + code + ")");
            }
        }
    }
}
