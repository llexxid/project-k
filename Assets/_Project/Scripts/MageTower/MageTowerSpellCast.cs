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
        private readonly List<Vector3> _boltContacts = new(5);
        private struct FallingStar
        {
            public Vector3 Start, End;
            public PooledSpellVfx Visual;
            public float Age;
        }
        private readonly List<FallingStar> _stars = new(4);
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

        public MageTowerSpellCast(MageTowerManager owner, MageTowerSkillSO skill, long power, int awakening, bool bloom, Monster target, Vector3? position = null)
        {
            _owner = owner; _skill = skill; _power = power; _awakening = awakening; _bloom = bloom;
            _battle = LocalProgression.State.ActiveBattleId; _initial = position ?? target.FootPosition;
            // Broad effects may centre just inside the arena edge; their visible boundary
            // and actual hit centre stay together. Small targeted strikes remain exact.
            if (!position.HasValue && (skill.spellKind == MageSpellKind.Meteor || skill.spellKind == MageSpellKind.VoidRift || skill.spellKind == MageSpellKind.VenomMist ||
                ((skill.spellKind == MageSpellKind.Lightning || skill.spellKind == MageSpellKind.ArcaneVolley) && bloom)))
            {
                var camera=MageTowerTargeting.ResolveCamera();
                if(camera!=null)
                {
                    float depth=Mathf.Abs(camera.transform.position.z);
                    float left=camera.ViewportToWorldPoint(new Vector3(.03f,0,depth)).x+1.8f;
                    float right=camera.ViewportToWorldPoint(new Vector3(.97f,0,depth)).x-1.8f;
                    _initial.x=left<=right?Mathf.Clamp(_initial.x,left,right):camera.transform.position.x;
                }
            }
#if UNITY_EDITOR || LOBBY_DEVICE_QA
            MageSkillDiagnostics.Record(skill.id, "begin", target != null ? target.name : "ground", 0, bloom, _initial);
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

        private static void Collect(Vector3 center, float radius, List<Monster> targets, List<Collider2D> colliders, bool ground = false, float groundAspect = .65f)
        {
            targets.Clear();
            var camera = MageTowerTargeting.ResolveCamera();
            foreach (var monster in CombatMotion.Monsters)
            {
                var delta = monster != null ? monster.FootPosition - center : Vector3.zero;
                if (ground) delta.y /= groundAspect;
                if (IsAlive(monster) && delta.sqrMagnitude <= radius*radius &&
                    MageTowerTargeting.IsOnScreen(camera, monster.transform.position)) targets.Add(monster);
            }
        }

        public static bool TryFindTarget(out Monster target) => TryFindTarget(null, out target);
        public static bool TryFindTarget(MageTowerSkillSO skill, out Monster target, bool bloom = false)
        {
            target = null;
            var camera = MageTowerTargeting.ResolveCamera(); if (camera == null) return false;
            float depth = Mathf.Abs(camera.transform.position.z);
            var center = camera.ViewportToWorldPoint(new Vector3(.5f, .5f, depth)); center.z = 0;
            float radius = Vector3.Distance(center, camera.ViewportToWorldPoint(new Vector3(1, 1, depth))) + 2;
            Collect(center, radius, Candidates, CandidateColliders);
            float distance = float.MaxValue; int cluster = -1;
            bool area = skill != null && (skill.spellKind == MageSpellKind.Meteor || skill.spellKind == MageSpellKind.VoidRift ||
                skill.spellKind == MageSpellKind.VenomMist || skill.spellKind == MageSpellKind.StoneSeal || skill.spellKind == MageSpellKind.Lightning ||
                (skill.spellKind == MageSpellKind.ArcaneVolley && bloom));
            foreach (var candidate in Candidates)
            {
                float next = (candidate.FootPosition - center).sqrMagnitude;
                int neighbours = 0;
                if (area) foreach(var other in Candidates)
                    if ((candidate.FootPosition-other.FootPosition).sqrMagnitude <= skill.TargetRadius(bloom)*skill.TargetRadius(bloom)) neighbours++;
                if (neighbours > cluster || (neighbours == cluster && next < distance)) { target = candidate; distance = next; cluster = neighbours; }
            }
            return target != null;
        }

        public static bool NeedsHealing()
            => TryFindHealingPoint(out _);

        public static bool TryFindHealingPoint(out Vector3 point)
        {
            point = default;
            var players = UserManager.Instance?.GetPlayers(); if (players == null) return false;
            Player lowest = null; double ratio = .9;
            foreach (var player in players)
            {
                if (player == null || player.playerStatus == null || player.playerStatus.HP <= 0) continue;
                double current = (double)player.playerStatus.HP / player.playerStatus.MaxHP;
                if (current < ratio) { lowest = player; ratio = current; }
            }
            if (lowest == null) return false;
            point = lowest.VfxFootPosition; return true;
        }

        private PooledSpellVfx Visual(GameObject prefab, Vector3 position, float lifetime, float scale = 1, Transform follow = null, Vector3 followOffset = default)
        {
            if (!Valid || prefab == null) return null;
            var effect = PooledSpellVfx.Spawn(prefab, position, lifetime, follow, followOffset, scale);
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

        private void Area(Vector3 center, float radius, decimal multiplier, float stun = 0, float slow = 0, bool ground = false)
        {
            if (!Valid) return;
            Collect(center, radius, _targets, _colliders, ground);
            int count = Math.Min(_bloom && (_skill.spellKind == MageSpellKind.Lightning || _skill.spellKind == MageSpellKind.ArcaneVolley) ? _skill.bloomMaxTargets : _skill.maxTargets, _targets.Count);
            for (int i = 0; i < count && Valid; i++)
            {
                var monster = _targets[i]; Hit(monster, multiplier, center);
                if (stun > 0) MonsterCCState.Apply(monster, CrowdControlKind.Stun, stun, 0);
                else if (slow > 0) MonsterCCState.Apply(monster, CrowdControlKind.Slow, _skill.controlDuration, slow, slowStyle: SlowVisualKind.Venom);
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
                case MageSpellKind.ArcaneVolley: yield return _bloom ? Meteor() : Volley(); break;
                case MageSpellKind.StoneSeal: yield return Stone(); break;
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
                if (Valid) sfx.PlayOneShot(gain, pitch, SoundChannel.MageTower);
                else Scripts.Core.SFXManager.Instance?.DestroySFX(sfx);
            });
        }

        private decimal NormalMultiplier => _bloom ? (decimal)_skill.bloomPowerMultiplier : 1m;
        private int Hits => MageSkillRules.HitCount(_skill, _awakening);

        private void ImpactShake(float pixels, float duration)
        {
            if (!Valid || !GamePresentationSettings.ScreenShake) return;
            var camera = MageTowerTargeting.ResolveCamera();
            if (camera == null || camera.pixelHeight <= 0) return;
            // Reference-phone pixels scale with width; keep the HUD still and never
            // accumulate amplitudes across a lightning chain.
            float depth = camera.WorldToScreenPoint(_initial).z;
            if (depth <= 0) return;
            float pixelStep = Vector3.Distance(camera.ScreenToWorldPoint(new Vector3(0,0,depth)), camera.ScreenToWorldPoint(new Vector3(1,0,depth)));
            float magnitude = pixels * Mathf.Clamp(camera.pixelWidth / 1080f, .65f, 1.4f) * pixelStep;
            var shaker = camera.GetComponent<CameraShaker>() ?? camera.gameObject.AddComponent<CameraShaker>();
            shaker.Shake(duration, magnitude);
#if UNITY_EDITOR || LOBBY_DEVICE_QA
            MageSkillDiagnostics.Record(_skill.id, "shake", "", 0, _bloom);
#endif
        }

        private Vector3 LightningContact(int index)
        {
            Vector3 contact = _initial;
            if (index > 0)
            {
                float best = -1;
                for (int attempt = 0; attempt < 16; attempt++)
                {
                    var candidate = _initial + (Vector3)(UnityEngine.Random.insideUnitCircle * MageSkillRules.LightningScatterRadius);
                    float nearest = float.MaxValue;
                    foreach (var previous in _boltContacts) nearest = Mathf.Min(nearest, (candidate - previous).sqrMagnitude);
                    if (nearest > best) { best = nearest; contact = candidate; }
                    if (nearest >= .45f * .45f) break;
                }
            }
            _boltContacts.Add(contact);
            return contact;
        }

        private IEnumerator Lightning()
        {
            if (_bloom)
            {
                // Telegraph stays on the battlefield; the HUD never receives a full-screen flash.
                // Fit the cloud/column into the battle view without moving the damage centre
                // away from the selected enemies on short displays or near the upper edge.
                float cloudLift = 3.35f;
                var camera = MageTowerTargeting.ResolveCamera();
                if (camera != null)
                {
                    float top = camera.ViewportToWorldPoint(new Vector3(.5f, .77f, Mathf.Abs(camera.transform.position.z))).y;
                    cloudLift = Mathf.Clamp(top - _initial.y - .72f, .8f, 3.35f);
                }
                Visual(_skill.bloomCastingPrefab, _initial + Vector3.up * cloudLift, 2.3f);
                yield return Delay(1.84f); if (!Valid) yield break;
                var thunder = Visual(_skill.bloomPrefab, _initial, .75f);
                if (thunder != null && thunder.transform.childCount > 0)
                {
                    // Layer0 is the authored bolt; Layer1 is the ground impact and keeps
                    // its full width. Read prefab values on every cast to avoid pool drift.
                    var column = thunder.transform.GetChild(0);
                    var authored = _skill.bloomPrefab.transform.GetChild(0);
                    float ratio = cloudLift / 3.35f;
                    var scale = authored.localScale; scale.y *= ratio; column.localScale = scale;
                    var offset = authored.localPosition; offset.y *= ratio; column.localPosition = offset;
                }
                yield return Delay(.16f); if (!Valid) yield break;
                Sound(.85f,.8f);
                ImpactShake(8, .24f);
                Area(_initial, _skill.bloomRadius, (decimal)_skill.bloomPowerMultiplier);
                yield return Delay(.75f);
            }
            else
                for (int i = 0; i < Hits && Valid; i++)
                {
                    // Original ThunderEffects clip: OnHit at frame 2 / 12 fps starts
                    // the next bolt immediately, scattered around the first contact.
                    // Awakening extends this same chain; bloom keeps its own choreography.
                    Vector3 contact = LightningContact(i);
                    Visual(_skill.prefab, contact, 5f / 12f);
#if UNITY_EDITOR || LOBBY_DEVICE_QA
                    MageSkillDiagnostics.Record(_skill.id, "bolt", i.ToString(), i + 1, false, contact);
#endif
                    yield return Delay(2f / 12f); if (!Valid) yield break;
                    Sound(i == 0 ? .55f : .38f);
                    ImpactShake(2.5f, .10f);
                    Area(contact, _skill.radius, 1m);
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
                var position = target.Monster.FootPosition;
                Visual(_skill.bloomPrefab, position, .9f);
                yield return Delay(.17f); if(!Valid || !target.Alive) yield break;
                Sound(.7f,.85f);
                Hit(target.Monster, (decimal)_skill.bloomPowerMultiplier, position);
                yield return Delay(.9f); yield break;
            }
            int volleys = _bloom ? 2 : 1, count = _bloom ? _skill.baseHits * 2 : Hits;
            for (int volley = 0; volley < volleys && Valid; volley++)
            {
                _lineTargets.Clear();
                for (int i = 0; i < count && Valid; i++)
                {
                    Collect(_initial, 20, _targets, _colliders); if (_targets.Count == 0) yield break;
                    var target = _targets[i % _targets.Count]; var position = target.FootPosition;
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
                    foreach(var pending in _lineTargets) if(pending.Alive) Hit(pending.Monster,(decimal)_skill.bloomAreaPowerMultiplier,pending.Monster.FootPosition);
                }
                yield return Delay(_bloom ? .38f : .4f);
            }
        }

        private IEnumerator Volley()
        {
            const float flight = .48f;
            int launched = 0;
            float nextLaunch = 0, clock = 0;
            while (Valid && (launched < Hits || _stars.Count > 0))
            {
                if (launched < Hits && clock >= nextLaunch)
                {
                    // Uniform area sampling; the landing point never follows an enemy.
                    // Rejection at the battlefield edge avoids piling impacts on a clamp line.
                    Vector3 end = _initial;
                    for (int attempt = 0; attempt < 64; attempt++)
                    {
                        var offset = UnityEngine.Random.insideUnitCircle * _skill.scatterRadius;
                        var candidate = _initial + new Vector3(offset.x, offset.y * .65f, 0);
                        if (MageTowerManager.IsValidAimPoint(candidate)) { end = candidate; break; }
                    }
                    Vector3 start = end + new Vector3(1.05f + (launched % 3 - 1) * .18f, 3.2f, 0);
                    _stars.Add(new FallingStar { Start = start, End = end,
                        Visual = Visual(_skill.prefab, start, flight + .2f) });
#if UNITY_EDITOR || LOBBY_DEVICE_QA
                    MageSkillDiagnostics.Record(_skill.id, "star-launch", "ground", launched, false, end);
#endif
                    launched++; nextLaunch += Mathf.Max(.12f, _skill.tickInterval);
                }
                for (int i = _stars.Count - 1; i >= 0; i--)
                {
                    var star = _stars[i]; star.Age += Time.deltaTime;
                    if (star.Visual != null)
                    {
                        star.Visual.transform.position = Vector3.Lerp(star.Start, star.End, Mathf.Clamp01(star.Age / flight));
                        var heading = star.End - star.Start;
                        star.Visual.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(heading.y, heading.x) * Mathf.Rad2Deg + 135);
                    }
                    if (star.Age < flight) { _stars[i] = star; continue; }
                    Visual(_skill.secondaryPrefab, star.End, .42f);
                    Area(star.End, _skill.radius, 1m, ground: true);
#if UNITY_EDITOR || LOBBY_DEVICE_QA
                    MageSkillDiagnostics.Record(_skill.id, "star-impact", "ground", 0, false, star.End);
#endif
                    star.Visual?.Release(); _stars.RemoveAt(i);
                }
                yield return null; clock += Time.deltaTime;
            }
            yield return Delay(.42f);
        }

        private IEnumerator Persistent()
        {
            bool moving = _skill.spellKind == MageSpellKind.FireTornado;
            Vector3 center = _initial;
            var visual = Visual(_skill.prefab, center, Hits * _skill.tickInterval + .2f);
            yield return Delay(.12f);
            float tick = 0; int emitted = 0;
            while (Valid && emitted < Hits)
            {
                if (moving && TryFindTarget(out var target)) center = Vector3.MoveTowards(center, target.FootPosition, 2.8f * Time.deltaTime);
                if (visual != null) visual.transform.position = center;
                if (tick <= 0)
                {
                    Area(center, _skill.radius, NormalMultiplier, slow: moving ? 0 : _skill.slowFraction);
                    emitted++; tick += _skill.tickInterval;
                }
                yield return null; tick -= Time.deltaTime;
            }
            // Preserve the final pulse interval before the field fades.
            yield return Delay(Mathf.Max(0, tick));
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

        private IEnumerator Meteor()
        {
            // Approach from the centre side so edge targets still show the full falling rock.
            const float flight = 1.5f;
            var travel = new Vector3(_initial.x > 0 ? -1.6f : 1.6f,3.2f,0);
            var falling = Visual(_skill.bloomPrefab, _initial + travel, flight + .2f);
            if (falling != null && travel.x < 0)
                falling.transform.localScale = Vector3.Scale(falling.transform.localScale, new Vector3(-1, 1, 1));
            if (falling != null)
                falling.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(travel.y, travel.x) * Mathf.Rad2Deg - (travel.x < 0 ? 135 : 45));
            float elapsed = 0;
            while (Valid && elapsed < flight)
            {
                // Enter with momentum and gain weight toward contact, without hovering
                // through the first half or rushing the entire flight in under a second.
                float t = Mathf.Clamp01(elapsed / flight);
                if (falling != null) falling.transform.position = _initial + travel * (1 - (.38f * t + .62f * t * t));
                yield return null; elapsed += Time.deltaTime;
            }
            if (!Valid) yield break;
            if (falling != null) falling.Release();
            Visual(_skill.bloomSecondaryPrefab, _initial, _skill.bloomDuration);
            float groundEnd = Time.time + _skill.bloomDuration;
#if UNITY_EDITOR || LOBBY_DEVICE_QA
            MageSkillDiagnostics.Record(_skill.id, "meteor-contact", "ground", 0, true, _initial);
#endif
            yield return Delay(.09f); if (!Valid) yield break;
            Sound(.75f,.78f); ImpactShake(9, .28f);
            Area(_initial, _skill.bloomRadius, (decimal)_skill.bloomPowerMultiplier, ground: true);
            float nextTick = Time.time;
            while (Valid && Time.time < groundEnd)
            {
                bool damageTick = Time.time >= nextTick;
                Collect(_initial, _skill.bloomGroundRadius, _targets, _colliders, ground: true, groundAspect: .42f);
                for (int i = 0; i < _targets.Count && Valid; i++)
                {
                    var monster = _targets[i];
                    if (damageTick && i < _skill.bloomMaxTargets) Hit(monster, (decimal)_skill.bloomAreaPowerMultiplier, _initial);
                    MonsterCCState.Apply(monster, CrowdControlKind.Slow, Mathf.Min(.12f, groundEnd - Time.time),
                        _skill.bloomSlowFraction, slowStyle: SlowVisualKind.Molten);
                }
                if (damageTick) nextTick += _skill.bloomTickInterval;
                yield return null;
            }
#if UNITY_EDITOR || LOBBY_DEVICE_QA
            MageSkillDiagnostics.Record(_skill.id, "meteor-ground-end", "ground", 0, true, _initial);
#endif
        }

        private IEnumerator Sanctuary()
        {
            Visual(_skill.prefab, _initial, Hits * _skill.tickInterval + .64f);
            yield return Delay(.32f);
            for (int i = 0; i < Hits && Valid; i++)
            {
                Player lowest = null; double ratio = 1;
                foreach (var player in UserManager.Instance.GetPlayers())
                {
                    if (player == null || player.playerStatus == null || player.playerStatus.HP <= 0 ||
                        (player.VfxFootPosition - _initial).sqrMagnitude > _skill.radius * _skill.radius) continue;
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
                    Vector3 foot = lowest.VfxFootPosition;
                    Visual(_skill.secondaryPrefab, foot, .5f, follow: lowest.transform, followOffset: foot - lowest.transform.position);
                }
                yield return Delay(_skill.tickInterval);
            }
            yield return Delay(.32f);
        }

        private IEnumerator Void()
        {
            Vector3 center = _initial;
            Visual(_skill.prefab, center + Vector3.up*.5f, Hits * _skill.tickInterval + .6f);
            yield return Delay(.15f);
            float tickTimer = 0; int ticks = 0;
            while (Valid && ticks < Hits)
            {
                int hitCount = 0;
                Collect(center, _skill.PullRadius, _targets, _colliders);
                foreach (var monster in _targets)
                {
                    if (!IsAlive(monster)) continue;
                    Vector3 delta = center - monster.FootPosition;
                    // Suction reaches 50% beyond the visible rim; damage retains its authored radius.
                    if (delta.sqrMagnitude > _skill.PullRadius*_skill.PullRadius) continue;
                    if (delta.sqrMagnitude > .04f) monster.ApplyKnockback(delta, Mathf.Min(1.6f, delta.magnitude * 3));
                    if (tickTimer <= 0 && delta.sqrMagnitude <= _skill.radius * _skill.radius && hitCount++ < _skill.maxTargets)
                    {
                        Hit(monster, NormalMultiplier, center);
                        MonsterCCState.Apply(monster, CrowdControlKind.Slow, _skill.controlDuration, _skill.slowFraction, slowStyle: SlowVisualKind.Void);
                    }
                }
                if (tickTimer <= 0) { ticks++; tickTimer += _skill.tickInterval; }
                yield return null; tickTimer -= Time.deltaTime;
            }
            if (!Valid) yield break;
            yield return Delay(Mathf.Max(0,tickTimer));
            Visual(_skill.secondaryPrefab, center + Vector3.up*.5f, .6f);
            Area(center, _skill.radius, NormalMultiplier * (decimal)_skill.secondaryPowerRatio);
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
