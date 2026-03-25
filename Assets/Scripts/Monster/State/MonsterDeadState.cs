using Cysharp.Threading.Tasks;
using Scripts.Core;
using Scripts.Core.StateMachine;
using Scripts.Core.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Scripts.Monster.State
{
	public class MonsterDeadState : EntityState<Monster>
	{
		public MonsterDeadState(Monster owner)
			: base(owner)
		{

		}

		public override void OnEnter()
		{
			base.OnEnter();
			//Attack으로 진입시
			_owner.AnimationComponent.TrySetTrigger(eMonsterAction.Dead);
			float time = _owner.GetAnimationLength(eMonsterAction.Dead);
			
			AfterAnimationEnd(time).Forget();
		}

		private async UniTaskVoid AfterAnimationEnd(float time)
		{
			await UniTask.Delay(TimeSpan.FromSeconds(time));
			Debug.Log($"monster Release |{_owner.gameObject.name} | {_owner.gameObject.GetInstanceID()}");
			_owner.OnDead();
			MonsterSpawner.Instance.ReleaseMonster(_owner.Type, _owner);
		}
		public override void OnUpdate()
		{
			base.OnUpdate();
		}
		public override void OnExit()
		{
			base.OnExit();
		}

	}
}

