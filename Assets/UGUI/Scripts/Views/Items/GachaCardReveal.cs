using System;
using UnityEngine;
using UnityEngine.UI;

namespace KingdomIdle.UGUI
{
    /// <summary>Result-only, unscaled reveal. Preview cards never acquire this component.</summary>
    [DisallowMultipleComponent]
    public sealed class GachaCardReveal : MonoBehaviour
    {
        private const float RevealDuration = .16f;
        private const float FlashDuration = .22f;
        private GachaCardItemView _card;
        private GachaRevealBorder _border;
        private GraphicState _icon, _fallback, _name, _sub, _frame, _background;
        private Action _onRevealed;
        private float _duration;
        private bool _captured, _revealing;

        public static float DurationFor(eEquipmentRarity rarity)
        {
            switch (rarity)
            {
                case eEquipmentRarity.Rare: return 1f;
                case eEquipmentRarity.Epic: return 2f;
                default: return .55f;
            }
        }

        /// <summary>Call after binding the final card contents, in the same Show frame for every result.</summary>
        public static GachaCardReveal Play(GachaCardItemView card, eEquipmentRarity rarity, Action onRevealed = null)
            => Play(card, DurationFor(rarity), UguiTheme.RarityColor(rarity), onRevealed);

        internal static GachaCardReveal Play(GachaCardItemView card, float duration, Color revealColor, Action onRevealed = null)
        {
            if (card == null) return null;
            var reveal = card.GetComponent<GachaCardReveal>();
            if (reveal == null) reveal = card.gameObject.AddComponent<GachaCardReveal>();
            reveal.Begin(card, duration, revealColor, onRevealed);
            return reveal;
        }

        private void Begin(GachaCardItemView card, float duration, Color revealColor, Action onRevealed)
        {
            CancelAndRestore();
            _card = card;
            _icon = new GraphicState(card.icon);
            _fallback = new GraphicState(card.iconFallback);
            _name = new GraphicState(card.nameLabel);
            _sub = new GraphicState(card.subLabel);
            _frame = new GraphicState(card.frame);
            _background = new GraphicState(card.background);
            _captured = true;
            _revealing = false;
            _onRevealed = onRevealed;
            _duration = duration;
            if (!gameObject.activeInHierarchy)
            {
                CancelAndRestore();
                enabled = false;
                return;
            }

            EnsureBorder();
            SetContentAlpha(0f);
            if (card.frame != null)
            {
                var neutral = UguiTheme.Bronze;
                neutral.a = .28f;
                card.frame.color = neutral;
            }
            if (card.background != null) card.background.color = UguiTheme.RusticPanelDeep;
            _border.Configure(revealColor, GamePresentationSettings.LowSpec);
            _border.SetProgress(0f);
            _border.enabled = true;
            enabled = true;
            // Same-frame ValueTo calls start at zero and share every unscaled frame delta.
            float total = _duration + FlashDuration;
            UITween.ValueTo(this, 0f, total, total, ApplyElapsed, CompleteReveal);
        }

        private void EnsureBorder()
        {
            if (_border != null) return;
            var overlay = new GameObject("RevealBorder", typeof(RectTransform), typeof(CanvasRenderer), typeof(LayoutElement));
            overlay.layer = gameObject.layer;
            var rect = (RectTransform)overlay.transform;
            rect.SetParent(transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            overlay.GetComponent<LayoutElement>().ignoreLayout = true;
            _border = overlay.AddComponent<GachaRevealBorder>();
            _border.raycastTarget = false;
        }

        private void ApplyElapsed(float elapsed)
        {
            if (!_captured || _card == null || !isActiveAndEnabled)
            {
                CancelAndRestore();
                enabled = false;
                return;
            }

            if (elapsed < _duration)
            {
                _border.SetProgress(elapsed / _duration);
                return;
            }

            if (!_revealing)
            {
                _revealing = true;
                _frame.Restore();
                _background.Restore();
            }
            float revealTime = elapsed - _duration;
            float fade = Mathf.Clamp01(revealTime / RevealDuration);
            SetContentAlpha(fade * fade * (3f - 2f * fade));
            float flash = 1f - Mathf.Clamp01(revealTime / FlashDuration);
            _border.SetFlash(flash * flash);
        }

        private void CompleteReveal()
        {
            if (!_captured || !isActiveAndEnabled) return;
            // Restore and detach the callback before it can close or replace this popup.
            var completed = _onRevealed;
            _onRevealed = null;
            RestoreVisuals();
            enabled = false;
            completed?.Invoke();
        }

        private void SetContentAlpha(float alpha)
        {
            _icon.SetAlpha(alpha);
            _fallback.SetAlpha(alpha);
            _name.SetAlpha(alpha);
            _sub.SetAlpha(alpha);
        }

        private void CancelAndRestore()
        {
            UITween.StopValue(this);
            _onRevealed = null;
            RestoreVisuals();
        }

        private void RestoreVisuals()
        {
            if (_captured)
            {
                _icon.Restore();
                _fallback.Restore();
                _name.Restore();
                _sub.Restore();
                _frame.Restore();
                _background.Restore();
                _captured = false;
            }
            if (_border != null) _border.enabled = false;
        }

        private void OnDisable() => CancelAndRestore();

        private void OnDestroy()
        {
            CancelAndRestore();
            if (_border != null) Destroy(_border.gameObject);
        }

        private readonly struct GraphicState
        {
            private readonly Graphic _graphic;
            private readonly Color _color;

            public GraphicState(Graphic graphic)
            {
                _graphic = graphic;
                _color = graphic != null ? graphic.color : Color.white;
            }

            public void SetAlpha(float alpha)
            {
                if (_graphic == null) return;
                var tint = _color;
                tint.a *= alpha;
                if (_graphic.color != tint) _graphic.color = tint;
            }

            public void Restore()
            {
                if (_graphic != null && _graphic.color != _color) _graphic.color = _color;
            }
        }
    }
}
