using Scripts.Core;
using Scripts.Core.StateMachine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Scripts.Monster.State
{
	public class MonsterIdleState : EntityState<Monster>
	{
		public MonsterIdleState(Monster owner) 
			: base(owner)
		{
		}
		public override void OnEnter()
		{
			base.OnEnter();
			_owner.AnimationComponent.TrySetBool(eMonsterAction.Idle, true);
			_owner.SetAction(eMonsterAction.Idle);
		}

		public override void OnUpdate()
		{
			base.OnUpdate();
		}
		public override void OnExit()
		{
			base.OnExit();
			_owner.AnimationComponent.TrySetBool(eMonsterAction.Idle, false);
		}
	}
}

