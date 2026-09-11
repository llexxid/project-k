using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace KingdomIdle.UGUI
{
    /// <summary>
    /// 하단 탭 버튼 (아이콘 스프라이트 + 라벨 + 상단 인디케이터).
    /// tab-btn-selected USS 클래스 대응 시각 상태를 코드로 적용한다.
    /// 아이콘은 픽셀 아트 키트 스프라이트 (폰트 글리프 ⚔♞✦ 는 Galmuri11에 없어 이미지로 대체).
    /// </summary>
    public sealed class MainTabButtonView : MonoBehaviour
    {
        [SerializeField] internal Button button;
        [SerializeField] internal Image background;
        [SerializeField] internal Image icon;
        [SerializeField] internal TMP_Text label;
        [SerializeField] internal Image indicator;
        [SerializeField] internal RectTransform content;

        /// <summary>선택 시 교체할 눌림 상태 스프라이트(픽셀 키트). 생성기가 채운다.</summary>
        [SerializeField] internal Sprite bgNormalSprite;
        [SerializeField] internal Sprite bgSelectedSprite;

        public Button Button => button;

        public bool IsSelected => _selected;
        private bool _selected;
        private Coroutine _motion;
        private Vector2 _iconRest;
        private bool _initialized;

        private void Awake() => Initialize();
        private void Initialize()
        {
            if (_initialized) return;
            _initialized = true;
            if (icon != null) _iconRest = icon.rectTransform.anchoredPosition;
        }

        private void OnEnable()
        {
            Initialize();
            if (content != null) content.localScale = Vector3.one;
            Apply(_selected ? 1 : 0);
            if (_selected) _motion = StartCoroutine(Animate(1, 1));
        }

        private void OnDisable()
        {
            if (_motion != null) StopCoroutine(_motion);
            _motion = null;
            if (icon != null) icon.rectTransform.anchoredPosition = _iconRest;
        }

        public void SetSelected(bool selected)
        {
            Initialize();
            if (_selected == selected) return;
            _selected = selected;
            if (_motion != null) StopCoroutine(_motion);
            _motion = null;
            if (!isActiveAndEnabled) return;
            float from = icon != null ? Mathf.Clamp01((icon.rectTransform.anchoredPosition.y - _iconRest.y) / 6f) : (selected ? 0 : 1);
            _motion = StartCoroutine(Animate(from, selected ? 1 : 0));
        }

        private IEnumerator Animate(float from, float to)
        {
            for (float t = 0; t < 1; t += Time.unscaledDeltaTime / .16f)
            {
                Apply(Mathf.Lerp(from, to, Mathf.SmoothStep(0, 1, t)));
                yield return null;
            }
            Apply(to);
            // Labels and touch targets stay still; only the selected indicator breathes.
            while (_selected)
            {
                if (indicator != null)
                {
                    var c = UguiTheme.BronzeLight;
                    c.a = .9f + .1f * Mathf.Sin(Time.unscaledTime * 1.8f);
                    indicator.color = c;
                }
                yield return null;
            }
            _motion = null;
        }

        private void Apply(float amount)
        {
            if (background != null)
                background.color = Color.Lerp(Color.clear, UguiTheme.RusticSurface, amount);
            var ink = Color.Lerp(UguiTheme.NavMuted, UguiTheme.Parchment, amount);
            if (icon != null)
            {
                icon.color = ink;
                icon.rectTransform.anchoredPosition = _iconRest + Vector2.up * (6 * amount);
            }
            if (label != null) label.color = ink;
            if (indicator != null)
            {
                var c = UguiTheme.BronzeLight;
                c.a = amount;
                indicator.color = c;
            }
        }
    }
}
