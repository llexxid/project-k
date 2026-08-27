using System.Collections.Generic;
using UnityEngine;
using Scripts.Core;
using Scripts.Core.inteface;
using Scripts.Monster;
using KingdomIdle.UGUI;

namespace KingdomIdle.MageTower
{
	/// <summary>
	/// 지속형 마탑 스킬. 대상 몬스터에 붙어서 일정 간격으로 데미지를 가하고,
	/// 대상이 죽으면 가장 가까운 몬스터로 이동한다. 지속시간이 끝나면 쿨다운 시작.
	/// </summary>
	public class MageTowerSkillPersistent : MonoBehaviour, IAttackable, IRewardable
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
        private SFXEntity _loopSfx;

        // 리타겟 실패(화면 안 후보 없음) 시 재시도 간격 — 매 프레임 화면 크기 물리 쿼리 방지
        private const float RetargetRetryInterval = 0.15f;
        private float _retargetCooldown;

        public ulong damage => _damage;
        public Vector3 attackerPos => transform.position;

		public GameObject gameobj => gameObject;

		/// <summary>마탑 스킬로 처치한 몬스터의 골드/고대주화를 파티에 귀속시킨다.</summary>
		public void GiveReward(int gold, int ancientCoin)
			=> MageTowerReward.GiveToParty(gold, ancientCoin);

		private static readonly List<Collider2D> _results = new(32);

        public void Initialize(ulong dmg, float duration, float tickInterval,
                               float moveSpeed, float arrivalThreshold,
                               int slotIndex, int skillId, Transform initialTarget,
                               string sfxLoopName = null)
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

            if (!string.IsNullOrEmpty(sfxLoopName) &&
                System.Enum.TryParse(sfxLoopName, out eSFXType loopSfxType))
            {
                SFXManager.Instance.GetSFX(
                    loopSfxType, transform.position, Quaternion.identity,
                    sfx => { _loopSfx = sfx; sfx.PlaySFXLoop(); });
            }
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

            // 타겟 유효성 체크 — 죽었거나 화면 밖으로 나갔으면 리타겟.
            // (지속형 스킬은 화면 안의 몬스터만 추적한다는 계약 — 화면 밖까지 쫓아가 잡지 않는다)
            // 화면 안에 후보가 하나도 없으면 실패가 반복되므로 0.15s 스로틀 —
            // 몬스터 이동 속도상 재시도 간격이 그보다 촘촘할 이유가 없다 (매 프레임 물리 쿼리 방지).
            if (_retargetCooldown > 0f) _retargetCooldown -= Time.deltaTime;
            if (!IsTargetAlive() ||
                !MageTowerTargeting.IsOnScreen(MageTowerTargeting.ResolveCamera(), _currentTarget.position))
            {
                if (_retargetCooldown <= 0f)
                {
                    Retarget();
                    _moving = _currentTarget != null;
                    _retargetCooldown = _currentTarget != null ? 0f : RetargetRetryInterval;
                }
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
        /// 현재 위치에서 가장 가까운 **화면 안** 살아있는 몬스터로 타겟 변경.
        /// 쿼리 원을 토네이도 위치가 아니라 화면 중앙에 걸어 두는 이유:
        /// 토네이도가 가장자리로 흘러간 상태에서 자기 중심 반경으로 찾으면
        /// 화면에서 한 화면 지름만큼 떨어진 몬스터까지 후보가 됐다(화면 밖 킬의 주범).
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
            int count = Physics2D.OverlapCircle(center, searchRadius, filter, _results);

            float bestDist = float.MaxValue;
            Transform bestTarget = null;

            for (int i = 0; i < count; i++)
            {
                var col = _results[i];
                if (col == null) continue;

                var monster = col.GetComponent<Monster>();
                if (monster == null || monster.MonAction == eMonsterAction.Dead) continue;

                // 뷰포트 밖 몬스터는 추적 대상이 아니다
                if (!MageTowerTargeting.IsOnScreen(cam, col.transform.position)) continue;

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
            DamageTextBridge.ShowOnTransform(_currentTarget, _damage);
        }

        private void Finish()
        {
            if (_loopSfx != null)
            {
                _loopSfx.StopSFX();
                SFXManager.Instance.DestroySFX(_loopSfx);
                _loopSfx = null;
            }

            var mgr = MageTowerManager.Instance;
            if (mgr != null)
                mgr.EndCasting(_slotIndex);

            Destroy(gameObject);
        }
    }
}
