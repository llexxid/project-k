using System.Collections.Generic;
using UnityEngine;
using Scripts.Core;
using Scripts.Monster;

namespace KingdomIdle.Combat
{
    public enum CrowdControlKind { None = 0, Stun = 1, Slow = 2 }

    /// <summary>Independent control clocks; the strongest live slow wins without stacking.</summary>
    [DisallowMultipleComponent]
    public sealed class MonsterCCState : MonoBehaviour
    {
        private struct Slow { public float Amount, Until; }
        private readonly List<Slow> _slows = new(4);
        private Monster _monster;
        private int _allocGen;
        private float _stunUntil;
        private bool _active, _stunned;
        private PooledSpellVfx _statusVfx;
        private int _statusVfxGen;
#if UNITY_EDITOR || LOBBY_DEVICE_QA
        public string DiagnosticKind => _stunned ? "Stun" : _active ? "Slow" : "None";
        public float DiagnosticRemaining
        {
            get
            {
                float end = _stunUntil;
                foreach (var slow in _slows) end = Mathf.Max(end, slow.Until);
                return _active ? Mathf.Max(0, end - Time.time) : 0;
            }
        }
#endif
        public static void Apply(Monster monster, CrowdControlKind kind, float duration, float slowPercent,
            GameObject statusVfxPrefab = null, Vector3 statusVfxOffset = default)
        {
            if (monster == null || !monster.isActiveAndEnabled || monster.MonAction == eMonsterAction.Dead ||
                kind == CrowdControlKind.None || duration <= 0) return;
            var state = monster.GetComponent<MonsterCCState>() ?? monster.gameObject.AddComponent<MonsterCCState>();
            if (state._active && state._allocGen != monster.AllocGen) state.Release();
            state._monster = monster; state._allocGen = monster.AllocGen;
            state._active = true; state.enabled = true;
            if (kind == CrowdControlKind.Stun)
                state._stunUntil = Mathf.Max(state._stunUntil, Time.time + duration);
            else
            {
                float amount = Mathf.Clamp(slowPercent, 0, .8f);
                bool found = false;
                for (int i = 0; i < state._slows.Count; i++)
                {
                    var slow = state._slows[i];
                    if (!Mathf.Approximately(slow.Amount, amount)) continue;
                    slow.Until = Mathf.Max(slow.Until, Time.time + duration);
                    state._slows[i] = slow; found = true; break;
                }
                if (!found) state._slows.Add(new Slow { Amount = amount, Until = Time.time + duration });
            }
            state.Refresh();
            if (statusVfxPrefab != null)
            {
                if (state._statusVfx != null) state._statusVfx.Release(state._statusVfxGen);
                state._statusVfx = PooledSpellVfx.Spawn(statusVfxPrefab, monster.transform.position + statusVfxOffset,
                    duration, monster.transform, statusVfxOffset);
                state._statusVfxGen = state._statusVfx != null ? state._statusVfx.SpawnGen : 0;
            }
        }
        private void Refresh()
        {
            float strongest = 0;
            for (int i = _slows.Count - 1; i >= 0; i--)
                if (_slows[i].Until <= Time.time) _slows.RemoveAt(i);
                else strongest = Mathf.Max(strongest, _slows[i].Amount);
            _monster.SpeedMultiplier = 1 - strongest;
            bool stun = Time.time < _stunUntil;
            if (stun && !_stunned) _monster.InterruptBehaviourTree();
            else if (!stun && _stunned) _monster.RestartBehaviourTree();
            _stunned = stun;
            if (!stun && _slows.Count == 0) Release();
        }
        private void Update()
        {
            if (_monster == null || _monster.AllocGen != _allocGen || _monster.MonAction == eMonsterAction.Dead)
                Release();
            else Refresh();
        }
        private void OnDisable() { if (_active) Release(); }
        private void Release()
        {
            _active = false;
            if (_monster != null && _monster.AllocGen == _allocGen && _monster.MonAction != eMonsterAction.Dead)
            {
                _monster.SpeedMultiplier = 1;
                if (_stunned) _monster.RestartBehaviourTree();
            }
            _stunned = false; _stunUntil = 0; _slows.Clear();
            if (_statusVfx != null) { _statusVfx.Release(_statusVfxGen); _statusVfx = null; }
            enabled = false;
        }
    }
}
