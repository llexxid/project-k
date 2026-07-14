using System;
using System.Collections.Generic;
using Scripts.Core.inteface;
using Scripts.Core.Utils;
using Scripts.Monster;

namespace Scripts.Core
{
    using Monster = Scripts.Monster.Monster;

    /// <summary>
    /// 현재 실행 중인 한 웨이브의 몬스터와 클리어 상태를 관리한다.
    /// 몬스터 생성 위치 결정과 리소스 로딩은 외부 매니저가 담당한다.
    /// </summary>
    public sealed class StageSession
    {
        public event Action<StageSession, Monster> MonsterKilled;
        public event Action<StageSession> Cleared;
        private readonly HashSet<Monster> _monsters = new();
        private readonly Dictionary<eMonsterType, int> _killCounts = new();
        private readonly MonsterSpawner _monsterSpawner;

        private bool _spawningCompleted;
        private bool _clearNotified;

        public StageDefinition Definition { get; }
        public bool IsRunning { get; private set; }
        public bool IsCleared => _spawningCompleted && _monsters.Count == 0;
        public int RemainingMonsterCount => _monsters.Count;

        public IReadOnlyCollection<Monster> Monsters => _monsters;
        public IReadOnlyDictionary<eMonsterType, int> KillCounts => _killCounts;



        public StageSession(StageDefinition definition, MonsterSpawner monsterSpawner)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            _monsterSpawner = monsterSpawner ?? throw new ArgumentNullException(nameof(monsterSpawner));
        }

        /// <summary>몬스터 등록을 받을 수 있도록 세션을 시작한다.</summary>
        public void Enter()
        {
            IsRunning = true;
            _spawningCompleted = false;
            _clearNotified = false;
            _killCounts.Clear();
        }

        /// <summary>외부에서 생성한 몬스터를 현재 웨이브 소속으로 등록한다.</summary>
        public bool RegisterMonster(Monster monster)
        {
            if (!IsRunning || monster == null || !_monsters.Add(monster))
                return false;

            monster.OnDeath += HandleMonsterKilled;
            return true;
        }

        /// <summary>예정된 몬스터 등록이 끝났음을 알리고 클리어 여부를 확인한다.</summary>
        public void CompleteSpawning()
        {
            if (!IsRunning)
                return;

            _spawningCompleted = true;
            TryNotifyCleared();
        }

        /// <summary>현재 웨이브를 종료하고 남아 있는 몬스터를 풀에 반환한다.</summary>
        public void Exit()
        {
            if (!IsRunning)
                return;

            IsRunning = false;

            var remainingMonsters = new List<Monster>(_monsters);
            foreach (Monster monster in remainingMonsters)
            {
                monster.OnDeath -= HandleMonsterKilled;
                _monsterSpawner.ReleaseMonster(monster.Type, monster);
            }

            _monsters.Clear();
            _spawningCompleted = false;
            _clearNotified = false;
        }

        private void HandleMonsterKilled(IDamageable target)
        {
            if (!IsRunning || target is not Monster monster || !_monsters.Remove(monster))
                return;

            monster.OnDeath -= HandleMonsterKilled;

            _killCounts.TryGetValue(monster.Type, out int killCount);
            _killCounts[monster.Type] = killCount + 1;

            MonsterKilled?.Invoke(this, monster);

            TryNotifyCleared();
        }

        private void TryNotifyCleared()
        {
            if (_clearNotified || !IsCleared)
                return;

            _clearNotified = true;
            Cleared?.Invoke(this);
        }
    }
}
