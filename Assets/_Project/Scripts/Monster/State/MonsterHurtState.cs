using Cysharp.Threading.Tasks;
using Cysharp.Threading.Tasks.CompilerServices;
using Scripts.Core;
using Scripts.Core.StateMachine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace Scripts.Monster.State
{
	public class MonsterHurtState : EntityState<Monster>
	{
		//Hurt같은 상태는 스스로 벗어나야함.
		float hit_latency = 100;
		CancellationTokenSource _token;
		public MonsterHurtState(Monster owner)
			: base(owner)
		{
			_token = new CancellationTokenSource();
		}

		public override void OnEnter()
		{
			base.OnEnter();
			//Attack으로 진입시
			_owner.InterruptBehaviourTree();
			_owner.AnimationComponent.TrySetBool(eMonsterAction.Hurt, true);
			_owner.SetAction(eMonsterAction.Hurt);

			WaitHitLatency().Forget();
		}

		public override void OnUpdate()
		{
			base.OnUpdate();
		}
		public override void OnExit()
		{
			base.OnExit();
			_owner.AnimationComponent.TrySetBool(eMonsterAction.Hurt, false);
			_owner.RestartBehaviourTree();
		}

		private async UniTaskVoid WaitHitLatency()
		{
			await UniTask.Delay(TimeSpan.FromMilliseconds(hit_latency), cancellationToken: _token.Token);
			_owner.ChangeState(new MonsterIdleState(_owner));
		}
	}
}

