using System;
using System.Collections;
using System.Collections.Generic;
using Scripts.Core.Manager;
using UnityEngine;

namespace Reincarnation
{
    public class ReincarnationService
    {
        public event Action<ReincarnationState> ReincarnationStateChanged;
        public ReincarnationState CurrentState => _currentState;
        
        private readonly ReincarnationPolicy _policy;
        private readonly IReincarnationProgressStore _store;
        private readonly IReincarnationStageGateway _stageGateway ;

        private bool _isProcessing;
        private ReincarnationState _currentState;
        private ReincarnationExecutionResult _errorCode;
        
        public ReincarnationService(ReincarnationPolicy policy, IReincarnationProgressStore store, IReincarnationStageGateway gateway)
        {
            _policy = policy;
            _store = store;
            _stageGateway = gateway;
            _errorCode = ReincarnationExecutionResult.None;
            //맨 처음 실행시 저장소에서 기존 환생 데이터가 존재하는지 확인
            ReincarnationLoadResponse response = store.Load();
            
            switch (response.Result)
            {
                case ReincarnationLoadResult.Success: //데이터 탐색 성공시 그대로 사용
                    _currentState = response.State;
                    break;
                case ReincarnationLoadResult.NotFound: //데이터가 없거나 형식이 깨졌으면 처음 시작으로 간주
                    _currentState = new ReincarnationState(0, 0);
                    break;
                case ReincarnationLoadResult.Corrupted:
                    _currentState = new ReincarnationState(0, 0);
                    break;
                case ReincarnationLoadResult.UnsupportedVersion: //버전이 맞지 않으면 오류선언
                    _errorCode = ReincarnationExecutionResult.UnsupportedSaveVersion;
                    break;
                case ReincarnationLoadResult.StorageError: //저장소 오류 발생시 오류 선언
                    _errorCode = ReincarnationExecutionResult.StorageUnavailable;
                    break;
            }
        }

        public ReincarnationPreview GetPreview()
        {
            // TODO(서버 연동):
            // 현재 Preview에는 저장소·서버 사용 불가 상태와 지원하지 않는 데이터 버전이 반영되지 않는다.
            // UI의 환생 가능 여부와 TryReincarnate의 실행 결과가 일치하도록 추후 보완해야 한다.
            ReincarnationStageSnapshot snapshot = _stageGateway.GetSnapshot();
            if (!snapshot.IsAvailable || !snapshot.IsMainStage)
                return new ReincarnationPreview(false, eReincarnationFailureReason.NotMainStage);
            if (!snapshot.IsRunning)
                return new ReincarnationPreview(false, eReincarnationFailureReason.StateIsNotRunning);
            
            ReincarnationPreview preview = _policy.Evaluate(_currentState, snapshot.IsMainStage, snapshot.StageNumber);
            return preview;
        }

        public ReincarnationExecutionResult TryReincarnate()
        {
            if (_errorCode != ReincarnationExecutionResult.None)
            {
                return _errorCode;
            }
            if (_isProcessing) return ReincarnationExecutionResult.AlreadyProcessing;
            _isProcessing = true;
            
            try
            {
                //preview 계산
                ReincarnationPreview preview = GetPreview();

                //이전상태 보관
                ReincarnationState _prevState = _currentState;
                
                // TODO(UI/서버 연동):
                // 현재는 모든 환생 불가 사유를 StageResetRejected로 반환한다.
                // 추후 Preview.FailureReason에 따라 실행 결과를 구분하도록 보완해야 한다.
                if (!preview.CanReincarnate)
                    return ReincarnationExecutionResult.StageResetRejected; //차후 Preview의 FailureReason에 따라 분기시키기
                //저장소에 NextState 저장
                if (!_store.TrySave(preview.NextState))
                    return ReincarnationExecutionResult.SaveFailed;
                
                // TODO(서버 연동):
                // 현재 true는 Stage1_1 전환 요청이 수락됐다는 의미이며 비동기 로딩 완료를 보장하지 않는다.
                // 추후 실제 전환 완료를 확인한 뒤 환생 트랜잭션을 확정하도록 변경해야 한다.
                //Gateway에 스테이지 리셋 요청
                if (!_stageGateway.TryResetToStartStage())
                {
                    if (!_store.TrySave(_prevState))
                    {
                        _errorCode = ReincarnationExecutionResult.RollbackFailed;
                        Debug.LogError("[Reincarnation] 저장 롤백 실패. 환생 기능을 중단합니다.");

                        return ReincarnationExecutionResult.RollbackFailed;
                    }
                    
                    return ReincarnationExecutionResult.StageResetRejected;
                }
                //스테이지 리셋 성공 시 현재 State 갱신
                // TODO(서버 연동):
                // 현재 스테이지 이벤트는 UserManager의 메모리 상태만 변경한다.
                // 추후 환생 상태와 시작 스테이지(Stage1_1)를 하나의 서버 트랜잭션으로 저장해야 한다.
                _currentState = preview.NextState;
                ReincarnationStateChanged?.Invoke(_currentState);
                return ReincarnationExecutionResult.None;
            }
            finally
            {
                _isProcessing = false;
            }
            
        }
    }
}