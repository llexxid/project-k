using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using Scripts.Core;
using Scripts.Core.Manager;
using Scripts.Core.Utils;
using Scripts.Monster;
using Debug = UnityEngine.Debug;

namespace Core.Stage
{
    public enum eStageFlowAction
    {
        None,
        MoveToStage,      // 다음 Stage로 이동
        RestartStage,     // 현재 Stage 재시작
        ShowResult,       // 결과 팝업 표시
        AwaitDefeatChoice, //사망 팝업
        ReturnToMainStage,     // 메인 콘텐츠 복귀
    }
    public struct StageRuleResult
    {
        public eStageFlowAction Action { get; }
        public eStage? TargetStage { get; } //Action = MoveToStage일때 사용

        StageRuleResult(eStageFlowAction action, eStage? target = null)
        {
            Action = action;
            TargetStage = target;
        }
        
        public static StageRuleResult None =>new(eStageFlowAction.None);

        public static StageRuleResult MoveTo(eStage target) => new(eStageFlowAction.MoveToStage, target);

        public static StageRuleResult Restart => new(eStageFlowAction.RestartStage);

        public static StageRuleResult ShowResult => new(eStageFlowAction.ShowResult);

        public static StageRuleResult AwaitDefeatChoice => new(eStageFlowAction.AwaitDefeatChoice);

        public static StageRuleResult ReturnToMain =>new(eStageFlowAction.ReturnToMainStage);
        
        public static StageRuleResult FromDefeatAction(
            eStageFlowAction action)
        {
            return action switch
            {
                eStageFlowAction.RestartStage => Restart,
                eStageFlowAction.AwaitDefeatChoice => AwaitDefeatChoice,
                eStageFlowAction.ReturnToMainStage => ReturnToMain,
                _ => throw new ArgumentOutOfRangeException(nameof(action))
            };
        }
    }
    public interface IStageRule
    {
        void Enter(StageSession session);
        StageRuleResult OnMonsterKilled(
            StageSession session,
            Monster monster);
        StageRuleResult Tick(
            StageSession session,
            float deltaTime);
        StageRuleResult OnPartyDefeated(StageSession session);
        void Exit(StageSession session);
    }

    public sealed class MainStageRule : IStageRule
    {
        private bool _hasTimeLimit;
        private float _remainTime;
        public void Enter(StageSession session)
        {
            _hasTimeLimit = session.Definition.TimeLimitSec > 0;
            _remainTime = session.Definition.TimeLimitSec;
            
            Debug.Log("스테이지 시작");
        }

        public StageRuleResult OnMonsterKilled(StageSession session, Monster monster)
        {
            if (!session.IsCleared)
            {
                return StageRuleResult.None;
            }
            
            GetNextWave(session.Definition.Id, out eStage stage);
            
            return StageRuleResult.MoveTo(stage);
        }

        public StageRuleResult Tick(StageSession session, float deltaTime)
        {
            return StageRuleResult.None;

            /* 일단 stageManager에서 시간관리 하고 차후 이전
             if (!_hasTimeLimit) return new StageRuleResult(eStageFlowAction.None);
            _remainTime -= deltaTime;
            
            if (_remainTime > 0)
            {
                return new StageRuleResult(eStageFlowAction.None);
            }

            _hasTimeLimit = false;
            return OnPartyDefeated(session);
            */
        }

        public StageRuleResult OnPartyDefeated(StageSession session)
        {
            return StageRuleResult.AwaitDefeatChoice;
        }

        public void Exit(StageSession session)
        {
            _hasTimeLimit = false; //추가적인 시간부여없게 막아버림
        }

        #region MainStage Utility
        
        
        /// <summary>
        /// 현재 스테이지 값을 기준으로 다음 웨이브 또는 다음 스테이지를 계산한다.
        /// </summary>
        /// <param name="currentStage">현재 진행 중인 스테이지/웨이브 값.</param>
        /// <param name="result">계산된 다음 스테이지/웨이브 값.</param>
        /// <returns>
        /// 일반 웨이브 이동이면 <see cref="eStageResult.WaveChanged"/>,
        /// <br/>보스 웨이브 진입이면 <see cref="eStageResult.BossWaveEntered"/>,
        /// <br/>보스 웨이브 이후 다음 스테이지로 넘어가면 <see cref="eStageResult.StageChanged"/>를 반환한다.
        /// </returns>
        /// <remarks>
        /// 현재 규칙에서는 11웨이브를 보스 웨이브로 사용한다.
        /// <br/>10웨이브에서 다음으로 이동하면 보스 웨이브 진입,
        /// 11웨이브에서 다음으로 이동하면 다음 스테이지 1웨이브로 전환된다.
        /// </remarks>
        public static eStageResult GetNextWave(eStage currentStage, out eStage result)
        {
            ulong wave = ((ulong)currentStage & StageParser.WaveMask);
            if (wave == StageParser.BossWaveNumber)
            {
                ulong stageAdder = 0x0000000000010001; // 첫번째 스테이지로 가기위해 +1
                //기존 스테이지의 베이스 스테이지로 이동 후 다음 1스테이지로 이동
                result = (eStage)(((ulong)currentStage & StageParser.StageBaseMask) + stageAdder );
                return eStageResult.StageChanged;
            }
            result = (eStage)((ulong)++currentStage);
            return ((ulong)result & StageParser.WaveMask) == StageParser.BossWaveNumber ? eStageResult.BossWaveEntered : eStageResult.WaveChanged;
        }

        
        #endregion
    }

    public sealed class BossStageRule : IStageRule
    {
        private BossChallengeConfig _config;
        public void Enter(StageSession session)
        {
            Debug.Log($"BossStage 던전 진입 {session.Definition.Type}, {session.Definition.FlowConfig.ConfigId}");
            IStageFlowConfig flowConfig = session.Definition.FlowConfig;

            if (flowConfig is not BossChallengeConfig bossConfig)
            {
                string actualType =
                    flowConfig?.GetType().Name ?? "null";

                Debug.LogError(
                    "[BossStageRule] 잘못된 FlowConfig 타입입니다. " +
                    $"Expected: {nameof(BossChallengeConfig)}, " +
                    $"Actual: {actualType}");

                return;
            }

            _config = bossConfig;
        }

        public StageRuleResult OnMonsterKilled(StageSession session, Monster monster)
        {
            if (monster.Type == _config.BossMonsterType)
            {
                return StageRuleResult.ShowResult;
            }
            return StageRuleResult.None;
        }

        public StageRuleResult Tick(StageSession session, float deltaTime)
        {
            return StageRuleResult.None;
        }

        public StageRuleResult OnPartyDefeated(StageSession session)
        {
            return StageRuleResult.FromDefeatAction(_config.DefeatAction);
        }

        public void Exit(StageSession session)
        {
            Debug.Log($"BossStage 던전 퇴장 {session.Definition.Type}, {session.Definition.FlowConfig.ConfigId}");
        }
    }

    public sealed class KillCountRule : IStageRule
    {
        private KillCountChallengeConfig _config;
        public void Enter(StageSession session)
        {
            Debug.Log($"KillCountChallengeStage 던전 진입 {session.Definition.Type}, {session.Definition.FlowConfig.ConfigId}");
            IStageFlowConfig flowConfig = session.Definition.FlowConfig;

            if (flowConfig is not KillCountChallengeConfig killCountConfig)
            {
                string actualType =
                    flowConfig?.GetType().Name ?? "null";

                Debug.LogError(
                    "[KillCountStageRule] 잘못된 FlowConfig 타입입니다. " +
                    $"Expected: {nameof(KillCountChallengeConfig)}, " +
                    $"Actual: {actualType}");

                return;
            }

            _config = killCountConfig;
        }

        public StageRuleResult OnMonsterKilled(StageSession session, Monster monster)
        {
            int killCount = _config.TargetMonsterType.HasValue 
                ? session.GetKillCount(_config.TargetMonsterType.Value) //목표 몬스터가 있을 때는 해당 몬스터만 카운트
                : session.TotalKillCount; //목표 몬스터가 없을 때는 전체 카운트
            CustomLogger.Log($"목표 처치 수 : {_config.RequiredKillCount} / 현재 처치 수 : {killCount}");
            return killCount >= _config.RequiredKillCount 
                ? StageRuleResult.ShowResult 
                : StageRuleResult.None;
        }

        public StageRuleResult Tick(StageSession session, float deltaTime)
        {
            return StageRuleResult.None;
        }

        public StageRuleResult OnPartyDefeated(StageSession session)
        {
            return StageRuleResult.FromDefeatAction(_config.DefeatAction);
        }

        public void Exit(StageSession session)
        {
            Debug.Log($"BossStage 던전 퇴장 {session.Definition.Type}, {session.Definition.FlowConfig.ConfigId}");
        }
    }
}