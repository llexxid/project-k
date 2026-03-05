using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Scripts.Core.inteface;
using KingdomIdle.UIToolkit;

namespace KingdomIdle.MageTower
{
    public class MageTowerSkillProjectile : MonoBehaviour, IAttackable
    {
        private ulong _damage;
        private Vector3 _spawnPos;
        private Collider2D _collider;
        private readonly HashSet<int> _hitIds = new();

        public ulong damage => _damage;
        public Vector3 attackerPos => _spawnPos;

        public void Initialize(ulong dmg, Vector3 pos)
        {
            _damage = dmg;
            _spawnPos = pos;
            transform.position = pos;

            _collider = GetComponent<Collider2D>();
            if (_collider != null)
                _collider.enabled = false;
        }

        public bool Attack(IDamageable target)
        {
            if (target == null) return false;
            return target.TakeDamage(this);
        }

        // Animation Event — 콜라이더 1프레임 활성화
        public void OnHit()
        {
            if (_collider != null)
            {
                _collider.enabled = true;
                StartCoroutine(DisableColliderNextFrame());
            }
        }

        private IEnumerator DisableColliderNextFrame()
        {
            yield return null;
            if (_collider != null)
                _collider.enabled = false;
        }

        // Animation Event
        public void OnAnimationEnd()
        {
            Destroy(gameObject);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.gameObject.layer != 6) return;

            int id = other.gameObject.GetInstanceID();
            if (!_hitIds.Add(id)) return;

            var damageable = other.GetComponent<IDamageable>();
            if (damageable != null)
            {
                Attack(damageable);
                UITKDamageTextBridge.ShowOnTransform(other.transform, _damage);
            }
        }
    }
}
