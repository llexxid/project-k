using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Scripts.Core;
using Scripts.Core.inteface;
using KingdomIdle.UIToolkit;

namespace KingdomIdle.MageTower
{
    public class MageTowerSkillProjectile : MonoBehaviour, IAttackable
    {
        private ulong _damage;
        private Vector3 _spawnPos;
        private readonly HashSet<int> _hitIds = new();

        private static readonly List<Collider2D> _overlapResults = new(8);

        private Action _onHitCallback;
        private float _damageRadius;
        private bool _shakeOnHit;
        private float _shakeDuration;
        private float _shakeMagnitude;
        private Transform _center;

        public ulong damage => _damage;
        public Vector3 attackerPos => _spawnPos;

		public GameObject gameobj => gameObject;

		public void Initialize(ulong dmg, Vector3 pos, Action onHitCallback = null,
                               float damageRadius = 1.5f, bool shakeOnHit = false,
                               float shakeDuration = 0.15f, float shakeMagnitude = 0.08f)
        {
            _damage = dmg;
            _spawnPos = pos;
            transform.position = pos;
            _onHitCallback = onHitCallback;
            _damageRadius = damageRadius;
            _shakeOnHit = shakeOnHit;
            _shakeDuration = shakeDuration;
            _shakeMagnitude = shakeMagnitude;

            _center = transform.Find("Center");

            var collider = GetComponent<Collider2D>();
            if (collider != null)
                collider.enabled = false;

            // 스킬 이펙트를 캐릭터 뒤, 몬스터 앞에 렌더링
            foreach (var sr in GetComponentsInChildren<SpriteRenderer>(true))
                sr.sortingOrder = 1;
        }

        public bool Attack(IDamageable target)
        {
            if (target == null) return false;
            return target.TakeDamage(this);
        }

        // Animation Event — 데미지 적용 및 체인 콜백
        public void OnHit()
        {
            DealDirectDamage();

            if (_shakeOnHit)
                DoScreenShake();

            _onHitCallback?.Invoke();
            _onHitCallback = null;
        }

        /// <summary>
        /// 스킬 위치 기준으로 범위 내 몬스터에게 직접 데미지를 적용한다.
        /// 콜라이더 온오프 방식 대신 Physics2D.OverlapCircle로 직접 탐색.
        /// </summary>
        private void DealDirectDamage()
        {
            ContactFilter2D filter = new ContactFilter2D();
            filter.SetLayerMask(GameLayers.EnemyMask);
            filter.useLayerMask = true;
            filter.useTriggers = true;

            _overlapResults.Clear();
            Vector3 circleCenter = _center != null ? _center.position : transform.position;
            int count = Physics2D.OverlapCircle(circleCenter, _damageRadius, filter, _overlapResults);

            for (int i = 0; i < count; i++)
            {
                var col = _overlapResults[i];
                if (col == null) continue;

                int id = col.gameObject.GetInstanceID();
                if (!_hitIds.Add(id)) continue;

                var damageable = col.GetComponent<IDamageable>();
                if (damageable != null)
                {
                    Attack(damageable);
                    UITKDamageTextBridge.ShowOnTransform(col.transform, _damage);
                }
            }
        }

        // Animation Event
        public void OnAnimationEnd()
        {
            Destroy(gameObject);
        }

        // ===== Gizmo: Scene 뷰에서 damageRadius 시각화 =====
        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 1f, 0f, 0.4f);
            Vector3 center = _center != null ? _center.position : transform.position;
            Gizmos.DrawWireSphere(center, _damageRadius > 0f ? _damageRadius : 1.5f);
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

            shaker.Shake(_shakeDuration, _shakeMagnitude);
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
