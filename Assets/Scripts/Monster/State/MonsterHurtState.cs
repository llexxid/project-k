using Cysharp.Threading.Tasks;
using Cysharp.Threading.Tasks.CompilerServices;
using Scripts.Core;
using Scripts.Core.StateMachine;
using Scripts.Core.Utils;
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
		float hit_latency;
		CancellationTokenSource _token;
		public MonsterHurtState(Monster owner)
			: base(owner)
		{
			_token = new CancellationTokenSource();
			hit_latency = _owner.GetAnimationLength(eMonsterAction.Hurt);
			_canRetriggered = true;
		}

		public override void OnEnter()
		{
			base.OnEnter();
			//Attack으로 진입시
			_owner.InterruptBehaviourTree();
			_owner.AnimationComponent.TrySetTrigger(eMonsterAction.Hurt);
			//CustomLogger.Log("Hurt상태 돌입!");
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
			_owner.RestartBehaviourTree();
		}

		private async UniTaskVoid WaitHitLatency()
		{
			await UniTask.Delay(TimeSpan.FromSeconds(hit_latency), cancellationToken: _token.Token);
			//CustomLogger.Log("Hurt상태 돌입 끝!");
			_owner.ChangeState(eMonsterAction.Walk);
		}
	}
}

