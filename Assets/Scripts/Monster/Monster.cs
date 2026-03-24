using KingdomIdle.UIToolkit; // UI 연동(피격 데미지 텍스트)
using Scripts.Core;
using Scripts.Core.inteface;
using Scripts.Core.Utils;
using Scripts.Monster.State;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Scripts.Monster
{
	using Scripts.Core.inteface;
	using Scripts.Core.SO;
	using Scripts.Core.StateMachine;
	using Scripts.Monster.MonsterNode;
	using Scripts.Monster.SO;

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
		[Serializable]
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
		[SerializeField]
		private MonsterStat _stat;
		[System.NonSerialized] eMonsterType _type;
		long _dropTableNumber;

		//AI
		private MonsterOrder _monAI;

		int a = 10;
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

		[SerializeField]
		MonsterAnimationSO _AnimationClipSO;
		float _lastAttackTime;
		public event Action OnDeath;

		public float LastAttackTime
		{
			get { return _lastAttackTime; }
		}

		public GameObject gameobj
		{
			get { return transform.gameObject; }
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
			_monAI.Init(this);
			InitializeAnimator();
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
			_stateManchine.currentState.OnUpdate();
		}

		private void LateUpdate()
		{

			//UpdateAnimation();
			//CleanUpResource();
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
			CustomLogger.Log("Target 초기화!");
		}
		public void SetType(eMonsterType monsterType)
		{
			_type = monsterType;
		}
		public void SetTarget(IDamageable target)
		{
			if (target == null)
			{
				CustomLogger.Log("Target IS NULL");
				return;
			}

			Target = target;
			target.OnDeath += ResetTarget;
		}
		public void SetAction(eMonsterAction action)
		{
			//Action Update.
			_monAction = action;
		}
		public void OnAlloc()
		{
			//생성자
			OnDeath = null;
			_stateManchine.BeginMachine(new MonsterMoveState(this));
			foreach (var col in GetComponentsInChildren<Collider2D>())
				col.enabled = true;

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
			// Dead 체크를 먼저: 이미 사망한 몬스터에게는 데미지 적용 안 함
			Debug.Log($"prev _monAction : {_monAction}");
			if (_monAction == eMonsterAction.Dead)
			{
				Debug.Log($"Monster is {gameObject.GetInstanceID()} Dead But attack By attacker : {attacker.gameobj.GetInstanceID()}");
				//CustomLogger.LogError("죽은 상태인데 공격받음!");
				return false;
			}


			ulong dmg = attacker.damage;

			// UI 연동: 몬스터 머리 위로 피격 데미지 표시
			UITKDamageTextBridge.ShowOnTransform(transform, dmg);

			// ── [DEBUG] 몬스터 무적 — 제거 시 이 블록 삭제 ──
			if (KingdomIdle.UIToolkit.UITKDebugMenuController.MonsterInvincible) return true;

			bool IsAlive = setHp(dmg);
			
			if (!IsAlive)
			{
				_monAction = eMonsterAction.Dead;

				Debug.Log($"Monster Is Dead {gameObject.GetInstanceID()} By attacker : {attacker.gameobj.GetInstanceID()}");
				Debug.Log($"after MonAction : {_monAction}");

				_stateManchine.ChangeState(new MonsterDeadState(this));
				if (attacker is IRewardable target)
				{
					GiveRewardToAttacker(target);
				}
				return false;
			}
			return true;
		}

		public bool Attack(IDamageable target)
		{
			bool IsAlive;
			IsAlive = target.TakeDamage(this);
			if (!IsAlive)
			{
				CustomLogger.Log("타겟이 죽음");
				return false;
			}
			_lastAttackTime = Time.time;
			return true;
		}

		public void ChangeState(EntityState<Monster> state)
		{
			_stateManchine.ChangeState(state);
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

		public void OnDead()
		{
			//Todo : DropItem 스폰
			//Institate 동전
			OnDeath?.Invoke();
			foreach (var col in GetComponentsInChildren<Collider2D>())
				col.enabled = false;
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

		private void GiveRewardToAttacker(IRewardable target)
		{

			DropInfo info = DropManager.Instance.GetDropInfo((eDropTable)_dropTableNumber);
			target.GiveReward(info._incomeGold, info._incomeAncientCoin);

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