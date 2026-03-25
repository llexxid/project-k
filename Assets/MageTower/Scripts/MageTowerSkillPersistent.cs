using System.Collections.Generic;
using UnityEngine;
using Scripts.Core;
using Scripts.Core.inteface;
using Scripts.Monster;
using KingdomIdle.UIToolkit;

namespace KingdomIdle.MageTower
{
	/// <summary>
	/// 지속형 마탑 스킬. 대상 몬스터에 붙어서 일정 간격으로 데미지를 가하고,
	/// 대상이 죽으면 가장 가까운 몬스터로 이동한다. 지속시간이 끝나면 쿨다운 시작.
	/// </summary>
	public class MageTowerSkillPersistent : MonoBehaviour, IAttackable
    {
        private ulong _damage;
        private float _duration;
        private float _tickInterval;
        private int _slotIndex;
        private int _skillId;

        private float _elapsed;
        private float _tickTimer;
        private Transform _currentTarget;
        private bool _initialized;
        private bool _moving;
        private float _moveSpeed;
        private float _arrivalThreshold;

        public ulong damage => _damage;
        public Vector3 attackerPos => transform.position;

		public GameObject gameobj => throw new System.NotImplementedException();

		private static readonly List<Collider2D> _results = new(32);

        public void Initialize(ulong dmg, float duration, float tickInterval,
                               float moveSpeed, float arrivalThreshold,
                               int slotIndex, int skillId, Transform initialTarget)
        {
            _damage = dmg;
            _duration = duration;
            _tickInterval = tickInterval;
            _moveSpeed = moveSpeed;
            _arrivalThreshold = arrivalThreshold;
            _slotIndex = slotIndex;
            _skillId = skillId;
            _currentTarget = initialTarget;
            _elapsed = 0f;
            _tickTimer = 0f;
            _initialized = true;

            if (_currentTarget != null)
                transform.position = _currentTarget.position;

            // 스킬 이펙트를 캐릭터 뒤, 몬스터 앞에 렌더링
            foreach (var sr in GetComponentsInChildren<SpriteRenderer>(true))
                sr.sortingOrder = 1;
        }

        public bool Attack(IDamageable target)
        {
            if (target == null) return false;
            return target.TakeDamage(this);
        }

        private void Update()
        {
            if (!_initialized) return;

            _elapsed += Time.deltaTime;
            if (_elapsed >= _duration)
            {
                Finish();
                return;
            }

            // 타겟 유효성 체크 — 죽었으면 리타겟
            if (!IsTargetAlive())
            {
                Retarget();
                _moving = _currentTarget != null;
            }

            // 이동 중이면 타겟을 향해 부드럽게 이동
            if (_currentTarget != null)
            {
                if (_moving)
                {
                    Vector3 targetPos = _currentTarget.position;
                    transform.position = Vector3.MoveTowards(
                        transform.position, targetPos, _moveSpeed * Time.deltaTime);

                    if (Vector2.Distance(transform.position, targetPos) < _arrivalThreshold)
                    {
                        transform.position = targetPos;
                        _moving = false;
                    }
                }
                else
                {
                    transform.position = _currentTarget.position;
                }
            }

            // 이동 중에는 데미지 안 줌, 도착해야 틱 시작
            if (_moving) return;

            // 데미지 틱
            _tickTimer += Time.deltaTime;
            if (_tickTimer >= _tickInterval)
            {
                _tickTimer -= _tickInterval;
                DealDamage();
            }
        }

        private bool IsTargetAlive()
        {
            if (_currentTarget == null) return false;

            var monster = _currentTarget.GetComponent<Monster>();
            if (monster == null) return false;
            return monster.MonAction != eMonsterAction.Dead;
        }

        /// <summary>
        /// 현재 위치에서 가장 가까운 살아있는 몬스터로 타겟 변경.
        /// </summary>
        private void Retarget()
        {
            _currentTarget = null;

            var cam = Camera.main;
            if (cam == null) return;

            float camDist = Mathf.Abs(cam.transform.position.z);
            Vector3 center = cam.ScreenToWorldPoint(
                new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, camDist));
            center.z = 0f;

            Vector3 edge = cam.ScreenToWorldPoint(
                new Vector3(Screen.width, Screen.height, camDist));
            float searchRadius = Vector2.Distance(center, (Vector2)edge) + 2f;

            ContactFilter2D filter = new ContactFilter2D();
            filter.SetLayerMask(GameLayers.EnemyMask);
            filter.useLayerMask = true;
            filter.useTriggers = true;

            _results.Clear();
            int count = Physics2D.OverlapCircle(transform.position, searchRadius, filter, _results);

            float bestDist = float.MaxValue;
            Transform bestTarget = null;

            for (int i = 0; i < count; i++)
            {
                var col = _results[i];
                if (col == null) continue;

                var monster = col.GetComponent<Monster>();
                if (monster == null || monster.MonAction == eMonsterAction.Dead) continue;

                float dist = Vector2.Distance(transform.position, col.transform.position);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestTarget = col.transform;
                }
            }

            _currentTarget = bestTarget;
        }

        private void DealDamage()
        {
            if (_currentTarget == null) return;

            var damageable = _currentTarget.GetComponent<IDamageable>();
            if (damageable == null) return;

            Attack(damageable);
            UITKDamageTextBridge.ShowOnTransform(_currentTarget, _damage);
        }

        private void Finish()
        {
            var mgr = MageTowerManager.Instance;
            if (mgr != null)
                mgr.EndCasting(_slotIndex);

            Destroy(gameObject);
        }
    }
}
