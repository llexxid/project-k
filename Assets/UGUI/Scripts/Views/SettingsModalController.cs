using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace KingdomIdle.UGUI
{
    /// <summary>Shared title/in-game device preferences; independent of account/progression authority.</summary>
    public sealed class SettingsModalController
    {
        SettingsModalView _view;
        bool _isMuted;
        public bool IsOpen => _view != null && _view.gameObject.activeSelf;

        public void Open(UIManager host)
        {
            if (_view == null)
            {
                if (host.Catalog == null || host.Catalog.overlaySettings == null) return;
                var go = Object.Instantiate(host.Catalog.overlaySettings, host.LayerOverlays, false);
                var rt = (RectTransform)go.transform;
                rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
                _view = go.GetComponent<SettingsModalView>();
                if (_view == null) { Object.Destroy(go); Debug.LogError("Settings prefab has no SettingsModalView."); return; }
                Bind(); ModalBackHandler.Bind(go, Close);
            }
            Load();
            _view.gameObject.SetActive(true);
            _view.transform.SetAsLastSibling();
            if (_view.scroll != null) { _view.scroll.StopMovement(); _view.scroll.verticalNormalizedPosition = 1; }
            UITween.PopIn(_view.panel);
        }
        public void Close() { if (_view != null) { PlayerPrefs.Save(); _view.gameObject.SetActive(false); } }

        void Bind()
        {
            _view.outsideCatcher.onClick.AddListener(Close);
            _view.btnSaveClose.onClick.AddListener(Close);
            if (_view.btnClose != null) _view.btnClose.onClick.AddListener(Close);
            _view.btnMute.onClick.AddListener(() =>
            {
                _isMuted = !_isMuted;
                PlayerPrefs.SetInt(UIManager.PrefKeyMute, _isMuted ? 1 : 0);
                GameAudioSettings.Apply(); PlayerPrefs.Save(); RefreshAudio();
            });
            BindVolume(_view.sldVolume, UIManager.PrefKeyVolume);
            BindVolume(_view.sldMusic, GameAudioSettings.MusicKey);
            BindVolume(_view.sldEffects, GameAudioSettings.EffectsKey);
            BindToggle(_view.tglPowerSave, UIManager.PrefKeyPowerSave);
            BindToggle(_view.tglLowSpec, GamePresentationSettings.LowSpecKey);
            BindToggle(_view.tglHideItem, UIManager.PrefKeyHideItem);
            BindToggle(_view.tglDamageText, UIManager.PrefKeyDamageText);
            BindToggle(_view.tglScreenShake, UIManager.PrefKeyScreenShake);
            BindToggle(_view.tglKeepAwake, GamePresentationSettings.KeepAwakeKey);
            for (int i = 0; _view.numberButtons != null && i < _view.numberButtons.Length; i++)
            {
                var style = (NumberStyle)i;
                _view.numberButtons[i].onClick.AddListener(() => { NumberNotation.SetStyle(style); RefreshNotation(); });
            }
            _view.lblVersion.text = "v" + Application.version;
            foreach (var control in new Component[] { _view.tglPush, _view.tglNightPush, _view.btnWithdraw, _view.btnSave })
                if (control != null) control.gameObject.SetActive(false);
        }
        void BindToggle(Toggle toggle, string key)
        {
            if (toggle == null) return;
            toggle.onValueChanged.AddListener(on =>
            {
                PlayerPrefs.SetInt(key, on ? 1 : 0);
                if (key == GamePresentationSettings.LowSpecKey) PlayerPrefs.SetInt("title_ambientMotion", on ? 0 : 1);
                GamePresentationSettings.Apply();
                if (key == UIManager.PrefKeyDamageText) DamageTextBridge.RefreshSettings();
                PlayerPrefs.Save();
            });
        }
        void BindVolume(Slider slider, string key)
        {
            if (slider == null) return;
            slider.onValueChanged.AddListener(value =>
            {
                PlayerPrefs.SetFloat(key, Mathf.Round(value * 100) / 100f);
                GameAudioSettings.Apply(); RefreshAudio();
                // Flush on release, close and background, not every drag frame.
            });
            if (slider.GetComponent<SettingsVolumeCommit>() == null) slider.gameObject.AddComponent<SettingsVolumeCommit>();
        }
        void Load()
        {
            GamePresentationSettings.Apply(); GameAudioSettings.Apply(); NumberNotation.Load();
            _isMuted = GameAudioSettings.Muted;
            _view.sldVolume.SetValueWithoutNotify(GameAudioSettings.Master);
            _view.sldMusic?.SetValueWithoutNotify(GameAudioSettings.Music);
            _view.sldEffects?.SetValueWithoutNotify(GameAudioSettings.Effects);
            _view.tglPowerSave.SetIsOnWithoutNotify(GamePresentationSettings.PowerSave);
            _view.tglLowSpec.SetIsOnWithoutNotify(GamePresentationSettings.LowSpec);
            _view.tglHideItem.SetIsOnWithoutNotify(GamePresentationSettings.HideItemNotifications);
            _view.tglDamageText.SetIsOnWithoutNotify(PlayerPrefs.GetInt(UIManager.PrefKeyDamageText, 1) == 1);
            _view.tglScreenShake.SetIsOnWithoutNotify(GamePresentationSettings.ScreenShake);
            _view.tglKeepAwake?.SetIsOnWithoutNotify(GamePresentationSettings.KeepAwake);
            foreach (var toggle in _view.GetComponentsInChildren<ToggleSwitchView>(true)) toggle.Refresh();
            _view.lblServer.text = "변경 즉시 적용 · 자동 저장";
            _view.btnGoogleChip.interactable = false;
            _view.btnGoogleChip.GetComponentInChildren<TMP_Text>().text = "진행 데이터는 현재 기기에 저장됩니다";
            RefreshAudio(); RefreshNotation();
        }
        void RefreshAudio()
        {
            _view.btnMuteBg.color = _isMuted ? UguiTheme.Bronze : UguiTheme.RusticSurface;
            _view.btnMute.GetComponentInChildren<TMP_Text>().text = _isMuted ? "음소거 켬" : "음소거 끔";
            if (_view.lblVolume != null) _view.lblVolume.text = Mathf.RoundToInt(GameAudioSettings.Master * 100) + "%";
            if (_view.lblMusic != null) _view.lblMusic.text = Mathf.RoundToInt(GameAudioSettings.Music * 100) + "%";
            if (_view.lblEffects != null) _view.lblEffects.text = Mathf.RoundToInt(GameAudioSettings.Effects * 100) + "%";
        }
        void RefreshNotation()
        {
            string[] titles = { "K / M / B", "만 / 억 / 조", "지수 e" };
            for (int i = 0; _view.numberButtons != null && i < _view.numberButtons.Length; i++)
            {
                bool selected = i == (int)NumberNotation.Style;
                var button = _view.numberButtons[i];
                button.GetComponent<Image>().color = selected ? UguiTheme.Bronze : UguiTheme.RusticSurface;
                button.GetComponentInChildren<TMP_Text>().text = titles[i] + (selected ? "\n선택됨" : "\n선택");
            }
            if (_view.numberPreview != null)
                _view.numberPreview.text = "표시 예시   " + NumberNotation.Format(12345) + "  ·  " + NumberNotation.Format(1234567890L) +
                    "\n재화 · 피해 · 능력치에 적용";
            if (_view.numberHint != null) _view.numberHint.text = (NumberNotation.Style == NumberStyle.Standard ? "1,000배마다 K → M → B → T → Qa → Qi" :
                NumberNotation.Style == NumberStyle.Korean ? "10,000배마다 만 → 억 → 조 → 경" : "e는 10의 거듭제곱 · 1e6 = 1,000,000") + "\n정확한 재화는 상단 재화를 눌러 확인";
        }
    }
}
