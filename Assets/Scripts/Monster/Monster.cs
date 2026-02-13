using Scripts.Core;
using Scripts.Core.inteface;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Scripts.Monster
{
    public class Monster : MonoBehaviour, IPoolable, IDamageable, IAttackable
    {
        [Serializable]
        public struct MonsterStat
        {
            public int _hp;
            public int _extraHp;

            public int _atk;

            public int _moveSpeed;
            public int _atkSpeed;
        }
        [SerializeField]
        private MonsterStat _stat;
        eMonsterType _type;
        public IDamageable Target { get; private set; }

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

        //Todo : SkillComponent . 몬스터 스킬

        void Start()
        {

        }

        void Update()
        {

        }

        public void SetType(eMonsterType monsterType)
        {
            _type = monsterType;
        }
        public void Init(eMonsterType monsterType, MonsterStat stat)
        {
            _stat = stat;
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
            return;
        }

        public void OnRelease()
        {
            //만약에 리지드 바디가 있다면, 초기화.

            return;
        }

        public void TakeDamage(IAttackable attacker)
        {
            int dmg = attacker.damage;

            setHp(dmg);
        }


        private void OnDead()
        {
            //Todo : DropItem 스폰


            CustomLogger.Log("Monster Is Dead!!");
            MonsterSpawner.Instance.ReleaseMonster(_type, this);
        }

        private void setHp(int damage)
        {
            long totalHp = _stat._hp + _stat._extraHp;
            //죽는경우
            if (totalHp - damage <= 0)
            {
                OnDead();
                return;
            }

            //ExtraHp먼저 깍기
            if (damage > _stat._extraHp)
            {
                int remainDamage = damage - _stat._extraHp;
                _stat._extraHp = 0;
                _stat._hp -= remainDamage;
                return;
            }
            _stat._extraHp -= damage;
            return;
        }
    }
}

