using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Scripts.Core;
using Scripts.Monster;
using KingdomIdle.KingdomArmy;
using KingdomIdle.MageTower;

namespace KingdomIdle.Divine
{
    /// <summary>
    /// 신 스킬의 전장 실행기. 수치 계산은 DivineSkillManager 가 끝낸 뒤
    /// 여기서는 "누구에게 · 언제 · 어떤 연출과 함께" 적용할지를 담당한다.
    /// DivineSkillManager 와 같은 GameObject 에 자동으로 붙는다.
    ///
    /// 연출은 전부 선택 사항이다 — 프리팹/SFX 가 비어 있으면 그 단계만 건너뛰고
    /// 데미지·회복·버프는 동일하게 적용된다.
    /// </summary>
    public sealed class DivineSkillCaster : MonoBehaviour
    {
        // 물리 질의 결과 버퍼 (한 번의 호출 안에서만 유효)
        private static readonly List<Collider2D> _searchResults = new(64);

        // 시전 1회 동안 사용하는 대상 버퍼 — 동시에 두 개의 신 스킬이 돌지 않으므로 재사용 가능하지만,
        // 코루틴이 프레임을 넘겨 살아 있으므로 시전마다 새 리스트를 만든다(시전 빈도가 낮아 비용 무시 가능).
        private CameraShaker _shaker;

        /// <summary>
        /// 카드를 시전한다. 실제로 무언가 적용됐으면 true.
        /// value: 공격형 = 1히트 데미지, 회복형 = 대상 MAXHP 에 곱할 비율.
        /// onFinished: 지속 효과까지 끝난 시점에 호출(쿨타임은 시전 즉시 시작된다).
        /// </summary>
        public bool Cast(DivineSkillSO so, double value, Action onFinished)
        {
            if (so == null)
            {
                onFinished?.Invoke();
                return false;
            }

            switch (so.effectKind)
            {
                case eDivineEffectKind.AoeBurst:     return CastAoeBurst(so, value, onFinished);
                case eDivineEffectKind.SingleBurst:  return CastSingleBurst(so, value, onFinished);
                case eDivineEffectKind.Dot:          return CastDot(so, value, onFinished);
                case eDivineEffectKind.HealAndGuard: return CastHealAndGuard(so, (float)value, onFinished);
                case eDivineEffectKind.PartyHaste:   return CastPartyHaste(so, onFinished);
            }

            onFinished?.Invoke();
            return false;
        }

        /// <summary>스테이지 전환 등으로 진행 중인 연출을 전부 중단한다.</summary>
        public void StopAll()
        {
            StopAllCoroutines();
        }

        // ────────────────────────────────────────────
        //  공격형
        // ────────────────────────────────────────────
        private bool CastAoeBurst(DivineSkillSO so, double value, Action onFinished)
        {
            var targets = new List<Monster>(32);
            CollectAliveMonsters(targets);
            if (targets.Count == 0)
            {
                onFinished?.Invoke();
                return false;
            }

            StartCoroutine(AoeBurstRoutine(so, value, targets, onFinished));
            return true;
        }

        private IEnumerator AoeBurstRoutine(DivineSkillSO so, double value,
                                            List<Monster> targets, Action onFinished)
        {
            PlaySfx(so.sfxName);
            SpawnBurst(so);

            // 군중 제어는 연출과 동시에 — 적이 멈춘 채로 심판을 기다리는 그림이 된다
            ApplyCrowdControl(so, targets);

            float wait = Mathf.Max(0f, so.impactDelay) + Mathf.Max(0f, so.castDelay);
            if (wait > 0f) yield return new WaitForSeconds(wait);

            // 지연 후 대상 재수집 — 그 사이에 죽거나 새로 나온 몬스터를 반영
            targets.Clear();
            CollectAliveMonsters(targets);

            // 풀 재사용 감지 (MonsterDeadState 와 같은 관용구):
            // 스태거 루프 중 죽은 몬스터가 풀에서 재할당되면 AllocGen 이 달라져 스킵된다.
            var gens = new List<int>(targets.Count);
            for (int i = 0; i < targets.Count; i++) gens.Add(targets[i].AllocGen);

            Shake(so);
            PlaySfx(so.impactSfxName);

            ulong damage = ToDamage(value);
            var owner = FindOwner();

            for (int i = 0; i < targets.Count; i++)
            {
                var m = targets[i];
                if (m == null || m.AllocGen != gens[i] || m.MonAction == eMonsterAction.Dead) continue;

                SpawnImpact(so, m.transform.position);
                DealDamage(m, damage, owner);

                if (so.impactStagger > 0f && i < targets.Count - 1)
                    yield return new WaitForSeconds(so.impactStagger);
            }

            onFinished?.Invoke();
        }

        private bool CastSingleBurst(DivineSkillSO so, double value, Action onFinished)
        {
            var target = FindPriorityTarget();
            if (target == null)
            {
                onFinished?.Invoke();
                return false;
            }

            StartCoroutine(SingleBurstRoutine(so, value, target, onFinished));
            return true;
        }

        private IEnumerator SingleBurstRoutine(DivineSkillSO so, double value,
                                               Monster target, Action onFinished)
        {
            PlaySfx(so.sfxName);
            SpawnBurst(so);

            if (so.crowdControl != eDivineCrowdControl.None && target != null)
                MonsterCCState.Apply(target, so.crowdControl, so.ccDuration, so.slowPercent,
                                     so.statusVfxPrefab, so.statusVfxOffset);

            float wait = Mathf.Max(0f, so.impactDelay) + Mathf.Max(0f, so.castDelay);
            if (wait > 0f) yield return new WaitForSeconds(wait);

            // 지연 중 대상이 죽었으면 다음 우선 대상으로 옮긴다 (궁극기가 허공을 때리지 않게)
            if (target == null || target.MonAction == eMonsterAction.Dead)
                target = FindPriorityTarget();

            if (target != null)
            {
                Shake(so);
                PlaySfx(so.impactSfxName);
                SpawnImpact(so, target.transform.position);
                DealDamage(target, ToDamage(value), FindOwner());
            }

            onFinished?.Invoke();
        }

        private bool CastDot(DivineSkillSO so, double value, Action onFinished)
        {
            if (!HasAliveMonster())
            {
                onFinished?.Invoke();
                return false;
            }

            StartCoroutine(DotRoutine(so, value, onFinished));
            return true;
        }

        private IEnumerator DotRoutine(DivineSkillSO so, double value, Action onFinished)
        {
            PlaySfx(so.sfxName);
            SpawnBurst(so);

            var targets = new List<Monster>(32);
            CollectAliveMonsters(targets);
            ApplyCrowdControl(so, targets);

            if (so.impactDelay > 0f) yield return new WaitForSeconds(so.impactDelay);

            int hits = Mathf.Max(1, so.hitCount);
            float interval = hits > 1 ? Mathf.Max(0.05f, so.duration / hits) : 0f;
            ulong damage = ToDamage(value);

            for (int h = 0; h < hits; h++)
            {
                // 히트마다 재탐색 — 도중에 스폰된 몬스터도 남은 히트를 맞는다
                targets.Clear();
                CollectAliveMonsters(targets);

                var owner = FindOwner();
                if (h == 0) Shake(so);

                for (int i = 0; i < targets.Count; i++)
                {
                    var m = targets[i];
                    if (m == null || m.MonAction == eMonsterAction.Dead) continue;
                    SpawnImpact(so, m.transform.position);
                    DealDamage(m, damage, owner);
                }

                if (h < hits - 1 && interval > 0f)
                    yield return new WaitForSeconds(interval);
            }

            onFinished?.Invoke();
        }

        // ────────────────────────────────────────────
        //  지원형
        // ────────────────────────────────────────────
        private bool CastHealAndGuard(DivineSkillSO so, float healRatio, Action onFinished)
        {
            var players = GetAlivePlayers();
            if (players.Count == 0)
            {
                onFinished?.Invoke();
                return false;
            }

            PlaySfx(so.sfxName);
            SpawnBurst(so);

            for (int i = 0; i < players.Count; i++)
            {
                var p = players[i];
                int maxHp = p.playerStatus != null ? p.playerStatus.MaxHP : 0;
                int heal = Mathf.RoundToInt(maxHp * healRatio);
                if (heal > 0) p.Heal(heal);

                SpawnImpact(so, p.transform.position);
            }

            if (so.damageReducePercent > 0f && so.duration > 0f)
                DivineBuffState.ApplyGuard(so.damageReducePercent, so.duration);

            onFinished?.Invoke();
            return true;
        }

        private bool CastPartyHaste(DivineSkillSO so, Action onFinished)
        {
            var players = GetAlivePlayers();
            if (players.Count == 0)
            {
                onFinished?.Invoke();
                return false;
            }

            PlaySfx(so.sfxName);
            SpawnBurst(so);

            for (int i = 0; i < players.Count; i++)
            {
                // 버프 지속 동안 캐릭터에 붙어 따라다니는 연출
                DivineVfxInstance.Spawn(so.statusVfxPrefab, players[i].transform.position,
                                        so.duration, players[i].transform, so.statusVfxOffset);
            }

            DivineBuffState.ApplyHaste(so.skillIntervalReducePercent,
                                       so.moveSpeedIncreasePercent,
                                       so.duration);
            onFinished?.Invoke();
            return true;
        }

        // ────────────────────────────────────────────
        //  연출 헬퍼
        // ────────────────────────────────────────────
        private void SpawnBurst(DivineSkillSO so)
        {
            var prefab = so.BurstPrefab;
            if (prefab == null) return;

            var inst = DivineVfxInstance.Spawn(prefab, ScreenCenter(), so.burstVfxLifetime);
            if (inst != null)
            {
                inst.fitToCamera = true;
                inst.RefreshFit(); // 프리팹 기본값이 꺼져 있어도 즉시 화면 덮기 적용
            }
        }

        private void SpawnImpact(DivineSkillSO so, Vector3 position)
        {
            DivineVfxInstance.Spawn(so.impactVfxPrefab, position, so.impactVfxLifetime,
                                    null, Vector3.zero, so.impactVfxScale);
        }

        private void Shake(DivineSkillSO so)
        {
            if (!so.screenShake) return;
            if (PlayerPrefs.GetInt("settings_screenShake", 1) == 0) return;

            var cam = Camera.main;
            if (cam == null) return;

            if (_shaker == null || _shaker.gameObject != cam.gameObject)
            {
                _shaker = cam.GetComponent<CameraShaker>();
                if (_shaker == null) _shaker = cam.gameObject.AddComponent<CameraShaker>();
            }

            _shaker.Shake(so.shakeDuration, so.shakeMagnitude);
        }

        private static void PlaySfx(string sfxName)
        {
            if (string.IsNullOrEmpty(sfxName)) return;
            if (!Enum.TryParse(sfxName, out eSFXType sfxType)) return;
            if (SFXManager.Instance == null) return;

            SFXManager.Instance.GetSFX(sfxType, Vector3.zero, Quaternion.identity,
                                       sfx => { if (sfx != null) sfx.PlaySFX(); });
        }

        // ────────────────────────────────────────────
        //  전투 헬퍼
        // ────────────────────────────────────────────
        private static void ApplyCrowdControl(DivineSkillSO so, List<Monster> targets)
        {
            if (so.crowdControl == eDivineCrowdControl.None || so.ccDuration <= 0f) return;
            for (int i = 0; i < targets.Count; i++)
                MonsterCCState.Apply(targets[i], so.crowdControl, so.ccDuration, so.slowPercent,
                                     so.statusVfxPrefab, so.statusVfxOffset);
        }

        private static void DealDamage(Monster monster, ulong damage, Player owner)
        {
            if (monster == null || monster.MonAction == eMonsterAction.Dead) return;

            // 데미지 텍스트는 Monster.TakeDamage 가 직접 띄운다 — 여기서 또 띄우면 숫자가 겹친다.
            var proxy = new ActiveSkill.DamageProxy(damage, owner);
            monster.TakeDamage(proxy);
        }

        private static ulong ToDamage(double value)
        {
            if (value <= 1d) return 1UL;
            if (value >= ulong.MaxValue) return ulong.MaxValue;
            return (ulong)value;
        }

        /// <summary>
        /// 골드/경험치 귀속용 시전자. 살아있는 파티원 우선, 전멸 상태면 죽은 파티원이라도 쓴다 —
        /// 보상은 계정 단위(User 지갑)라서 사망 여부와 무관하게 지급되어야 한다.
        /// </summary>
        private static Player FindOwner()
        {
            var km = KingdomArmyManager.Instance;
            if (km == null) return null;

            var players = km.GetPlayers();
            for (int i = 0; i < players.Count; i++)
            {
                var p = players[i];
                if (p != null && !p.IsDead) return p;
            }
            // 전멸 — 보상 유실을 막기 위해 User 가 연결된 아무 파티원에게 귀속
            for (int i = 0; i < players.Count; i++)
            {
                var p = players[i];
                if (p != null && p.User != null) return p;
            }
            return null;
        }

        public static List<Player> GetAlivePlayers()
        {
            var result = new List<Player>(3);
            var km = KingdomArmyManager.Instance;
            if (km == null) return result;

            var players = km.GetPlayers();
            for (int i = 0; i < players.Count; i++)
            {
                var p = players[i];
                if (p != null && !p.IsDead) result.Add(p);
            }
            return result;
        }

        /// <summary>HP 비율이 threshold 미만인 살아있는 파티원이 있는지 (할당 없음).</summary>
        public static bool AnyPlayerBelowHp(float threshold)
        {
            var km = KingdomArmyManager.Instance;
            if (km == null) return false;

            var players = km.GetPlayers();
            for (int i = 0; i < players.Count; i++)
            {
                var p = players[i];
                if (p != null && !p.IsDead && p.HPRatio < threshold) return true;
            }
            return false;
        }

        // ── 대상 탐색 (마탑 스킬과 동일한 화면 전체 탐색 방식) ──
        /// <summary>화면 안의 살아있는 몬스터를 results 에 채운다 (중복 제거).</summary>
        public static void CollectAliveMonsters(List<Monster> results)
        {
            if (results == null) return;
            results.Clear();

            int count = SearchOnScreen(out _);
            for (int i = 0; i < count; i++)
            {
                var col = _searchResults[i];
                if (col == null) continue;

                var monster = col.GetComponentInParent<Monster>();
                if (monster == null || monster.MonAction == eMonsterAction.Dead) continue;
                if (results.Contains(monster)) continue;

                results.Add(monster);
            }
        }

        /// <summary>화면 안에 살아있는 몬스터가 하나라도 있는지 (할당 없음).</summary>
        public static bool HasAliveMonster()
        {
            int count = SearchOnScreen(out _);
            for (int i = 0; i < count; i++)
            {
                var col = _searchResults[i];
                if (col == null) continue;

                var monster = col.GetComponentInParent<Monster>();
                if (monster != null && monster.MonAction != eMonsterAction.Dead) return true;
            }
            return false;
        }

        /// <summary>단일 대상 카드의 타게팅 — 화면 내 최대 체력(=보스) 우선.</summary>
        private static Monster FindPriorityTarget()
        {
            var monsters = new List<Monster>(32);
            CollectAliveMonsters(monsters);

            Monster best = null;
            long bestHp = -1;

            for (int i = 0; i < monsters.Count; i++)
            {
                long maxHp = monsters[i].MaxHp;
                if (maxHp > bestHp)
                {
                    bestHp = maxHp;
                    best = monsters[i];
                }
            }
            return best;
        }

        private static int SearchOnScreen(out Vector3 worldCenter)
        {
            worldCenter = Vector3.zero;

            var cam = Camera.main;
            if (cam == null) return 0;

            float camDist = Mathf.Abs(cam.transform.position.z);

            Vector3 center = cam.ScreenToWorldPoint(
                new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, camDist));
            center.z = 0f;
            worldCenter = center;

            Vector3 screenEdge = cam.ScreenToWorldPoint(
                new Vector3(Screen.width, Screen.height, camDist));
            float searchRadius = Vector2.Distance(center, (Vector2)screenEdge) + 2f;

            ContactFilter2D filter = new ContactFilter2D();
            filter.SetLayerMask(GameLayers.EnemyMask);
            filter.useLayerMask = true;
            filter.useTriggers = true;

            _searchResults.Clear();
            return Physics2D.OverlapCircle(worldCenter, searchRadius, filter, _searchResults);
        }

        public static Vector3 ScreenCenter()
        {
            var cam = Camera.main;
            if (cam == null) return Vector3.zero;

            float camDist = Mathf.Abs(cam.transform.position.z);
            Vector3 center = cam.ScreenToWorldPoint(
                new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, camDist));
            center.z = 0f;
            return center;
        }
    }
}
