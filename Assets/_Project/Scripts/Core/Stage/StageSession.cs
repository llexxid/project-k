using System;
using System.Collections.Generic;
using Core.Stage;
using Core.Stage.Action;
using Scripts.Core.inteface;
using Scripts.Core.Utils;
using Scripts.Monster;
using UnityEngine;
using UnityEngine.PlayerLoop;

namespace Scripts.Core
{
    using Monster = Scripts.Monster.Monster;

    /// <summary>
    /// 현재 실행 중인 한 웨이브의 몬스터와 클리어 상태를 관리한다.
    /// 몬스터 생성 위치 결정과 리소스 로딩은 외부 매니저가 담당한다.
    /// </summary>
    public sealed class StageSession
    {
        public event Action<StageSession, StageRuleResult> OnResultProduced;
        public event Action<StageSession, StageRuleResult> OnResultAccepted;
        public event Action<StageSession> OnBattleStarted;
        public event Action<StageSession, Exception> OnActionSequenceFailed;
        public event Action<StageSession, Monster> OnMonsterKilled;
        
        public StageDefinition Definition { get; }
        public bool IsRunning { get; private set; }
        public bool IsBattleRunning { get; private set; }
        public bool HasPendingResult { get; private set; }
        public StageRuleResult PendingResult { get; private set; }
        public bool IsCleared => _spawningCompleted && _monsters.Count <= 0;
        public int RemainingMonsterCount => _monsters.Count;
        
        private IStageRule _rule;
        private readonly HashSet<Monster> _monsters = new();
        private readonly Dictionary<eMonsterType, int> _killCounts = new();
        private readonly MonsterSpawner _monsterSpawner;
        private StageActionSequence _actionSequence;

        private bool _spawningCompleted;
        private bool _resultDispatched;
        private bool _actionFailureNotified;
        private int _totalKillCount;
        public IReadOnlyCollection<Monster> Monsters => _monsters;
        public IReadOnlyDictionary<eMonsterType, int> KillCounts => _killCounts;
        public int TotalKillCount => _totalKillCount;

        public StageSession(StageDefinition definition, IStageRule rule, MonsterSpawner monsterSpawner)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            _rule = rule ?? throw new ArgumentNullException(nameof(rule));
            _monsterSpawner = monsterSpawner ?? throw new ArgumentNullException(nameof(monsterSpawner));
        }
        #if UNITY_EDITOR
                public bool TestPublishResult(StageRuleResult result)
                {
                    if (!IsRunning)
                        return false;

                    AcceptResult(result);
                    return true;
                }
        #endif
        /// <summary>몬스터 등록을 받을 수 있도록 세션을 시작한다.</summary>
        public void Enter(StageActionSequence actionSequence)
        {
            _actionSequence = actionSequence ?? throw new ArgumentNullException(nameof(actionSequence));
            IsRunning = true;
            IsBattleRunning = false;
            HasPendingResult = false;
            PendingResult = StageRuleResult.None;
            _spawningCompleted = false;
            _resultDispatched = false;
            _actionFailureNotified = false;
            _killCounts.Clear();
            _totalKillCount = 0;
            
            _rule.Enter(this);
        }

        public void Tick(float deltaTime, float unscaledDeltaTime)
        {
            if (!IsRunning || _resultDispatched)
                return;

            _actionSequence.Tick(new StageActionTime(deltaTime, unscaledDeltaTime));

            if (_actionSequence.State == eStageActionSequenceState.Failed)
            {
                if (!_actionFailureNotified)
                {
                    _actionFailureNotified = true;
                    OnActionSequenceFailed?.Invoke(this, _actionSequence.Failure);
                }

                return;
            }

            if (_actionSequence.State != eStageActionSequenceState.Completed ||
                !HasPendingResult)
            {
                return;
            }

            _resultDispatched = true;
            OnResultProduced?.Invoke(this, PendingResult);
        }
        /// <summary>외부에서 생성한 몬스터를 현재 웨이브 소속으로 등록한다.</summary>
        public bool RegisterMonster(Monster monster)
        {
            if (!IsRunning || monster == null || !_monsters.Add(monster))
                return false;

            monster.OnDeath += HandleMonsterKilled;
            return true;
        }

        public int GetKillCount(eMonsterType type)
        {
            return _killCounts.GetValueOrDefault(type, 0);
        }
        /// <summary>예정된 몬스터 등록이 끝났음을 알리고 클리어 여부를 확인한다.</summary>
        public void CompleteSpawning()
        {
            if (!IsRunning)
                return;

            _spawningCompleted = true;
        }

        /// <summary>현재 웨이브를 종료하고 남아 있는 몬스터를 풀에 반환한다.</summary>
        public void Exit()
        {
            if (!IsRunning)
                return;

            _actionSequence?.Cancel();
            IsRunning = false;
            IsBattleRunning = false;
            _rule.Exit(this);

            var remainingMonsters = new List<Monster>(_monsters);
            foreach (Monster monster in remainingMonsters)
            {
                monster.OnDeath -= HandleMonsterKilled;
                _monsterSpawner.ReleaseMonster(monster.Type, monster);
            }

            _monsters.Clear();
            _spawningCompleted = false;
            HasPendingResult = false;
            PendingResult = StageRuleResult.None;
            _actionSequence = null;
        }
        public void NotifyPartyDefeated()
        {
            if (!IsRunning || HasPendingResult)
                return;

            AcceptResult(_rule.OnPartyDefeated(this));
        }

        internal void BeginBattle()
        {
            if (!IsRunning || IsBattleRunning || HasPendingResult)
                return;

            IsBattleRunning = true;
            OnBattleStarted?.Invoke(this);
        }

        internal void TickRule(float deltaTime)
        {
            if (!IsRunning || !IsBattleRunning || HasPendingResult)
                return;

            AcceptResult(_rule.Tick(this, deltaTime));
        }

        private void HandleMonsterKilled(IDamageable target)
        {
            if (!IsRunning || target is not Monster monster || !_monsters.Remove(monster))
                return;

            monster.OnDeath -= HandleMonsterKilled;

            // 결과 연출 중 발생한 추가 사망은 진행도와 퀘스트 처치 수에 포함하지 않는다.
            if (HasPendingResult)
                return;

            _killCounts.TryGetValue(monster.Type, out int killCount);
            _killCounts[monster.Type] = killCount + 1;
            _totalKillCount++;
            // 상태 갱신이 끝난 뒤 Rule 판정
            StageRuleResult result =
                _rule.OnMonsterKilled(this, monster);

            OnMonsterKilled?.Invoke(this, monster);
            AcceptResult(result);
        }

        private void AcceptResult(StageRuleResult result)
        {
            if (result.Action == eStageFlowAction.None) return;
            if (HasPendingResult) return;

            HasPendingResult = true;
            IsBattleRunning = false;
            PendingResult = result;
            OnResultAccepted?.Invoke(this, result);
        }
    }
}
