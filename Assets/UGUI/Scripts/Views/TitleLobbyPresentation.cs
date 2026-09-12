using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KingdomIdle.UGUI
{
    /// <summary>Layered title art, safe-area layout and a single allocation-free ambient loop.</summary>
    [DisallowMultipleComponent]
    public sealed class TitleLobbyPresentation : MonoBehaviour
    {
        [Serializable]
        public sealed class MotionLayer
        {
            public RectTransform target;
            public Vector2 rest;
            public float angle;
            public float rise;
            public float period = 4f;
            public float phase;
            public bool startled;
        }

        [Serializable]
        public sealed class LocalizedLabel
        {
            public TMP_Text target;
            public string korean;
            public string english;
        }

        public RectTransform artViewport;
        public RectTransform artWorld;
        public RectTransform heroes;
        public RectTransform landscapePanel;
        public Image landscapeFallback;
        public Image footerShade;
        public RectTransform logoRoot;
        public Image logo;
        public Sprite koreanLogo;
        public Sprite englishLogo;
        public RectTransform footer;
        public RectTransform loginBox;
        public Button languageButton;
        public RectTransform accountButton;
        public GameObject languagePopup;
        public RectTransform languagePanel;
        public TMP_Text currentLanguage;
        public Button koreanButton, englishButton, languageDismiss;
        public Image koreanSelected, englishSelected;
        public LobbyActorRig[] actorRigs = Array.Empty<LobbyActorRig>();
        public Image[] siegeLights = Array.Empty<Image>();
        public Image crystalGlow;
        public LobbyStormFlash stormFlash;
        public TMP_Text pressHint;
        public TMP_Text versionLabel;
        public Image magicGlow;
        public MotionLayer[] layers = Array.Empty<MotionLayer>();
        public RectTransform[] motes = Array.Empty<RectTransform>();
        public LocalizedLabel[] labels = Array.Empty<LocalizedLabel>();

        const string LanguageKey = "title_language";
        static readonly Unity.Profiling.ProfilerMarker AmbientMarker = new("KingdomIdle.LobbyAnimation");
        readonly Vector3[] _corners = new Vector3[4];
        readonly WaitForSecondsRealtime _normalTick = new WaitForSecondsRealtime(1f / 30f);
        readonly WaitForSecondsRealtime _powerTick = new WaitForSecondsRealtime(1f / 15f);
        RectTransform _root;
        Canvas _canvas;
        Coroutine _ambient;
        Vector2 _lastSize;
        Rect _lastSafe;
        Vector2Int _lastScreen;
        Vector2 _logoRest;
        bool _english;
        bool _motion;
        bool _focused = true;
        bool _paused;
        bool _layoutDirty = true;
        float _clock;

        public bool English => _english;
        public bool AmbientMotion => _motion;
        public string Localize(string korean, string english) => _english ? english : korean;

        void Awake()
        {
            _root = (RectTransform)transform;
            _canvas = GetComponentInParent<Canvas>()?.rootCanvas;
            _english = PlayerPrefs.GetString(LanguageKey,
                Application.systemLanguage == SystemLanguage.Korean ? "ko" : "en") == "en";
            GamePresentationSettings.Apply();
            _motion = !GamePresentationSettings.LowSpec;
            languageButton.onClick.AddListener(OpenLanguagePopup);
            if (koreanButton != null) koreanButton.onClick.AddListener(ChooseKorean);
            if (englishButton != null) englishButton.onClick.AddListener(ChooseEnglish);
            if (languageDismiss != null) languageDismiss.onClick.AddListener(DismissLanguage);
            if (languagePopup != null) ModalBackHandler.Bind(languagePopup, DismissLanguage);
        }

        void OnEnable()
        {
            _layoutDirty = true;
            GamePresentationSettings.Changed += OnPresentationChanged;
            _motion = !GamePresentationSettings.LowSpec;
            RefreshLanguage();
            RestartAmbient();
        }

        void OnDisable()
        {
            GamePresentationSettings.Changed -= OnPresentationChanged;
            CloseLanguagePopup();
            StopAmbient();
            ResetPose();
        }

        void OnDestroy()
        {
            if (languageButton != null) languageButton.onClick.RemoveListener(OpenLanguagePopup);
            if (koreanButton != null) koreanButton.onClick.RemoveListener(ChooseKorean);
            if (englishButton != null) englishButton.onClick.RemoveListener(ChooseEnglish);
            if (languageDismiss != null) languageDismiss.onClick.RemoveListener(DismissLanguage);
        }

        void OnRectTransformDimensionsChange() => _layoutDirty = true;
        void OnApplicationFocus(bool focused) { _focused = focused; RestartAmbient(); }
        void OnApplicationPause(bool paused) { _paused = paused; RestartAmbient(); }

        void LateUpdate()
        {
            if (_root == null) return;
            if (_layoutDirty || _root.rect.size != _lastSize || Screen.safeArea != _lastSafe ||
                Screen.width != _lastScreen.x || Screen.height != _lastScreen.y)
                RefreshLayout();
        }

        public void SetLanguage(bool english, bool persist = false)
        {
            _english = english;
            if (persist) { PlayerPrefs.SetString(LanguageKey, english ? "en" : "ko"); PlayerPrefs.Save(); }
            RefreshLanguage();
            _layoutDirty = true;
        }

        void ChooseKorean() { SetLanguage(false, true); CloseLanguagePopup(); }
        void ChooseEnglish() { SetLanguage(true, true); CloseLanguagePopup(); }
        void DismissLanguage() => CloseLanguagePopup();
        public void OpenLanguagePopup()
        {
            if (languagePopup == null) return;
            RefreshLanguage();
            languagePopup.SetActive(true);
            languagePopup.transform.SetAsLastSibling();
            UITween.PopIn(languagePanel, .18f, .96f);
        }
        public bool CloseLanguagePopup()
        {
            if (languagePopup == null || !languagePopup.activeSelf) return false;
            languagePopup.SetActive(false);
            return true;
        }
        public void SetAmbientMotion(bool enabled, bool persist = false)
        {
            GamePresentationSettings.SetLowSpec(!enabled, persist);
            OnPresentationChanged();
        }

        void OnPresentationChanged()
        {
            _motion = !GamePresentationSettings.LowSpec;
            RefreshLanguage();
            RestartAmbient();
        }

        void RefreshLanguage()
        {
            if (logo != null) logo.sprite = _english ? englishLogo : koreanLogo;
            foreach (var label in labels)
                if (label.target != null) label.target.text = _english ? label.english : label.korean;
            if (currentLanguage != null) currentLanguage.text = Localize("현재 언어: 한국어", "Current language: English");
            if (koreanSelected != null) koreanSelected.gameObject.SetActive(!_english);
            if (englishSelected != null) englishSelected.gameObject.SetActive(_english);
            if (versionLabel != null) versionLabel.text = "v" + Application.version;
        }

        public void RefreshLayout()
        {
            if (_root == null) _root = (RectTransform)transform;
            if (_canvas == null) _canvas = GetComponentInParent<Canvas>()?.rootCanvas;
            _lastSize = _root.rect.size;
            _lastSafe = Screen.safeArea;
            _lastScreen = new Vector2Int(Screen.width, Screen.height);
            _layoutDirty = false;

            // Screen_Title lives inside the existing SafeArea. Only artwork bleeds to the full canvas.
            if (_canvas != null)
            {
                ((RectTransform)_canvas.transform).GetWorldCorners(_corners);
                var lo = _root.InverseTransformPoint(_corners[0]);
                var hi = _root.InverseTransformPoint(_corners[2]);
                artViewport.anchorMin = artViewport.anchorMax = new Vector2(.5f, .5f);
                artViewport.anchoredPosition = (lo + hi) * .5f;
                artViewport.sizeDelta = hi - lo;
            }
            float width = Mathf.Max(1, _lastSize.x);
            float height = Mathf.Max(1, _lastSize.y);
            var full = artViewport.rect.size;
            float safeBottom = Mathf.Max(0, _root.rect.yMin - (artViewport.anchoredPosition.y - full.y * .5f));
            if (footerShade != null) footerShade.rectTransform.sizeDelta = new Vector2(0, 520f + safeBottom);
            float scale = Mathf.Max(full.x, full.y) / 2048f;
            artWorld.localScale = Vector3.one * scale;
            artWorld.anchoredPosition = Vector2.zero;
            if (heroes != null) heroes.localScale = Vector3.one * (width / height > .68f && width <= height ? .86f : 1f);
            if (landscapeFallback != null) landscapeFallback.gameObject.SetActive(width > height);
            if (landscapePanel != null)
            {
                landscapePanel.gameObject.SetActive(width > height);
                landscapePanel.sizeDelta = new Vector2(full.x * .48f, full.y);
            }

            // Reserve the band below the wordmark for the distant siege on short/tablet displays too.
            float logoWidth = Mathf.Min(width * .82f, height * .45f);
            float topPadding = Mathf.Max(24f, height * .02f);
            if (width <= height)
            {
                // The cloud and dragon wings must stay below the wordmark, including
                // on tablets and foldables whose safe-area inset reduces vertical space.
                float availableHeight = height * .5f - artViewport.anchoredPosition.y
                    - scale * 448f - topPadding - 16f;
                logoWidth = Mathf.Min(logoWidth, Mathf.Max(160f, availableHeight) / .60f);
            }
            float logoHeight = logoWidth * .60f;
            logoRoot.sizeDelta = new Vector2(logoWidth, logoHeight);
            _logoRest = new Vector2(0, -topPadding);
            logoRoot.anchoredPosition = _logoRest;

            // Keep readable controls in safe area. Tablet width does not stretch the buttons.
            footer.sizeDelta = new Vector2(Mathf.Min(960f, width - 48f), 310f);
            footer.anchoredPosition = new Vector2(0, 34f);
            accountButton.sizeDelta = new Vector2(380, 144);
            accountButton.anchoredPosition = new Vector2(0, -50);
            var languageRect = (RectTransform)languageButton.transform;
            languageRect.sizeDelta = new Vector2(144, 144);
            languageRect.anchoredPosition = new Vector2(24, -24);
            pressHint.rectTransform.anchoredPosition = new Vector2(0, 86);
            if (loginBox != null)
                loginBox.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Mathf.Min(820f, width - 64f));
            if (languagePanel != null)
            {
                languagePanel.sizeDelta = new Vector2(Mathf.Min(490, width - 48), 414);
                languagePanel.anchoredPosition = new Vector2(24, -180);
            }

            // Landscape is not a configured mobile orientation, but has a deliberate compact fallback.
            if (width > height)
            {
                artWorld.localScale = Vector3.one * (height / 1780f);
                artWorld.anchoredPosition = new Vector2(width * .23f, 0);
                logoRoot.sizeDelta = new Vector2(width * .4f, height * .25f);
                _logoRest = new Vector2(-width * .25f, -144f);
                logoRoot.anchoredPosition = _logoRest;
                footer.sizeDelta = new Vector2(width * .46f, 310f);
                footer.anchoredPosition = new Vector2(-width * .25f, 20f);
                accountButton.sizeDelta = new Vector2(Mathf.Min(380, footer.rect.width * .82f), 100);
                accountButton.anchoredPosition = new Vector2(0, -10);
                pressHint.rectTransform.anchoredPosition = new Vector2(0, 110);
                languageRect.sizeDelta = new Vector2(112, 112);
                if (languagePanel != null) languagePanel.anchoredPosition = new Vector2(24, -148);
            }
            pressHint.rectTransform.sizeDelta = new Vector2(Mathf.Min(900, footer.rect.width - 16), 90);
            pressHint.fontSize = Mathf.Min(48, (footer.rect.width - 16) / 18.2f);
        }

        void RestartAmbient()
        {
            StopAmbient();
            if (!isActiveAndEnabled) return;
            ResetPose();
            if (_motion && _focused && !_paused) _ambient = StartCoroutine(Animate());
        }

        void StopAmbient()
        {
            if (_ambient != null) StopCoroutine(_ambient);
            _ambient = null;
        }

        void ResetPose()
        {
            foreach (var layer in layers)
                if (layer.target != null)
                {
                    layer.target.anchoredPosition = layer.rest;
                    layer.target.localRotation = Quaternion.identity;
                }
            if (logoRoot != null) { logoRoot.localScale = Vector3.one; logoRoot.anchoredPosition = _logoRest; }
            if (pressHint != null) pressHint.alpha = 1;
            if (magicGlow != null) magicGlow.color = new Color(.5f, .93f, 1f, .28f);
            foreach (var rig in actorRigs) if (rig != null) rig.ResetPose();
            foreach (var light in siegeLights) if (light != null) light.gameObject.SetActive(_motion);
            if (crystalGlow != null) crystalGlow.gameObject.SetActive(_motion);
            if (stormFlash != null) { stormFlash.SetFlash(0, 0); stormFlash.gameObject.SetActive(_motion); }
            foreach (var mote in motes) if (mote != null) mote.gameObject.SetActive(_motion);
        }

        IEnumerator Animate()
        {
            bool powerSave = PlayerPrefs.GetInt(UIManager.PrefKeyPowerSave, 0) != 0 || SystemInfo.systemMemorySize < 3000;
            var tick = powerSave ? _powerTick : _normalTick;
            float previous = Time.unscaledTime;
            while (true)
            {
                float now = Time.unscaledTime;
                _clock += Mathf.Min(now - previous, .1f);
                previous = now;
                SamplePose(_clock);
                yield return tick;
            }
        }

        /// <summary>Deterministic pose sampling also lets editor QA compare two real rendered frames.</summary>
        public void SamplePose(float time)
        {
            using var sample = AmbientMarker.Auto();
            foreach (var rig in actorRigs) if (rig != null) rig.Sample(time);
            foreach (var layer in layers)
            {
                if (layer.target == null) continue;
                float period = Mathf.Max(.1f, layer.period);
                float wave = Mathf.Sin(time * (Mathf.PI * 2 / period) + layer.phase);
                float shake = 0;
                if (layer.startled)
                {
                    float burst = Mathf.Repeat(time + layer.phase, period);
                    wave = burst < .65f ? Mathf.Sin(burst * 57f) * Mathf.Sin(burst / .65f * Mathf.PI) : 0;
                    shake = wave * 3.2f;
                }
                layer.target.anchoredPosition = layer.rest + new Vector2(shake, wave * layer.rise);
                layer.target.localRotation = Quaternion.Euler(0, 0, wave * layer.angle);
            }
            logoRoot.anchoredPosition = _logoRest + new Vector2(0, Mathf.Sin(time * .85f) * 4f);
            if (pressHint != null) pressHint.alpha = .74f + .26f * (.5f + .5f * Mathf.Sin(time * 1.7f));
            if (magicGlow != null) magicGlow.color = new Color(.5f, .93f, 1f, .22f + .25f * (.5f + .5f * Mathf.Sin(time * 2.2f)));
            for (int i = 0; i < siegeLights.Length; i++)
            {
                var light = siegeLights[i];
                float flicker = .5f + .3f * Mathf.Sin(time * 4.3f + i * 2) + .2f * Mathf.Sin(time * 8.1f + i);
                light.color = new Color(1f, .44f, .12f, .12f + flicker * .22f);
                light.rectTransform.localScale = Vector3.one * (.85f + flicker * .28f);
            }
            if (crystalGlow != null) crystalGlow.color = new Color(.25f, .49f, 1f, .10f + .16f * (.5f + .5f * Mathf.Sin(time * 1.35f)));
            if (stormFlash != null) stormFlash.SetFlash(LobbyStormFlash.IntensityAt(time), (int)(time / LobbyStormFlash.Period) & 1);
            for (int i = 0; i < motes.Length; i++)
            {
                float t = Mathf.Repeat(time * .027f + i * .173f, 1f);
                motes[i].anchoredPosition = new Vector2(-350 + i * 131 + Mathf.Sin(time * .35f + i) * 24, -520 + t * 570);
                float s = Mathf.Sin(t * Mathf.PI) * (i % 2 == 0 ? 1 : .6f);
                motes[i].localScale = Vector3.one * s;
            }
        }
    }
}
