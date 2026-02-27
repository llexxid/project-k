using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Scripts.Core.StateMachine;
using Scripts.Core;
using Scripts.Core.Utils;

namespace Scripts.Monster.State
{
	using Monster = Scripts.Monster.Monster;
	public class MonsterAttackState : EntityState<Monster>
	{
		public MonsterAttackState(Monster owner) 
			: base(owner)
		{

		}

		public override void OnEnter()
		{
			base.OnEnter();
			//Attack으로 진입시
			_owner.AnimationComponent.TrySetBool(eMonsterAction.Attack, true);
			_owner.SetAction(eMonsterAction.Attack);
		}

		public override void OnUpdate()
		{
			base.OnUpdate();
		}
		public override void OnExit()
		{
			base.OnExit();
			_owner.AnimationComponent.TrySetBool(eMonsterAction.Attack, false);
		}
	}
}

