using Scripts.Core;
using Scripts.Monster;
using UnityEngine;

namespace KingdomIdle.Combat
{
    /// <summary>Actor-owned indicators: independent clocks remain in the actual gameplay states.</summary>
    [DefaultExecutionOrder(100), DisallowMultipleComponent]
    public sealed class CombatStatusVisuals : MonoBehaviour
    {
        Monster _monster;
        MonsterCCState _control;
        Player _player;
        readonly SpriteRenderer[] _icons = new SpriteRenderer[4];
        public static void Ensure(Component actor)
        {
            var view = actor.GetComponent<CombatStatusVisuals>() ?? actor.gameObject.AddComponent<CombatStatusVisuals>();
            view._control = actor.GetComponent<MonsterCCState>();
            view.enabled = true;
        }
        void Awake() { _monster = GetComponent<Monster>(); _player = GetComponent<Player>(); }
        void LateUpdate()
        {
            bool alive = _monster != null ? _monster.isActiveAndEnabled && _monster.MonAction != eMonsterAction.Dead :
                _player != null && _player.isActiveAndEnabled && !_player.IsDead;
            if (!alive) { enabled = false; return; }
            var art = CombatStatusArt.Current;
            if (art == null) { enabled = false; return; }
            bool stun = _control != null && _control.IsStunned;
            bool slow = _control != null && _control.SlowFraction > 0;
            bool taunt = _monster != null && _monster.HasTaunt;
            bool shield = _player != null && _player.ShieldHP > 0;
            Vector3 foot = _monster != null ? _monster.FootPosition : _player.VfxFootPosition;
            Vector3 head = _monster != null ? _monster.HeadPosition : _player.VfxHeadPosition;
            // HP bars sit at head + .15. Status symbols have their own row above that bar.
            Draw(0, stun, Frame(art.stun, 8), head + new Vector3(taunt ? -.22f : 0, .40f, 0), Color.white, .9f, false);
            Color slowColor = _control != null && _control.SlowStyle == SlowVisualKind.Void ? new Color(.73f,.48f,.94f,.85f) :
                _control != null && _control.SlowStyle == SlowVisualKind.Venom ? new Color(.53f,.90f,.32f,.9f) : new Color(.50f,.80f,1,.85f);
            Draw(1, slow, Frame(art.footRing, 12), foot + Vector3.up * .38f, slowColor, 1.1f, true);
            Draw(2, taunt, art.taunt, head + new Vector3(stun ? .32f : 0, .40f, 0), Color.white, .36f, false);
            Draw(3, shield, Frame(art.footRing, 12), foot + Vector3.up * .47f, new Color(.52f,.79f,1,.9f), 1.35f, true);
            if (!stun && !slow && !taunt && !shield) enabled = false;
        }
        static Sprite Frame(Sprite[] frames, float fps) => frames != null && frames.Length > 0 ? frames[(int)(Time.time * fps) % frames.Length] : null;
        void Draw(int slot, bool visible, Sprite sprite, Vector3 position, Color color, float width, bool ground)
        {
            var renderer = _icons[slot];
            if (!visible || sprite == null) { if (renderer != null) renderer.enabled = false; return; }
            if (renderer == null)
            {
                var child = new GameObject(new[] { "Status_Stun", "Status_Slow", "Status_Taunt", "Status_Shield" }[slot]);
                child.transform.SetParent(transform, false);
                renderer = _icons[slot] = child.AddComponent<SpriteRenderer>();
                renderer.sortingLayerName = ground ? "Default" : "CombatVFX";
                renderer.sortingOrder = ground ? CombatVfxOrder.FootStatus + 1 : CombatVfxOrder.OverheadStatus + slot;
            }
            renderer.enabled = true;
            if (renderer.sprite != sprite) renderer.sprite = sprite;
            if (renderer.color != color) renderer.color = color;
            renderer.transform.position = position;
            renderer.transform.rotation = Quaternion.identity;
            float scale = width / sprite.bounds.size.x;
            Vector3 parent = transform.lossyScale;
            renderer.transform.localScale = new Vector3(scale / parent.x, scale / parent.y, 1);
        }
        void OnDisable() { foreach (var icon in _icons) if (icon != null) icon.enabled = false; }
    }
}
