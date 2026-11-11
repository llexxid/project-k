using System;
using System.Collections.Generic;
using System.Linq;
using Core.Stage;
using KingdomIdle.Balance;
using PlayFab;
using Protocol.Message.Dto;
using Scripts.Core;
using UnityEngine;

namespace Scripts.Core.Manager
{
    public partial class StageManager
    {
        private float _nextHuntSendAt;
        private string _mainRunForSession;
        private bool _recoveringMainCompletion;
        private bool IsMainDefeatSettlement => _currentState == eStageRunState.DefeatPending ||
            (_currentSession?.HasPendingResult == true && _currentSession.PendingResult.Action == eStageFlowAction.AwaitDefeatChoice);

        private bool OwnsHuntResponse(HuntOutboxItem item, NetworkManager network, long generation, long networkGeneration,
            System.Threading.CancellationTokenSource token) => this != null && NetworkManager.Instance == network && network != null &&
            network.AccountGeneration == networkGeneration && LocalProgression.AccountGeneration == generation &&
            ReferenceEquals(_token, token) && _huntOutbox.Count > 0 && ReferenceEquals(_huntOutbox.Peek(), item);

        private void PumpHuntRetry()
        {
            if (_nextHuntSendAt > 0f && Time.realtimeSinceStartup >= _nextHuntSendAt)
            {
                _nextHuntSendAt = 0f;
                TrySendNextHunt();
            }
        }

        private bool BindMainWaveRun(StageDefinition definition)
        {
            _mainRunForSession = null;
            if (definition.Type != eStageType.Main || !MainWaveProgression.Available) return true;
            var battle = LocalProgression.State.MainBattle;
            if (battle == null || battle.Stage != (long)definition.Id)
            {
                StopMainSettlement("서버의 메인 회차를 확인하지 못했습니다. 재접속해 주세요.");
                return false;
            }
            _mainRunForSession = battle.RunId;
            return true;
        }

        private void StopMainSettlement(string message)
        {
            _huntInFlight = false;
            _huntSyncFailed = true;
            _nextHuntSendAt = 0f;
            FinishHuntSettlement(false);
            Time.timeScale = 0f;
            Debug.LogError("[MainWave] " + message);
            KingdomIdle.UGUI.UIManager.Instance?.ShowToast(message);
        }

        private bool ResumePendingMainCompletion()
        {
            var pending = LocalProgression.State.PendingMainCompletion;
            if (pending == null) return false;
            EndSession();
            _currentState = eStageRunState.Resolving;
            Time.timeScale = 0f;
            if (!MainWaveProgression.Available)
            {
                StopMainSettlement("미확정 메인 정산을 확인할 서버 연결이 필요합니다. 재접속해 주세요.");
                return true;
            }
            _recoveringMainCompletion = true;
            _huntOutbox.Enqueue(new HuntOutboxItem
            {
                IsClear = true, StageId = pending.Stage, MainRunId = pending.RunId,
                Completion = pending.Copy(), PresentNextAfter = Time.realtimeSinceStartup
            });
            TrySendNextHunt();
            return true;
        }

        private bool FlushMainWaveHunts(bool explicitSettlement = false)
        {
            bool stateAllowed = _currentState == eStageRunState.Running || (explicitSettlement &&
                (_currentState == eStageRunState.DefeatPending || _currentState == eStageRunState.Resolving));
            if (_mainRunForSession == null || _currentSession == null || _huntOutbox.Count != 0 ||
                _currentSession.Definition.Type != eStageType.Main || !stateAllowed ||
                (_currentSession.IsCleared && !(explicitSettlement && IsMainDefeatSettlement)) ||
                (!explicitSettlement && _currentSession.HasPendingResult) ||
                LocalProgression.State.PendingMainCompletion != null) return false;
            var battle = LocalProgression.State.MainBattle;
            if (!MainWaveProgression.Available || battle == null || battle.RunId != _mainRunForSession ||
                battle.Stage != (long)_currentSession.Definition.Id)
            {
                StopMainSettlement("서버의 사냥 회차가 달라져 정산을 중단했습니다. 재접속해 주세요.");
                return false;
            }
            var delta = MainWaveProgression.Delta(_currentSession.KillCounts, battle);
            if (delta.Count == 0) return false;
            _huntOutbox.Enqueue(new HuntOutboxItem
            {
                StageId = battle.Stage, MainRunId = battle.RunId, BatchId = Guid.NewGuid().ToString("N"), Loots = delta
            });
            return true;
        }

        private void EnqueueMainCompletion(StageDefinition definition, bool showResult)
        {
            var battle = LocalProgression.State.MainBattle;
            if (!MainWaveProgression.Available || battle == null || battle.RunId != _mainRunForSession ||
                battle.Stage != (long)definition.Id || _currentSession?.Definition.Id != definition.Id)
            {
                StopMainSettlement("완료할 메인 회차를 확인하지 못했습니다. 재접속해 주세요.");
                return;
            }
            MainStageRule.GetNextWave(definition.Id, out eStage next);
            Debug.Log($"[MainWave] ready stage=0x{(long)definition.Id:X} realtime={Time.realtimeSinceStartupAsDouble:F3}");
            bool saved = LocalProgression.Execute("main-completion-request", state =>
            {
                if (state.PendingMainCompletion != null)
                    return state.PendingMainCompletion.RunId == battle.RunId && state.PendingMainCompletion.Stage == battle.Stage;
                state.PendingMainCompletion = new MainWaveCompletionSave
                {
                    RunId = battle.RunId, RequestId = Guid.NewGuid().ToString("N"), Stage = battle.Stage,
                    NextAction = (int)(ShouldRestart(next) ? Protocol.Message.Data.eStageClearAction.Repeat : Protocol.Message.Data.eStageClearAction.Advance),
                    TotalKills = _currentSession.KillCounts.ToDictionary(pair => (ulong)pair.Key, pair => pair.Value)
                };
                return true;
            });
            if (!saved)
            {
                StopMainSettlement("메인 완료 요청을 저장하지 못했습니다. 저장 공간 확인 후 재접속해 주세요.");
                return;
            }
            if (_huntOutbox.Any(item => item.Completion?.RequestId == LocalProgression.State.PendingMainCompletion.RequestId)) return;
            // 이미 전송한 부분 정산은 FIFO로 기다린다. 미전송분은 누적 완료 요청 하나가 대체한다.
            var queued = _huntOutbox.ToArray();
            _huntOutbox.Clear();
            for (int i = 0; i < queued.Length; i++)
                if ((i == 0 && _huntInFlight) || queued[i].MainRunId != battle.RunId) _huntOutbox.Enqueue(queued[i]);
            _huntBuffer.Remove(battle.Stage);
            var pending = LocalProgression.State.PendingMainCompletion;
            _huntOutbox.Enqueue(new HuntOutboxItem
            {
                IsClear = true, StageId = pending.Stage, MainRunId = pending.RunId, Completion = pending.Copy(),
                ShowResult = showResult, PresentNextAfter = Time.realtimeSinceStartup + MainWaveClearHoldSeconds
            });
            _nextHuntSendAt = 0f;
            TrySendNextHunt();
        }

        private void OnMainCompletionSuccess(HuntOutboxItem item, OnCompleteMainWaveResponseDTO result)
        {
            _huntInFlight = false;
            var pending = LocalProgression.State.PendingMainCompletion;
            if (pending == null || pending.RequestId != item.Completion.RequestId || result.RequestId != pending.RequestId ||
                result.CompletedRunId != pending.RunId || result.MainBattle == null || result.MainBattle.RunId == pending.RunId ||
                !IsValidMainStageState(result.StageState) || result.MainBattle.Stage != result.StageState.CurrentStage)
            {
                OnMainWaveError(item, NetworkManager.CreateError("InvalidResponse", "메인 완료 응답의 회차를 확인하지 못했습니다."));
                return;
            }
            MainStageRule.GetNextWave((eStage)pending.Stage, out var expected);
            long unlocked = (long)expected;
            if ((long)result.StageState.MainClear < pending.Stage || (long)result.StageState.HighestStage < unlocked)
            {
                OnMainWaveError(item, NetworkManager.CreateError("InvalidResponse", "메인 완료 기록을 확인하지 못했습니다."));
                return;
            }
            if (pending.NextAction == (int)Protocol.Message.Data.eStageClearAction.Repeat) expected = (eStage)pending.Stage;
            if (!result.Replayed && (long)result.StageState.CurrentStage != (long)expected)
            {
                OnMainWaveError(item, NetworkManager.CreateError("InvalidResponse", "메인 완료 응답의 다음 위치가 다릅니다."));
                return;
            }
            if (result.Replayed)
            {
                RefreshMainWaveState(item);
                return;
            }
            var session = !_recoveringMainCompletion && _currentSession?.Definition.Id == (eStage)item.StageId ? _currentSession : null;
            bool saved = LocalProgression.Execute("server-main-complete", state =>
            {
                if (session != null && !BattleEconomy.ApplyConfirmedClear(state, session)) return false;
                UserManager.ApplyCurrency(state, result.Currency);
                UserManager.ApplyEquipment(state, result.Equipment);
                UserManager.ApplyMageTower(state, result.MageTowerState);
                state.AccountLevel = BalanceMath.Clamp(result.Level, 1, BalanceMath.AccountCap);
                state.Experience = Math.Max(0, result.Exp);
                BalanceMath.GainExperience(ref state.AccountLevel, ref state.Experience, 0);
                state.Kills = Math.Max(0, result.KillScore);
                if (!ApplyMainStageState(state, result.StageState) || !MainWaveProgression.ApplyServerState(state, result.MainBattle)) return false;
                state.PendingMainCompletion = null;
                return true;
            });
            Debug.Log($"[MainWave] complete stage=0x{item.StageId:X} realtime={Time.realtimeSinceStartupAsDouble:F3} replayed={result.Replayed} next=0x{(long)result.StageState.CurrentStage:X} saved={saved}");
            if (!saved)
            {
                OnMainWaveError(item, NetworkManager.CreateError("ApplyFailed", "메인 완료 결과를 저장하지 못했습니다."));
                return;
            }
            _recoveringMainCompletion = false;
            _saturatedTypes.Remove(item.StageId);
            EquipmentManager.Instance?.RestoreEquipment();
            KingdomIdle.MageTower.MageTowerManager.Instance?.NotifyCommitted();
            if (EquipmentManager.IsInstanceInventory && LocalProgression.State.Equipment.Count >= EquipmentManager.InventoryCapacity &&
                result.GrantedRewards.Any(reward => reward != null && reward.Kind == RewardKind.Equipment))
                GameManager.Instance?.Mailbox?.RefreshAfterDelivery("main-completion-overflow");
            QuestManager.Instance?.RequestRefresh("clear");
            if (session != null)
            {
                UpdateMaxClearedStage(session.Definition);
                OnStageCleared?.Invoke(session.Definition);
            }
            if (item.ShowResult && session != null) EnterResultPending();
            else if (!TransitionStage((eStage)(long)result.StageState.CurrentStage, item.PresentNextAfter))
            {
                StopMainSettlement("확정된 메인 위치로 이동하지 못했습니다. 재접속해 주세요.");
                return;
            }
            if (session != null) RubyProgression.OnWaveCleared(session.Definition);
            Dequeue(item);
            TrySendNextHunt();
        }

        private void OnMainWaveError(HuntOutboxItem item, PlayFabError error)
        {
            _huntInFlight = false;
            int code = NetworkManager.TryGetBusinessError(error, out var business) ? (int)business.ErrorCode : 0;
            string kind = NetworkManager.GetFailureKind(error);
            bool retry = code == 20 || code == 1014 || code == 1017 || kind == "Timeout" || kind == "Transport";
            if (retry && item.Completion == null && _huntOutbox.Any(next => next.Completion != null && next.MainRunId == item.MainRunId && next.StageId == item.StageId))
            {
                // 누적 완료가 같은 회차의 부분 요청까지 정산한다. 늦은 응답은 outbox 소유권 검사에서 무시한다.
                Dequeue(item);
                _nextHuntSendAt = 0f;
                Debug.LogWarning($"[MainWave] cumulative completion replaces uncertain partial stage=0x{item.StageId:X} code={code} kind={kind}");
                TrySendNextHunt();
                return;
            }
            if (retry && item.Attempts < HuntMaxAttempts)
            {
                _nextHuntSendAt = Time.realtimeSinceStartup + Math.Min(3f, .5f * item.Attempts);
                Debug.LogWarning($"[MainWave] same-request retry stage=0x{item.StageId:X} code={code} kind={kind} attempt={item.Attempts}");
                return;
            }
            // 새 회차의 1007을 분할·폐기하지 않는다. 현재 서버 회차를 확인하고 불확정 요청은 보존한다.
            RefreshMainWaveState(item);
        }

        private void RefreshMainWaveState(HuntOutboxItem item)
        {
            var network = NetworkManager.Instance;
            var user = UserManager.Instance;
            long generation = LocalProgression.AccountGeneration, networkGeneration = network != null ? network.AccountGeneration : -1;
            var token = _token;
            float previousTimeScale = Time.timeScale;
            _huntInFlight = true;
            Time.timeScale = 0f;
            if (user == null) { StopMainSettlement("메인 정산 상태를 조회하지 못했습니다. 재접속해 주세요."); return; }
            user.RefreshFromServer(ok =>
            {
                if (this == null || generation != LocalProgression.AccountGeneration || NetworkManager.Instance != network ||
                    network == null || network.AccountGeneration != networkGeneration || !ReferenceEquals(_token, token)) return;
                _huntInFlight = false;
                var state = LocalProgression.State;
                if (!ok || !MainWaveProgression.Available || state.MainBattle == null || !string.IsNullOrEmpty(state.ActiveDungeon))
                { StopMainSettlement("메인 정산 상태를 확인하지 못했습니다. 재접속해 주세요."); return; }
                if (state.MainBattle.RunId == item.MainRunId)
                {
                    if (item.Completion != null)
                    { StopMainSettlement("완료되지 않은 메인 정산을 보존했습니다. 재접속해 주세요."); return; }
                    var delta = _currentSession != null ? MainWaveProgression.Delta(_currentSession.KillCounts, state.MainBattle) : null;
                    if (delta == null || (delta.Count == item.Loots.Count && delta.SequenceEqual(item.Loots)))
                    { StopMainSettlement("부분 정산을 확인하지 못해 기존 요청을 보존했습니다. 재접속해 주세요."); return; }
                    Dequeue(item);
                    _nextHuntSendAt = 0f;
                    ResumeMainHuntAfterRefresh(previousTimeScale);
                    return;
                }
                // 과거 요청으로 현재 잔액·위치를 되돌리지 않고 조회로 확정한 서버 상태를 사용한다.
                if (!LocalProgression.Execute("main-completion-confirmed-state", s => { s.PendingMainCompletion = null; return true; }))
                { StopMainSettlement("확정된 메인 상태를 저장하지 못했습니다. 재접속해 주세요."); return; }
                _huntOutbox.Clear(); _huntBuffer.Clear(); _saturatedTypes.Clear();
                _recoveringMainCompletion = false; _mainRunForSession = null; _nextHuntSendAt = 0f;
                FinishHuntSettlement(false);
                _huntSyncFailed = false;
                _currentState = eStageRunState.Resolving;
                if (!TransitionStage((eStage)state.MainStage)) StopMainSettlement("서버의 현재 위치로 이동하지 못했습니다. 재접속해 주세요.");
            });
        }

        private void ResumeMainHuntAfterRefresh(float previousTimeScale)
        {
            Time.timeScale = _onHuntsSettled != null ? 0f : previousTimeScale;
            TrySendNextHunt();
        }
    }
}
