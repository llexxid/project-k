using Scripts.Core;
using Scripts.Core.inteface;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Scripts.Monster
{
    using Scripts.Core.inteface;

    public struct MonsterInfo
    {
        public MonsterInfo(string name, int exp, int baseHp, int baseAtk, double baseMoveSpeed, double baseAtkSpeed, long dropTable)
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
        public readonly int _exp;
        public readonly int _baseHp;
        public readonly int _baseAtk;

        public readonly double _baseMoveSpeed;
        public readonly double _baseAtkSpeed;

		public readonly long _dropTableNumber;
	}

    public class Monster : MonoBehaviour, IPoolable, IDamageable, IAttackable
    {
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
        private MonsterStat _stat;
        eMonsterType _type;
		long _dropTableNumber;

        //AI
        private MonsterOrder _monAI;

		//Animation
		private int _facingDir;
		private eMonsterAction _monAction;
        private eMonsterAction _prevAction;
        public IDamageable Target { get; private set; }
        private Animator _am;
        [SerializeField]
        private float _attackRadius;
        [SerializeField]
        private float _detectRadius;
        public Animator Animator { 
            get
            {
                return _am;
            }
        }
        public eMonsterAction MonAction { get { return _monAction; } }
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
        //Todo : SkillComponent . 몬스터 스킬
        void Awake()
        {
            _detectRadius = 2.5f;
            _attackRadius = 0.8f;
            _facingDir = 1; // 1 : Right, -1 : Left
            _am = gameObject.GetComponentInChildren<Animator>();

            _monAI = new MonsterOrder();
            _monAI.Init(this);
            InitializeAnimator();
        }
        void Start()
        {

        }

        void Update()
        {
            _prevAction = _monAction;
            if (_monAI != null)
            {
                _monAI.ExecuteNode();
            }
        }

        private void LateUpdate()
        {
            UpdateAnimation();
            CleanUpResource();
        }

        private void CleanUpResource()
        {
            if (_monAction == eMonsterAction.Dead)
            {
                MonsterSpawner.Instance.ReleaseMonster(_type, this);
            }
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
        public void ChangeMonsterAction(eMonsterAction action)
        {
            _monAction = action;
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
            int dmg = attacker.damage;
            bool IsAlive = setHp(dmg);

            if (!IsAlive)
            {
                //Todo : Reward 주기
                return false;
            }
            return true;
        }


        private void OnDead()
        {
            //Todo : DropItem 스폰
            //Institate 동전
            CustomLogger.Log("Monster Is Dead!!");
            _monAction = eMonsterAction.Dead;
        }

        private void UpdateAnimation()
        {
            if (_prevAction != _monAction)
            {
                
                TurnOffAnimation(_prevAction);
                TurnOnAnimation(_monAction);
            }
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

        private void TurnOffAnimation(eMonsterAction action)
        {
            switch (action)
            {
                case eMonsterAction.Idle:
                case eMonsterAction.Attack:
                case eMonsterAction.Hurt:
                    _animatorComponent.TrySetBool(action, false);
                    /*** Fall through ***/
                    break;
            }
        }
        private void TurnOnAnimation(eMonsterAction action)
        {
            switch (action)
            {
                case eMonsterAction.Idle:
                case eMonsterAction.Attack:
                case eMonsterAction.Hurt:
                    _animatorComponent.TrySetBool(action, true);
                    break;
                /*** Fall through ***/
                case eMonsterAction.Dead:
                    _animatorComponent.TrySetTrigger(action);
                    break;
            }
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

