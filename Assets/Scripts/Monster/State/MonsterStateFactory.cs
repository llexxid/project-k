using Scripts.Core;
using Scripts.Core.StateMachine;
using Scripts.Monster;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Scripts.Monster.State
{
	public class MonsterStateFactory
	{
		Dictionary<eMonsterAction, EntityState<Monster>> _dic;

		public MonsterStateFactory(Monster owner)
		{
			_dic = new Dictionary<eMonsterAction, EntityState<Monster>>();
			RegisterFactory(owner);
		}

		void RegisterFactory(Monster owner)
		{
			_dic.Add(eMonsterAction.Idle, new MonsterIdleState(owner));
			_dic.Add(eMonsterAction.Hurt, new MonsterHurtState(owner));
			_dic.Add(eMonsterAction.Dead, new MonsterDeadState(owner));
			_dic.Add(eMonsterAction.Walk, new MonsterMoveState(owner));
			_dic.Add(eMonsterAction.Attack, new MonsterAttackState(owner));
		}

		public EntityState<Monster> GetState(eMonsterAction action)
		{
			return _dic[action];
		}
	}

}
