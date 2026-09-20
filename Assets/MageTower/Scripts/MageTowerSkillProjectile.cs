using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Scripts.Core;
using Scripts.Core.inteface;
using KingdomIdle.UGUI;

namespace KingdomIdle.MageTower
{
    public class MageTowerSkillProjectile : MonoBehaviour, IAttackable, IRewardable
    {
        private ulong _damage;
        private bool _fired;
        private string _battle;
        private Vector3 _spawnPos;
        private readonly HashSet<int> _hitIds = new();

        private static readonly List<Collider2D> _overlapResults = new(8);

        private Action _onHitCallback;
        private float _damageRadius;
        private bool _shakeOnHit;
        private float _shakeDuration;
        private float _shakeMagnitude;
        private Transform _center;
        private string _sfxName;

        public ulong damage => _damage;
        public Vector3 attackerPos => _spawnPos;

		public GameObject gameobj => gameObject;

		/// <summary>마탑 스킬로 처치한 몬스터의 골드/고대주화를 파티에 귀속시킨다.</summary>
		public void GiveReward(int gold, int ancientCoin)
			=> MageTowerReward.GiveToParty(gold, ancientCoin);

		public void Initialize(ulong dmg, Vector3 pos, Action onHitCallback = null,
                               float damageRadius = 1.5f, bool shakeOnHit = false,
                               float shakeDuration = 0.15f, float shakeMagnitude = 0.08f,
                               string sfxName = null)
        {
            _damage = dmg; _fired = false; _battle = KingdomIdle.Balance.LocalProgression.State.ActiveBattleId;
            _spawnPos = pos;
            transform.position = pos;
            _onHitCallback = onHitCallback;
            _damageRadius = damageRadius;
            _shakeOnHit = shakeOnHit;
            _shakeDuration = shakeDuration;
            _shakeMagnitude = shakeMagnitude;

            _sfxName = sfxName;
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
            if (_fired || _battle != KingdomIdle.Balance.LocalProgression.State.ActiveBattleId) return;
            _fired = true;
            DealDirectDamage();

            if (_shakeOnHit)
                DoScreenShake();

            if (!string.IsNullOrEmpty(_sfxName) &&
                System.Enum.TryParse(_sfxName, out Scripts.Core.eSFXType sfxType))
            {
                Scripts.Core.SFXManager.Instance.GetSFX(
                    sfxType, transform.position, Quaternion.identity, sfx => sfx.PlaySFX());
            }

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

            var cam = MageTowerTargeting.ResolveCamera();
            for (int i = 0; i < count; i++)
            {
                var col = _overlapResults[i];
                if (col == null) continue;

                // 화면 가장자리 AoE 가 화면 밖 몬스터까지 스치지 않게 — 마탑 스킬 피해의
                // 단일 관문이라 여기서 거르면 번개 랜덤 홉 포함 전 피해가 화면 안으로 갇힌다.
                if (!MageTowerTargeting.IsOnScreen(cam, col.transform.position)) continue;

                var monster = col.GetComponentInParent<Scripts.Monster.Monster>();
                if (monster == null || monster.MonAction == eMonsterAction.Dead) continue;
                int id = monster.GetInstanceID();
                if (!_hitIds.Add(id)) continue;

                var damageable = monster as IDamageable;
                if (damageable != null)
                {
                    Attack(damageable);
                    if (_hitIds.Count >= (_damageRadius < .4f ? 1 : 3)) break;
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
        private float _duration, _magnitude, _elapsed;
        private bool _shaking;
        private Vector3 _offset;
        private Unity.Cinemachine.CinemachineBrain _brain;
        public static event System.Action<float, float> OnShake;
#if UNITY_EDITOR || LOBBY_DEVICE_QA
        public Vector3 DiagnosticOffset => _offset;
#endif

        public void Shake(float duration, float magnitude)
        {
            if (!KingdomIdle.UGUI.GamePresentationSettings.ScreenShake || duration <= 0) return;
            _duration = duration; _magnitude = Mathf.Max(0, magnitude); _elapsed = 0; _shaking = true;
            OnShake?.Invoke(duration, magnitude);
        }
        private void OnEnable()
        {
            _brain = GetComponent<Unity.Cinemachine.CinemachineBrain>();
            KingdomIdle.UGUI.GamePresentationSettings.Changed += ApplySettings;
            Unity.Cinemachine.CinemachineCore.CameraUpdatedEvent.AddListener(AfterCameraUpdate);
        }
        private void OnDisable()
        {
            KingdomIdle.UGUI.GamePresentationSettings.Changed -= ApplySettings;
            Unity.Cinemachine.CinemachineCore.CameraUpdatedEvent.RemoveListener(AfterCameraUpdate);
            StopShake();
        }
        private void LateUpdate()
        {
            if (!_shaking) return;
            _elapsed += Time.unscaledDeltaTime;
            if (_elapsed >= _duration) { StopShake(); return; }
            if (_brain == null || !_brain.isActiveAndEnabled)
            {
                transform.localPosition -= _offset; _offset = Vector3.zero;
                ApplyOffset();
            }
        }
        private void AfterCameraUpdate(Unity.Cinemachine.CinemachineBrain brain)
        {
            if (brain != _brain) return;
            // Cinemachine has just written a fresh base pose; apply shake after that write.
            _offset = Vector3.zero;
            if (_shaking) ApplyOffset();
        }
        private void ApplyOffset()
        {
            float amplitude = _magnitude * Mathf.Clamp01(1 - _elapsed / _duration);
            // Presentation must not consume the random stream used by drops/combat.
            _offset = new Vector3(Mathf.Sin(_elapsed * 97.1f), Mathf.Sin(_elapsed * 151.3f + 1.7f), 0) * amplitude;
            transform.localPosition += _offset;
        }
        private void ApplySettings() { if (!KingdomIdle.UGUI.GamePresentationSettings.ScreenShake) StopShake(); }
        private void StopShake() { transform.localPosition -= _offset; _offset = Vector3.zero; _shaking = false; }
    }
}
