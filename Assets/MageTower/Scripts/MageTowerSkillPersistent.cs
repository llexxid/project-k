using System.Collections.Generic;
using UnityEngine;
using Scripts.Core;
using Scripts.Core.inteface;
using Scripts.Monster;

namespace KingdomIdle.MageTower
{
    /// <summary>Fixed area, ticks at .5, 1.0, ... duration, including the final endpoint.</summary>
    public class MageTowerSkillPersistent : MonoBehaviour, IAttackable, IRewardable
    {
        public ulong damage { get; private set; }
        public Vector3 attackerPos => transform.position;
        public GameObject gameobj => gameObject;
        private double _elapsed;
        private float _interval;
        private int _ticks, _totalTicks, _slot;
        private bool _initialized;
        private string _battle;
        private readonly List<Collider2D> _results = new(16);
        private readonly HashSet<Monster> _hit = new();
        public void GiveReward(int gold, int ancientCoin) { }
        public bool Attack(IDamageable target) => target != null && target.TakeDamage(this);
        public void Initialize(ulong dmg, float duration, float tickInterval, float moveSpeed, float arrivalThreshold,
            int slotIndex, int skillId, Transform initialTarget, string sfxLoopName = null)
        {
            _battle = KingdomIdle.Balance.LocalProgression.State.ActiveBattleId;
            damage = dmg; _slot = slotIndex; _interval = .5f;
            _totalTicks = Mathf.RoundToInt(duration / _interval); _ticks = 0; _elapsed = 0; _initialized = true;
            if (initialTarget != null) transform.position = initialTarget.position;
            foreach (var sr in GetComponentsInChildren<SpriteRenderer>(true)) sr.sortingOrder = 1;
        }
        private void Update()
        {
            if (!_initialized) return;
            if (_battle != KingdomIdle.Balance.LocalProgression.State.ActiveBattleId) { _initialized = false; MageTowerManager.Instance?.EndCasting(_slot); Destroy(gameObject); return; }
            _elapsed += Time.deltaTime;
            while (_ticks < _totalTicks && _elapsed + .000001 >= (_ticks + 1) * _interval) { DealDamage(); _ticks++; }
            if (_ticks >= _totalTicks) { _initialized = false; MageTowerManager.Instance?.EndCasting(_slot); Destroy(gameObject); }
        }
        private void DealDamage()
        {
            var filter = new ContactFilter2D(); filter.SetLayerMask(GameLayers.EnemyMask); filter.useTriggers = true;
            _results.Clear(); _hit.Clear();
            Physics2D.OverlapCircle(transform.position, 1.5f, filter, _results);
            foreach (var col in _results)
            {
                var monster = col.GetComponentInParent<Monster>();
                if (monster == null || monster.MonAction == eMonsterAction.Dead || !_hit.Add(monster)) continue;
                Attack(monster); if (_hit.Count >= 6) break;
            }
        }
    }
}
