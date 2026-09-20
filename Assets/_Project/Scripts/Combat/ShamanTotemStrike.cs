using System.Collections.Generic;
using Scripts.Monster;
using UnityEngine;

namespace KingdomIdle.Combat
{
    /// <summary>The shaman's authored summon: emerge, one hit, settle, then crumble.</summary>
    public sealed class ShamanTotemStrike : MonoBehaviour
    {
        [SerializeField] private Sprite[] emerge, idle, crumble;
        [SerializeField] private float framesPerSecond = 12;
        [SerializeField] private int impactFrame = 3;
        private static readonly Dictionary<ShamanTotemStrike, Stack<ShamanTotemStrike>> Pools = new();
        private ShamanTotemStrike _prefab;
        private SpriteRenderer _renderer;
        private Monster _owner;
        private Player _target;
        private int _ownerGeneration, _targetGeneration, _shownFrame = -1;
        private ulong _damage;
        private Vector2 _point;
        private float _elapsed;
        private bool _hit;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetPools() => Pools.Clear();

        public static void Launch(ShamanTotemStrike prefab, Monster owner, Player target)
        {
            if (prefab == null || owner == null || target == null || target.IsDead) return;
            if (!Pools.TryGetValue(prefab, out var pool)) Pools[prefab] = pool = new();
            ShamanTotemStrike effect = null;
            while (pool.Count > 0 && effect == null) effect = pool.Pop();
            if (effect == null) { effect = Instantiate(prefab); effect._prefab = prefab; }
            effect._renderer ??= effect.GetComponent<SpriteRenderer>();
            effect._owner = owner; effect._target = target;
            effect._ownerGeneration = owner.AllocGen; effect._targetGeneration = target.LifeGeneration;
            effect._damage = owner.damage; effect._point = target.VfxFootPosition;
            effect.transform.position = new Vector3(effect._point.x, effect._point.y, owner.transform.position.z);
            effect._elapsed = 0; effect._hit = false; effect._shownFrame = -1;
            effect.gameObject.SetActive(true); effect.Show(0);
        }

        private void Update()
        {
            _elapsed += Time.deltaTime;
            int frame = Mathf.FloorToInt(_elapsed * framesPerSecond);
            if (!_hit && frame >= impactFrame)
            {
                _hit = true;
                bool valid = _owner != null && _owner.AllocGen == _ownerGeneration &&
                    _target != null && _target.isActiveAndEnabled && !_target.IsDead && _target.LifeGeneration == _targetGeneration;
                if (valid && Vector2.Distance(_target.VfxFootPosition, _point) <= .55f)
                {
                    _target.TakeDamage(new ActiveSkill.DamageProxy(_damage, null));
                    CombatDiagnostics.Record("totem-hit", _owner, _target);
                }
                else if (_owner != null) CombatDiagnostics.Record("totem-miss", _owner, _target);
            }
            if (frame >= emerge.Length + idle.Length + crumble.Length)
            {
                gameObject.SetActive(false); _owner = null; _target = null;
                if (_prefab != null && Pools.TryGetValue(_prefab, out var pool)) pool.Push(this);
                return;
            }
            Show(frame);
        }

        private void Show(int frame)
        {
            if (_shownFrame == frame) return;
            _shownFrame = frame;
            _renderer.sprite = frame < emerge.Length ? emerge[frame] :
                frame < emerge.Length + idle.Length ? idle[frame - emerge.Length] : crumble[frame - emerge.Length - idle.Length];
        }
    }
}
