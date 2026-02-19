using Scripts.Core;
using Scripts.Core.inteface;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Scripts.Monster
{
    using Scripts.Core.inteface;
    public class Monster : MonoBehaviour, IPoolable, IDamageable, IAttackable
    {
        [Serializable]
        public struct MonsterStat
        {
            public MonsterStat(int hp, int extraHp, int atk, int moveSpeed, int atkSpeed)
            {
                _hp = hp;
                _extraHp = extraHp;
                _atk = atk;
                _moveSpeed = moveSpeed;
                _atkSpeed = atkSpeed;
            }
            public int _hp;
            public int _extraHp;

            public int _atk;

            public int _moveSpeed;
            public int _atkSpeed;
        }
        [SerializeField]
        private MonsterStat _stat;
        eMonsterType _type;
        [SerializeField]
        private float _attackRadius;
        public float AttackRadius
        {
            get { return _attackRadius; }
        }
        [SerializeField]
        private float _detectRadius;
        public float DectectRadius
        {
            get { return _detectRadius; }
        }
        public IDamageable Target { get; private set; }

        private MonsterOrder _monAI;

        public bool IsActive { get; set; }
        public int damage 
        {
            get
            {
                return _stat._atk;
            }
        }
        public Vector3 attackerPos 
        { 
            get 
            { 
                return transform.position; 
            } 
        }

        public Vector3 targetPos
        {
            get
            {
                return transform.position;
            }
        }

        //Todo : SkillComponent . 몬스터 스킬
        void Awake()
        {
            _detectRadius = 2.5f;
            _attackRadius = 0.8f;
        }
        void Start()
        {

        }

        void Update()
        {
            if (_monAI != null)
            {
                _monAI.ExecuteNode();
            }
        }

        public void Init(eMonsterType monsterType, MonsterStat stat)
        {
            _stat = stat;
            _type = monsterType;
        }

        public int GetSpeed()
        {
            return _stat._moveSpeed;
        }

        public void ResetTarget()
        {
            Target = null;
        }
        public void SetType(eMonsterType monsterType)
        {
            _type = monsterType;
        }


        public void SetTarget(IDamageable target)
        {
            //개발 모드. null일 때 Log남겨놓고 Crash!
            if (target == null)
            {
                CustomLogger.LogWarning("Monster SetTarget is Null!");
            }
            Target = target;
        }

        public void OnAlloc()
        {
            _monAI = MonsterOrderPool.Instance.GetMonsterOrder();
            _monAI.Init(this);
            return;
        }

        public void OnRelease()
        {
            //만약에 리지드 바디가 있다면, 초기화.
            Target = null;
            MonsterOrderPool.Instance.ReleaseMonsterOrder(_monAI);
            return;
        }

        public bool TakeDamage(IAttackable attacker)
        {
            int dmg = attacker.damage;
            bool IsAlive = setHp(dmg);

            setHp(dmg);
            if (!IsAlive)
            {
                return false;
            }
            return true;
        }


        private void OnDead()
        {
            //Todo : DropItem 스폰


            CustomLogger.Log("Monster Is Dead!!");
            MonsterSpawner.Instance.ReleaseMonster(_type, this);
        }

        private bool setHp(int damage)
        {
            long totalHp = _stat._hp + _stat._extraHp;
            //죽는경우
            if (totalHp - damage <= 0)
            {
                OnDead();
                return false;
            }

            //ExtraHp먼저 깍기
            if (damage > _stat._extraHp)
            {
                int remainDamage = damage - _stat._extraHp;
                _stat._extraHp = 0;
                _stat._hp -= remainDamage;
                return true;
            }
            _stat._extraHp -= damage;
            return true;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, _detectRadius);

            Gizmos.color = Color.red;

            // 적의 위치에 구체를 그립니다.
            Gizmos.DrawWireSphere(transform.position, _attackRadius);
        }
    }
}

