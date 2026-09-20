using System.Collections.Generic;
using Scripts.Core.inteface;
using Scripts.Monster;
using UnityEngine;

namespace KingdomIdle.Combat
{
    /// <summary>Pooled flight; captured damage is applied only on arrival.</summary>
    public sealed class MonsterProjectile : MonoBehaviour, IAttackable
    {
        private static readonly Dictionary<GameObject, Stack<MonsterProjectile>> Pools = new();
        private GameObject _prefab;
        private Monster _owner;
        private Player _target;
        private int _ownerGeneration, _targetGeneration;
        private Vector3 _start, _destination;
        private float _elapsed, _duration, _arc, _angleOffset;
        private bool _flying;
        public ulong damage { get; private set; }
        public Vector3 attackerPos => transform.position;
        public GameObject gameobj => gameObject;
        public bool Attack(IDamageable target) => target != null && target.TakeDamage(this);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() => Pools.Clear();

        public static void Launch(GameObject prefab, Monster owner, Player target, Vector3 start, float speed, float arc, float angleOffset)
        {
            if (prefab == null || owner == null || target == null || target.IsDead) return;
            if (!Pools.TryGetValue(prefab, out var pool)) Pools[prefab] = pool = new();
            MonsterProjectile shot = null;
            while (pool.Count > 0 && shot == null) shot = pool.Pop();
            if (shot == null)
            {
                var go = Instantiate(prefab);
                shot = go.GetComponent<MonsterProjectile>() ?? go.AddComponent<MonsterProjectile>();
                shot._prefab = prefab;
                foreach (var collider in go.GetComponentsInChildren<Collider2D>()) collider.enabled = false;
            }
            shot._owner = owner; shot._ownerGeneration = owner.AllocGen;
            shot._target = target; shot._targetGeneration = target.LifeGeneration;
            shot.damage = owner.damage;
            shot._start = start; shot._destination = target.transform.position + Vector3.up * .35f;
            shot._duration = Mathf.Max(.12f, Vector3.Distance(start, shot._destination) / Mathf.Max(1, speed));
            shot._elapsed = 0; shot._arc = arc; shot._angleOffset = angleOffset; shot._flying = true;
            shot.transform.SetPositionAndRotation(start, Quaternion.identity);
            shot.gameObject.SetActive(true);
            foreach (var animator in shot.GetComponentsInChildren<Animator>()) { animator.Rebind(); animator.Update(0); }
        }

        private void Update()
        {
            if (!_flying) return;
            if (_owner == null || !_owner.isActiveAndEnabled || _owner.AllocGen != _ownerGeneration ||
                _target == null || !_target.isActiveAndEnabled || _target.IsDead || _target.LifeGeneration != _targetGeneration)
            { Release(); return; }
            _elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(_elapsed / _duration);
            // A committed throw can miss a retreating target; it never chases a respawn.
            Vector3 next = Vector3.Lerp(_start, _destination, t) + Vector3.up * (_arc * 4 * t * (1 - t));
            Vector3 direction = next - transform.position;
            transform.position = next;
            if (direction.sqrMagnitude > .00001f)
                transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - _angleOffset);
            if (t < 1) return;
            if (Vector2.Distance(_target.transform.position + Vector3.up * .35f, _destination) <= .48f)
            {
                CombatDiagnostics.Record("projectile-hit", this, _target);
                _target.TakeDamage(this);
            }
            else CombatDiagnostics.Record("projectile-miss",this,_target);
            Release();
        }

        private void Release()
        {
            _flying = false; _owner = null; _target = null;
            gameObject.SetActive(false);
            if (_prefab != null && Pools.TryGetValue(_prefab, out var pool)) pool.Push(this);
        }
    }
}
