using Scripts.Core;
using Scripts.Core.StateMachine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Scripts.Monster.State
{
	public class MonsterMoveState : EntityState<Monster>
	{
		public MonsterMoveState(Monster owner)
			: base(owner)
		{

		}

		public override void OnEnter()
		{
			//Attack으로 진입시
			base.OnEnter();
			_owner.AnimationComponent.TrySetBool(eMonsterAction.Walk, true);
			_owner.SetAction(eMonsterAction.Walk);
		}

		public override void OnUpdate()
		{
			base.OnUpdate();
		}
		public override void OnExit()
		{
			base.OnExit();
			_owner.AnimationComponent.TrySetBool(eMonsterAction.Walk, false);
		}
	}
}

