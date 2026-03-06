using System;
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

        private Action _onHitCallback;
        private bool _shakeOnHit;

        public ulong damage => _damage;
        public Vector3 attackerPos => _spawnPos;

        public void Initialize(ulong dmg, Vector3 pos, Action onHitCallback = null, bool shakeOnHit = false)
        {
            _damage = dmg;
            _spawnPos = pos;
            transform.position = pos;
            _onHitCallback = onHitCallback;
            _shakeOnHit = shakeOnHit;

            _collider = GetComponent<Collider2D>();
            if (_collider != null)
                _collider.enabled = false;

            // 스킬 이펙트를 캐릭터 뒤, 몬스터 앞에 렌더링
            foreach (var sr in GetComponentsInChildren<SpriteRenderer>(true))
                sr.sortingOrder = 1;
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

            if (_shakeOnHit)
                DoScreenShake();

            _onHitCallback?.Invoke();
            _onHitCallback = null;
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
            if (other.gameObject.layer != GameLayers.Enemy) return;

            int id = other.gameObject.GetInstanceID();
            if (!_hitIds.Add(id)) return;

            var damageable = other.GetComponent<IDamageable>();
            if (damageable != null)
            {
                Attack(damageable);
                UITKDamageTextBridge.ShowOnTransform(other.transform, _damage);
            }
        }

        // ===== 화면 흔들림 =====
        private void DoScreenShake()
        {
            bool shakeEnabled = PlayerPrefs.GetInt("settings_screenShake", 1) == 1;
            if (!shakeEnabled) return;

            var cam = Camera.main;
            if (cam == null) return;

            var shaker = cam.GetComponent<CameraShaker>();
            if (shaker == null)
                shaker = cam.gameObject.AddComponent<CameraShaker>();

            shaker.Shake(0.15f, 0.08f);
        }
    }

    public class CameraShaker : MonoBehaviour
    {
        private Vector3 _originalPos;
        private float _duration;
        private float _magnitude;
        private float _elapsed;
        private bool _shaking;

        public void Shake(float duration, float magnitude)
        {
            if (!_shaking)
                _originalPos = transform.localPosition;

            _duration = duration;
            _magnitude = magnitude;
            _elapsed = 0f;
            _shaking = true;
        }

        private void LateUpdate()
        {
            if (!_shaking) return;

            _elapsed += Time.deltaTime;
            if (_elapsed >= _duration)
            {
                transform.localPosition = _originalPos;
                _shaking = false;
                return;
            }

            float t = 1f - (_elapsed / _duration);
            float offsetX = UnityEngine.Random.Range(-1f, 1f) * _magnitude * t;
            float offsetY = UnityEngine.Random.Range(-1f, 1f) * _magnitude * t;

            transform.localPosition = _originalPos + new Vector3(offsetX, offsetY, 0f);
        }
    }
}
