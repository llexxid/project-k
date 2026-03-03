using Scripts.Core;
using Scripts.Core.inteface;
using Scripts.Core.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Scripts.Monster.State;
using KingdomIdle.UIToolkit; // UI 연동(피격 데미지 텍스트)

namespace Scripts.Monster
{
    using Scripts.Core.inteface;
    using Scripts.Core.SO;
    using Scripts.Core.StateMachine;
    using Scripts.Monster.MonsterNode;
    using Scripts.Monster.SO;
    using UnityEditorInternal;

    public struct MonsterInfo
    {
        public MonsterInfo(string name, ulong exp, long baseHp, ulong baseAtk, double baseMoveSpeed, double baseAtkSpeed, long dropTable)
        {
            _name = name;
            _exp = exp;
            _baseHp = baseHp;
            _baseAtk = baseAtk;
            _baseMoveSpeed = baseMoveSpeed;
            _baseAtkSpeed = baseAtkSpeed;
            _dropTableNumber = dropTable;
        }

        public readonly string _name;
        public readonly ulong _exp;
        public readonly long _baseHp;
        public readonly ulong _baseAtk;

        public readonly double _baseMoveSpeed;
        public readonly double _baseAtkSpeed;

        public readonly long _dropTableNumber;
    }

    public class Monster : MonoBehaviour, IPoolable, IDamageable, IAttackable
    {
        public struct MonsterStat
        {
            public MonsterStat(long hp, int extraHp, ulong atk, double moveSpeed, double atkSpeed)
            {
                _hp = hp;
                _extraHp = extraHp;
                _atk = atk;
                _moveSpeed = moveSpeed;
                _atkSpeed = atkSpeed;
            }
            public long _hp;
            public int _extraHp;

            public ulong _atk;

            public double _moveSpeed;
            public double _atkSpeed;
        }
        private MonsterStat _stat;
        eMonsterType _type;
        long _dropTableNumber;

        //AI
        private MonsterOrder _monAI;

        //Animation
        private int _facingDir;
        private eMonsterAction _monAction;
        public IDamageable Target { get; private set; }
        private Animator _am;
        [SerializeField]
        private float _attackRadius;
        [SerializeField]
        private float _detectRadius;

        public eMonsterType Type
        {
            get { return _type; }
        }
        public Animator Animator
        {
            get
            {
                return _am;
            }
        }
        public eMonsterAction MonAction { get { return _monAction; } }
        public bool IsActive { get; set; }
        public ulong damage
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
        public float AttackRadius
        {
            get { return _attackRadius; }
        }
        public float DectectRadius
        {
            get { return _detectRadius; }
        }
        public int FacingDir
        {
            get { return _facingDir; }
        }
        
        AnimatorComponent<eMonsterAction> _animatorComponent;
        public AnimatorComponent<eMonsterAction> AnimationComponent
        {
            get { return _animatorComponent; }
        }
        StateMachine<Monster> _stateManchine;
        MonsterStateFactory _stateFactory;
        [SerializeField]
        MonsterAnimationSO _AnimationClipSO;

        float _lastAttackTime;
        public float LastAttackTime
        {
            get { return _lastAttackTime; }
        }
        //Todo : SkillComponent . 몬스터 스킬
        void Awake()
        {
            _detectRadius = 2.5f;
            _attackRadius = 0.8f;
            _facingDir = 1; // 1 : Right, -1 : Left
            _am = gameObject.GetComponentInChildren<Animator>();

            _stateManchine = new StateMachine<Monster>();
            _monAI = new MonsterOrder();
            _stateFactory = new MonsterStateFactory(this);
			_monAI.Init(this);
			InitializeAnimator();

			//ForTest
		}

        void Update()
        {
            if (_monAI != null)
            {
                _monAI.ExecuteNode();
            }
            _stateManchine.currentState.OnUpdate();
        }

        /// <summary>
        /// 상대좌표 - 내 좌표한 값을 매개변수로 받습니다.
        /// </summary>
        /// <param name="GapBetweenX"></param>
        public void SetFlip(float GapBetweenX)
        {
            //나보다 오른쪽에 있는데 왼쪽을 보는경우
            if (GapBetweenX >= 0 && _facingDir == -1)
            {
                CustomLogger.Log("Flip To Right");
                transform.Rotate(0, 180, 0);
                _facingDir *= -1;
                return;
            }
            //나보다 왼쪽에 있는데, 내가 오른쪽을 보고있다.
            if (GapBetweenX < 0 && _facingDir == 1)
            {
                CustomLogger.Log("Flip To Left");
                transform.Rotate(0, 180, 0);
                _facingDir *= -1;
                return;
            }
        }

        public void Init(eMonsterType monsterType, MonsterStat stat, long droptable_number)
        {
            _stat = stat;
            _type = monsterType;
            _dropTableNumber = droptable_number;
        }
        public double GetSpeed()
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
        public void SetAction(eMonsterAction action)
        {
            //Action Update.
            _monAction = action;
        }
        public void OnAlloc()
        {
            //생성자
            _stateManchine.BeginMachine(_stateFactory.GetState(eMonsterAction.Walk));
            return;
        }
        public void OnRelease()
        {
            //만약에 리지드 바디가 있다면, 초기화.
            Target = null;
            return;
        }
        public bool TakeDamage(IAttackable attacker)
        {
            ulong dmg = attacker.damage;

            // UI 연동: 몬스터 머리 위로 피격 데미지 표시
            UITKDamageTextBridge.ShowOnTransform(transform, dmg);
            CustomLogger.Log($"몬스터가 HP : {_stat._hp} DMG : {dmg} 받음");
            bool IsAlive = setHp(dmg);
			if (_monAction == eMonsterAction.Dead)
            {
                CustomLogger.LogError("죽은 상태인데 공격받음!");
            }

            if (!IsAlive)
            {
                //죽었다면 -> 죽은 연출해주고, Reward를 주면됨.
                //Todo : Reward 주기
                if (attacker is IRewardable target)
                {
                    DropInfo info = DropManager.Instance.GetDropInfo(eDropTable.ORC_DROPTABLE);
                    target.GiveReward(info._incomeGold, info._incomeAncientCoin);
                }
                return false;
            }

            //연출부
			if (_monAction != eMonsterAction.Hurt)
			{
				ChangeState(eMonsterAction.Hurt);
			}
			return true;
        }

        public bool Attack(IDamageable target)
        {
            bool IsAlive;
            IsAlive = target.TakeDamage(this);
            _lastAttackTime = Time.time;
            if (!IsAlive)
            {
                CustomLogger.Log("타겟이 죽음");
                ResetTarget();
                return false;
            }
            return true;
        }

        public void ChangeState(eMonsterAction action)
        {
            _stateManchine.ChangeState(_stateFactory.GetState(action));
        }

        public void InterruptBehaviourTree()
        {
            _monAI.InterruptBT();
        }
        public void RestartBehaviourTree()
        {
            _monAI.RecoveryBT();
        }

        public float GetAnimationLength(eMonsterAction action)
        {
            return _AnimationClipSO.GetAnimationLength(action);
        }

        private void OnDead()
        {
            //Todo : DropItem 스폰
            //Institate 동전
            CustomLogger.Log("Monster Is Dead!!");
            _monAction = eMonsterAction.Dead;
            _stateManchine.ChangeState(_stateFactory.GetState(eMonsterAction.Dead));
        }

        private void InitializeAnimator()
        {
            Dictionary<eMonsterAction, int> dic = new Dictionary<eMonsterAction, int>();
            dic.Add(eMonsterAction.Idle, Animator.StringToHash("Idle"));
            dic.Add(eMonsterAction.Walk, Animator.StringToHash("Walk"));
            dic.Add(eMonsterAction.Attack, Animator.StringToHash("Attack"));
            dic.Add(eMonsterAction.Dead, Animator.StringToHash("Dead"));
            dic.Add(eMonsterAction.Hurt, Animator.StringToHash("Hurt"));

            _animatorComponent = new AnimatorComponent<eMonsterAction>(_am, dic);
        }

        private bool setHp(ulong damage)
        {
            long totalHp = _stat._hp + _stat._extraHp;
            //죽는경우
            if (totalHp - (long)damage <= 0)
            {
                OnDead();
                return false;
            }

            //ExtraHp먼저 깍기
            if ((long)damage > _stat._extraHp)
            {
                long remainDamage = (long)damage - _stat._extraHp;
                _stat._extraHp = 0;
                _stat._hp -= remainDamage;
                return true;
            }

            _stat._extraHp = _stat._extraHp - (int)damage;
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