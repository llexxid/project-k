using System;
using System.Collections;
using System.Collections.Generic;
using KingdomIdle.Balance;
using KingdomIdle.Combat;
using KingdomIdle.UGUI;
using Scripts.Core;
using Scripts.Core.inteface;
using Scripts.Core.Manager;
using Scripts.Monster;
using UnityEngine;

namespace KingdomIdle.MageTower
{
    /// <summary>One cast owns immutable power/bloom/battle snapshots and pooled visual handles.</summary>
    internal sealed class MageTowerSpellCast : IDisposable, IAttackable, IRewardable
    {
        private readonly MageTowerManager _owner;
        private readonly MageTowerSkillSO _skill;
        private readonly string _battle;
        private readonly long _power;
        private readonly int _awakening;
        private readonly bool _bloom;
        private readonly Vector3 _initial;
        private readonly List<Monster> _targets = new(32);
        private readonly List<Collider2D> _colliders = new(32);
        private readonly List<Target> _lineTargets = new(16);
        private readonly List<(PooledSpellVfx effect, int generation)> _visuals = new(24);
        private bool _disposed;
        private Vector3 _origin;
        private ulong _damage;
        private static readonly List<Monster> Candidates = new(32);
        private static readonly List<Collider2D> CandidateColliders = new(32);

        private readonly struct Target
        {
            public readonly Monster Monster;
            private readonly int _generation;
            public Target(Monster monster) { Monster = monster; _generation = monster != null ? monster.AllocGen : -1; }
            public bool Alive => IsAlive(Monster) && Monster.AllocGen == _generation;
        }

        public MageTowerSpellCast(MageTowerManager owner, MageTowerSkillSO skill, long power, int awakening, bool bloom, Monster target)
        {
            _owner = owner; _skill = skill; _power = power; _awakening = awakening; _bloom = bloom;
            _battle = LocalProgression.State.ActiveBattleId; _initial = target.transform.position;
#if UNITY_EDITOR || LOBBY_DEVICE_QA
            MageSkillDiagnostics.Record(skill.id, "begin", target.name, 0, bloom);
#endif
        }

        private bool Valid => !_disposed && _owner != null && StageManager.Instance?.CurrentRunState == eStageRunState.Running &&
            LocalProgression.State.ActiveBattleId == _battle;
        private static bool IsAlive(Monster monster) => monster != null && monster.gameObject.activeInHierarchy && monster.MonAction != eMonsterAction.Dead;
        public ulong damage => _damage;
        public Vector3 attackerPos => _origin;
        public GameObject gameobj => _owner != null ? _owner.gameObject : null;
        public bool Attack(IDamageable target) => Valid && target != null && target.TakeDamage(this);
        public void GiveReward(int gold, int ancientCoin) => MageTowerReward.GiveToParty(gold, ancientCoin);

        private static void Collect(Vector3 center, float radius, List<Monster> targets, List<Collider2D> colliders)
        {
            targets.Clear(); colliders.Clear();
            var filter = new ContactFilter2D { useTriggers = true };
            filter.SetLayerMask(GameLayers.EnemyMask);
            Physics2D.OverlapCircle(center, radius, filter, colliders);
            var camera = MageTowerTargeting.ResolveCamera();
            foreach (var collider in colliders)
            {
                if (collider == null) continue;
                var monster = collider.GetComponentInParent<Monster>();
                if (IsAlive(monster) && MageTowerTargeting.IsOnScreen(camera, monster.transform.position) && !targets.Contains(monster)) targets.Add(monster);
            }
        }

        public static bool TryFindTarget(out Monster target)
        {
            target = null;
            var camera = MageTowerTargeting.ResolveCamera(); if (camera == null) return false;
            float depth = Mathf.Abs(camera.transform.position.z);
            var center = camera.ViewportToWorldPoint(new Vector3(.5f, .5f, depth)); center.z = 0;
            float radius = Vector3.Distance(center, camera.ViewportToWorldPoint(new Vector3(1, 1, depth))) + 2;
            Collect(center, radius, Candidates, CandidateColliders);
            float distance = float.MaxValue;
            foreach (var candidate in Candidates)
            {
                float next = (candidate.transform.position - center).sqrMagnitude;
                if (next < distance) { target = candidate; distance = next; }
            }
            return target != null;
        }

        public static bool NeedsHealing()
        {
            var players = UserManager.Instance?.GetPlayers(); if (players == null) return false;
            foreach (var player in players)
                if (player != null && player.playerStatus != null && player.playerStatus.HP > 0 && player.playerStatus.HP < player.playerStatus.MaxHP * .9) return true;
            return false;
        }

        private PooledSpellVfx Visual(GameObject prefab, Vector3 position, float lifetime, float scale = 1)
        {
            if (!Valid || prefab == null) return null;
            var effect = PooledSpellVfx.Spawn(prefab, position, lifetime, scaleMultiplier: scale);
            if (effect != null) _visuals.Add((effect, effect.SpawnGen));
            return effect;
        }

        private void Hit(Monster monster, decimal multiplier, Vector3 center)
        {
            if (!Valid || !IsAlive(monster) || !MageTowerTargeting.IsOnScreen(MageTowerTargeting.ResolveCamera(), monster.transform.position)) return;
            _damage = checked((ulong)BalanceMath.Damage(_power, multiplier)); _origin = center;
            Attack(monster); // TakeDamage returns whether the monster survived, not hit acceptance.
#if UNITY_EDITOR || LOBBY_DEVICE_QA
            MageSkillDiagnostics.Record(_skill.id, "damage", monster.name, (long)_damage, _bloom);
#endif
        }

        private void Area(Vector3 center, float radius, decimal multiplier, float stun = 0, float slow = 0)
        {
            if (!Valid) return;
            Collect(center, radius, _targets, _colliders);
            int count = Math.Min(_bloom && _skill.spellKind == MageSpellKind.Lightning ? _skill.bloomMaxTargets : _skill.maxTargets, _targets.Count);
            for (int i = 0; i < count && Valid; i++)
            {
                var monster = _targets[i]; Hit(monster, multiplier, center);
                if (stun > 0) MonsterCCState.Apply(monster, CrowdControlKind.Stun, stun, 0);
                else if (slow > 0) MonsterCCState.Apply(monster, CrowdControlKind.Slow, _skill.controlDuration, slow);
            }
        }

        private IEnumerator Delay(float seconds)
        {
            float elapsed = 0;
            while (Valid && elapsed < seconds) { yield return null; elapsed += Time.deltaTime; }
        }

        public IEnumerator Run()
        {
            if (!Valid) yield break;
            if (_skill.spellKind != MageSpellKind.Lightning && _skill.spellKind != MageSpellKind.IceSpike &&
                _skill.spellKind != MageSpellKind.StoneSeal && _skill.spellKind != MageSpellKind.Meteor) Sound(.42f);
            switch (_skill.spellKind)
            {
                case MageSpellKind.Lightning: yield return Lightning(); break;
                case MageSpellKind.IceSpike: yield return Ice(); break;
                case MageSpellKind.ArcaneVolley: yield return Volley(); break;
                case MageSpellKind.GaleBlades: yield return Gale(); break;
                case MageSpellKind.StoneSeal: yield return Stone(); break;
                case MageSpellKind.Meteor: yield return Meteor(); break;
                case MageSpellKind.Sanctuary: yield return Sanctuary(); break;
                case MageSpellKind.VoidRift: yield return Void(); break;
                default: yield return Persistent(); break;
            }
            // Keep the final impact alive while it dissipates; battle cancellation still releases immediately.
            yield return Delay(.25f);
        }

        private void Sound(float gain, float pitch = 1f)
        {
            if (!Valid || !Enum.TryParse(_skill.sfxName, out eSFXType sound)) return;
            Scripts.Core.SFXManager.Instance?.GetSFX(sound, _initial, Quaternion.identity, sfx =>
            {
                if (Valid) sfx.PlayOneShot(gain, pitch);
                else Scripts.Core.SFXManager.Instance?.DestroySFX(sfx);
            });
        }

        private decimal NormalMultiplier => _bloom ? (decimal)_skill.bloomPowerMultiplier : 1m;
        private int Hits => MageSkillRules.HitCount(_skill, _awakening);

        private IEnumerator Lightning()
        {
            if (_bloom)
            {
                // Telegraph stays on the battlefield; the HUD never receives a full-screen flash.
                Visual(_skill.bloomCastingPrefab, _initial + Vector3.up * 1.8f, 2.15f);
                yield return Delay(1.84f); if (!Valid) yield break;
                Visual(_skill.bloomPrefab, _initial, .75f);
                yield return Delay(.16f); if (!Valid) yield break;
                Sound(.85f,.8f);
                var camera = MageTowerTargeting.ResolveCamera();
                float radius = camera == null ? 2.2f : Mathf.Clamp(Vector3.Distance(camera.ViewportToWorldPoint(new Vector3(.25f, .5f, Mathf.Abs(camera.transform.position.z))), camera.ViewportToWorldPoint(new Vector3(.75f, .5f, Mathf.Abs(camera.transform.position.z)))) * .5f, 1.6f, 2.6f);
                Area(_initial, radius, (decimal)_skill.bloomPowerMultiplier);
                yield return Delay(.75f);
            }
            else
                for (int i = 0; i < Hits && Valid; i++)
                {
                    Visual(_skill.prefab, _initial, .5f);
                    yield return Delay(.16f); if (!Valid) yield break;
                    if(i==0) Sound(.58f);
                    Area(_initial, _skill.radius, 1m);
                    yield return Delay(.14f);
                }
        }

        private IEnumerator Ice()
        {
            Collect(_initial, 20, _targets, _colliders);
            if (_bloom && _targets.Count == 1)
            {
                var target = new Target(_targets[0]);
                Visual(_skill.bloomCastingPrefab, _initial, .45f);
                yield return Delay(.35f); if (!Valid || !target.Alive) yield break;
                var position = target.Monster.transform.position;
                Visual(_skill.bloomPrefab, position, .9f);
                yield return Delay(.17f); if(!Valid || !target.Alive) yield break;
                Sound(.7f,.85f);
                Hit(target.Monster, (decimal)_skill.bloomPowerMultiplier, position);
                MonsterCCState.Apply(target.Monster, CrowdControlKind.Stun, _skill.bloomControlDuration, 0);
                yield return Delay(.9f); yield break;
            }
            int volleys = _bloom ? 2 : 1, count = _bloom ? _skill.baseHits * 2 : Hits;
            for (int volley = 0; volley < volleys && Valid; volley++)
            {
                _lineTargets.Clear();
                for (int i = 0; i < count && Valid; i++)
                {
                    Collect(_initial, 20, _targets, _colliders); if (_targets.Count == 0) yield break;
                    var target = _targets[i % _targets.Count]; var position = target.transform.position;
                    float spread = _bloom ? ((i / _targets.Count) % 3 - 1) * .14f : 0;
                    if (!GamePresentationSettings.LowSpec || i < 4) Visual(_skill.prefab, position + new Vector3(spread,0,0), .6f);
                    if (_bloom) _lineTargets.Add(new Target(target));
                    else
                    {
                        var pending = new Target(target);
                        yield return Delay(.17f);
                        if (i==0) Sound(.5f);
                        if(pending.Alive) Hit(pending.Monster,1m,position);
                        yield return Delay(.01f);
                    }
                }
                if (_bloom)
                {
                    yield return Delay(.17f); Sound(.6f,volley==0?1f:1.08f);
                    foreach(var pending in _lineTargets) if(pending.Alive) Hit(pending.Monster,(decimal)_skill.bloomAreaPowerMultiplier,pending.Monster.transform.position);
                }
                yield return Delay(_bloom ? .38f : .4f);
            }
        }

        private IEnumerator Volley()
        {
            for (int i = 0; i < Hits && Valid; i++)
            {
                if (!TryFindTarget(out var monster)) yield break;
                var target = new Target(monster);
                Vector3 start = CombatViewport.Formation(2) + Vector3.up * .3f;
                var visual = Visual(_skill.prefab, start, .5f); float elapsed = 0;
                while (Valid && target.Alive && elapsed < .22f)
                {
                    if (visual != null)
                    {
                        var end=target.Monster.transform.position + Vector3.up * .3f;
                        visual.transform.position=Vector3.Lerp(start,end,elapsed/.22f);
                        var heading=end-start;
                        visual.transform.rotation=Quaternion.Euler(0,0,Mathf.Atan2(heading.y,heading.x)*Mathf.Rad2Deg-45f);
                    }
                    yield return null; elapsed += Time.deltaTime;
                }
                if (Valid && target.Alive)
                {
                    var position = target.Monster.transform.position;
                    Visual(_skill.secondaryPrefab, position + Vector3.up * .3f, .35f);
                    Hit(target.Monster, NormalMultiplier, position);
                }
                if (visual != null) visual.Release();
            }
            yield return Delay(.35f);
        }

        private IEnumerator Persistent()
        {
            bool moving = _skill.spellKind == MageSpellKind.FireTornado;
            Vector3 center = _initial;
            var visual = Visual(_skill.prefab, center, Hits * _skill.tickInterval + .2f);
            float tick = 0; int emitted = 0;
            while (Valid && emitted < Hits)
            {
                if (moving && TryFindTarget(out var target)) center = Vector3.MoveTowards(center, target.transform.position, 2.8f * Time.deltaTime);
                if (visual != null) visual.transform.position = center;
                if (tick <= 0)
                {
                    Area(center, _skill.radius, NormalMultiplier, slow: moving ? 0 : _skill.slowFraction);
                    emitted++; tick += _skill.tickInterval;
                }
                yield return null; tick -= Time.deltaTime;
            }
        }

        private IEnumerator Stone()
        {
            Visual(_skill.castingPrefab, _initial, .5f); yield return Delay(.4f);
            for (int i = 0; i < Hits && Valid; i++)
            {
                Visual(_skill.prefab, _initial, .65f);
                yield return Delay(.17f); if (!Valid) yield break;
                Sound(.45f,.82f);
                Area(_initial, _skill.radius, NormalMultiplier, stun: _skill.controlDuration);
                yield return Delay(.28f);
            }
        }

        private IEnumerator Gale()
        {
            Vector3 from = CombatViewport.Formation(2), direction = (_initial - from).normalized;
            if (direction.sqrMagnitude < .01f) direction = Vector3.up;
            for (int hit = 0; hit < Hits && Valid; hit++)
            {
                var visual = Visual(_skill.prefab, from, .55f);
                Collect(from, 12, _targets, _colliders);
                _lineTargets.Clear();
                foreach (var monster in _targets)
                {
                    var delta = monster.transform.position - from; float along = Vector3.Dot(delta, direction);
                    if (along >= 0 && along <= 7 && (delta - direction * along).sqrMagnitude <= _skill.radius * _skill.radius && _lineTargets.Count < _skill.maxTargets) _lineTargets.Add(new Target(monster));
                }
                float elapsed = 0;
                while (Valid && elapsed < .4f)
                {
                    float distance = 7f * elapsed / .4f;
                    if (visual != null) visual.transform.position = from + direction * distance;
                    for (int i = _lineTargets.Count - 1; i >= 0; i--)
                        if (!_lineTargets[i].Alive) _lineTargets.RemoveAt(i);
                        else if (Vector3.Dot(_lineTargets[i].Monster.transform.position - from, direction) <= distance)
                        { Hit(_lineTargets[i].Monster, NormalMultiplier, from); _lineTargets.RemoveAt(i); }
                    yield return null; elapsed += Time.deltaTime;
                }
                foreach (var target in _lineTargets) if (target.Alive) Hit(target.Monster, NormalMultiplier, from);
                if (visual != null) visual.Release();
            }
        }

        private IEnumerator Meteor()
        {
            Visual(_skill.castingPrefab, _initial, 1.4f);
            var travel = new Vector3(2.5f,3.5f,0);
            var falling = Visual(_skill.prefab, _initial + travel, 1.3f);
            float elapsed = 0;
            while (Valid && elapsed < 1.1f)
            {
                if (falling != null) falling.transform.position = _initial + travel * (1 - Mathf.Pow(elapsed / 1.1f,1.65f));
                yield return null; elapsed += Time.deltaTime;
            }
            if (!Valid) yield break;
            if (falling != null) falling.Release();
            Visual(_skill.secondaryPrefab, _initial, 2.1f);
            yield return Delay(.09f); if (!Valid) yield break;
            Sound(.65f,.78f); Area(_initial, _skill.radius, NormalMultiplier);
            for (int i = 0; i < 2 && Valid; i++) { yield return Delay(.8f); Area(_initial, _skill.radius, NormalMultiplier * (decimal)_skill.secondaryPowerRatio); }
        }

        private IEnumerator Sanctuary()
        {
            Visual(_skill.prefab, new Vector3(0, -.6f, 0), Hits * _skill.tickInterval + .3f);
            for (int i = 0; i < Hits && Valid; i++)
            {
                Player lowest = null; double ratio = 1;
                foreach (var player in UserManager.Instance.GetPlayers())
                {
                    if (player == null || player.playerStatus == null || player.playerStatus.HP <= 0) continue;
                    double candidate = (double)player.playerStatus.HP / player.playerStatus.MaxHP;
                    if (candidate < ratio) { lowest = player; ratio = candidate; }
                }
                if (lowest != null)
                {
                    long hpBefore = lowest.playerStatus.HP;
                    lowest.Heal(BalanceMath.Damage(_power, NormalMultiplier));
#if UNITY_EDITOR || LOBBY_DEVICE_QA
                    MageSkillDiagnostics.Record(_skill.id, "heal", lowest.name, lowest.playerStatus.HP - hpBefore, _bloom);
#endif
                    Visual(_skill.secondaryPrefab, lowest.transform.position, .5f);
                }
                yield return Delay(_skill.tickInterval);
            }
        }

        private IEnumerator Void()
        {
            Visual(_skill.prefab, _initial, Hits * _skill.tickInterval + .6f);
            for (int tick = 0; tick < Hits && Valid; tick++)
            {
                Collect(_initial, _skill.radius, _targets, _colliders);
                foreach (var monster in _targets)
                    if ((monster.transform.position - _initial).sqrMagnitude > .12f) monster.ApplyKnockback(_initial - monster.transform.position, 1.8f);
                Area(_initial, _skill.radius, NormalMultiplier, slow: _skill.slowFraction);
                yield return Delay(_skill.tickInterval);
            }
            if (!Valid) yield break;
            Visual(_skill.secondaryPrefab, _initial, .6f);
            Area(_initial, _skill.radius, NormalMultiplier * (decimal)_skill.secondaryPowerRatio);
            yield return Delay(.6f);
        }

        public void Dispose()
        {
            if (_disposed) return; _disposed = true;
#if UNITY_EDITOR || LOBBY_DEVICE_QA
            MageSkillDiagnostics.Record(_skill.id, "end", "", 0, _bloom);
#endif
            foreach (var visual in _visuals) if (visual.effect != null) visual.effect.Release(visual.generation);
            _visuals.Clear();
        }
    }
}
