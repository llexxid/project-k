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
		// ── [WaveManager] stale 태스크 방지 ──
		// OnEnter 시점의 AllocGen을 캡처. 비동기 대기 후 풀 재할당으로 AllocGen이
		// 변했다면 (= 같은 인스턴스가 다른 wave 몬스터로 재사용됨) OnDead를 발사하지 않는다.
		private int _allocGenAtEnter;
		// ── [WaveManager 끝] ──

		public MonsterDeadState(Monster owner)
			: base(owner)
		{

		}

		public override void OnEnter()
		{
			base.OnEnter();
			//Attack���� ���Խ�
			_owner.AnimationComponent.TrySetTrigger(eMonsterAction.Dead);
			float time = _owner.GetAnimationLength(eMonsterAction.Dead);

			_allocGenAtEnter = _owner.AllocGen;
			AfterAnimationEnd(time).Forget();
		}

		private async UniTaskVoid AfterAnimationEnd(float time)
		{
			await UniTask.Delay(TimeSpan.FromSeconds(time));
			// 이미 풀에 반환된 경우 (이중 해제 방지)
			if (_owner == null || !_owner.IsActive) return;
			// 재할당된 몬스터(=다른 wave에서 재사용 중)에는 OnDead를 발사하지 않음
			if (_owner.AllocGen != _allocGenAtEnter) return;
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

