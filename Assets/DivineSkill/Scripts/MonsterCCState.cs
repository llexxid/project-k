using UnityEngine;
using Scripts.Core;
using Scripts.Monster;

namespace KingdomIdle.Divine
{
    /// <summary>
    /// 신 스킬이 몬스터 1기에 건 군중 제어를 관리하는 런타임 컴포넌트.
    /// 대상에 자동으로 붙고 만료 시 스스로 원복하며, 상태이상 연출의 수명도 함께 관리한다.
    ///
    /// 몬스터는 풀링되므로, 붙은 뒤 재할당(AllocGen 변화)되거나 사망하면 즉시 해제한다.
    /// (Monster.OnAlloc 이 RecoveryBT / 스탯을 초기화하므로 잔류 효과는 남지 않는다)
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MonsterCCState : MonoBehaviour
    {
        private Monster _monster;
        private eDivineCrowdControl _kind;
        private float _endTime;
        private int _allocGen;
        private bool _active;
        private DivineVfxInstance _statusVfx;
        private int _statusVfxGen;

        /// <summary>대상에게 군중 제어를 적용한다. 이미 걸려 있으면 더 늦은 만료 시각으로 갱신.</summary>
        public static void Apply(Monster monster, eDivineCrowdControl kind,
                                 float duration, float slowPercent,
                                 GameObject statusVfxPrefab = null, Vector3 statusVfxOffset = default)
        {
            if (monster == null) return;
            if (kind == eDivineCrowdControl.None || duration <= 0f) return;
            if (monster.MonAction == eMonsterAction.Dead) return;

            var state = monster.GetComponent<MonsterCCState>();
            if (state == null)
                state = monster.gameObject.AddComponent<MonsterCCState>();

            state.Begin(monster, kind, duration, slowPercent, statusVfxPrefab, statusVfxOffset);
        }

        private void Begin(Monster monster, eDivineCrowdControl kind,
                           float duration, float slowPercent,
                           GameObject statusVfxPrefab, Vector3 statusVfxOffset)
        {
            // 다른 종류가 이미 걸려 있으면 먼저 원복하고 새 효과로 교체
            if (_active && _kind != kind)
                Release();

            _monster = monster;
            _kind = kind;
            _allocGen = monster.AllocGen;
            _endTime = Mathf.Max(_endTime, Time.time + duration);
            _active = true;
            enabled = true; // Release 에서 꺼 둔 Update 를 다시 켠다

            switch (kind)
            {
                case eDivineCrowdControl.Stun:
                    _monster.InterruptBehaviourTree();
                    break;
                case eDivineCrowdControl.Slow:
                    _monster.SpeedMultiplier = Mathf.Clamp01(1f - slowPercent);
                    break;
            }

            // 상태이상 연출 — 남은 지속시간만큼 대상 머리 위를 따라다닌다
            if (statusVfxPrefab != null && _statusVfx == null)
            {
                float remain = Mathf.Max(0.1f, _endTime - Time.time);
                _statusVfx = DivineVfxInstance.Spawn(statusVfxPrefab,
                                                     _monster.transform.position + statusVfxOffset,
                                                     remain, _monster.transform, statusVfxOffset);
                // 인스턴스가 수명 만료로 풀에 반납→재사용된 뒤 우리가 뒤늦게 Release 하는
                // 사고를 막기 위해 세대 토큰을 캡처해 둔다
                _statusVfxGen = _statusVfx != null ? _statusVfx.SpawnGen : 0;
            }
        }

        private void Update()
        {
            if (!_active) return;

            bool expired = Time.time >= _endTime;
            bool invalid = _monster == null
                        || _monster.AllocGen != _allocGen
                        || _monster.MonAction == eMonsterAction.Dead;

            if (expired || invalid)
                Release();
        }

        private void OnDisable()
        {
            // 풀 반환 등으로 비활성화되면 연출이 공중에 남지 않도록 정리
            if (_active) Release();
        }

        private void Release()
        {
            if (!_active) return;
            _active = false;
            _endTime = 0f;

            // 풀링된 몬스터에 붙은 채 세션 내내 살아남으므로, 유휴 상태에서는
            // Update 자체가 돌지 않게 꺼 둔다 (다음 Apply 의 Begin 이 다시 켠다)
            enabled = false;

            if (_statusVfx != null)
            {
                _statusVfx.Release(_statusVfxGen); // 세대 불일치(이미 재사용됨)면 no-op
                _statusVfx = null;
            }

            // 재할당된 몬스터라면 OnAlloc 이 이미 초기화했으므로 건드리지 않는다.
            // 사망 상태면 특히 RestartBehaviourTree 를 호출하면 안 된다 —
            // Monster.TakeDamage 가 죽으면서 건 InterruptBT 를 풀어 시체가 다시 걸어다닌다.
            if (_monster == null
                || _monster.AllocGen != _allocGen
                || _monster.MonAction == eMonsterAction.Dead) return;

            switch (_kind)
            {
                case eDivineCrowdControl.Stun:
                    _monster.RestartBehaviourTree();
                    break;
                case eDivineCrowdControl.Slow:
                    _monster.SpeedMultiplier = 1f;
                    break;
            }
        }
    }
}
