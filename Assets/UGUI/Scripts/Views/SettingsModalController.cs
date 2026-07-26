using UnityEngine;

namespace KingdomIdle.UGUI
{
    /// <summary>
    /// 환경설정 모달 (UITKUIManager 설정 영역 이식).
    /// PlayerPrefs 키는 기존과 동일 — 세이브 호환 유지.
    /// </summary>
    public sealed class SettingsModalController
    {
        private SettingsModalView _view;
        private UIManager _host;
        private bool _isMuted;

        public bool IsOpen => _view != null && _view.gameObject.activeSelf;

        public void Open(UIManager host)
        {
            _host = host;
            EnsureView();
            if (_view == null) return;

            LoadSettingsToUI();
            _view.gameObject.SetActive(true);
            _view.transform.SetAsLastSibling();
            if (_view.panel != null) UITween.PopIn(_view.panel);
        }

        public void Close()
        {
            if (_view == null) return;
            _view.gameObject.SetActive(false);
        }

        private void EnsureView()
        {
            if (_view != null) return;
            if (_host == null || _host.Catalog == null || _host.Catalog.overlaySettings == null)
            {
                Debug.LogError("[SettingsModal] overlaySettings 프리팹이 카탈로그에 없습니다.");
                return;
            }

            var go = Object.Instantiate(_host.Catalog.overlaySettings, _host.LayerOverlays, false);
            _view = go.GetComponent<SettingsModalView>();
            if (_view == null)
            {
                Debug.LogError("[SettingsModal] SettingsModalView 컴포넌트가 없습니다.");
                Object.Destroy(go);
                return;
            }

            Bind();
            go.SetActive(false);
        }

        private void Bind()
        {
            if (_view.outsideCatcher != null)
                _view.outsideCatcher.onClick.AddListener(Close);

            if (_view.btnGoogleChip != null)
                _view.btnGoogleChip.onClick.AddListener(() => _host.ShowToast("현재는 지원하지 않는 기능입니다."));

            if (_view.btnWithdraw != null)
                _view.btnWithdraw.onClick.AddListener(() => _host.ShowToast("현재는 지원하지 않는 기능입니다."));

            if (_view.btnMute != null)
            {
                _view.btnMute.onClick.AddListener(() =>
                {
                    _isMuted = !_isMuted;
                    ApplyMuteVisual();
                    ApplyVolumeToSystem();
                });
            }

            if (_view.sldVolume != null)
            {
                _view.sldVolume.onValueChanged.AddListener(v =>
                {
                    if (_isMuted) return;
                    AudioListener.volume = v;
                });
            }

            if (_view.btnSave != null)
            {
                _view.btnSave.onClick.AddListener(() =>
                {
                    SaveSettingsFromUI();
                    _host.ShowToast("저장되었습니다.");
                });
            }

            if (_view.btnSaveClose != null)
            {
                _view.btnSaveClose.onClick.AddListener(() =>
                {
                    SaveSettingsFromUI();
                    Close();
                });
            }

            if (_view.lblVersion != null)
            {
                string ver = string.IsNullOrWhiteSpace(Application.version) ? "0.0.1" : Application.version;
                _view.lblVersion.text = $"Version {ver}";
            }
        }

        private void ApplyVolumeToSystem()
        {
            if (_isMuted) AudioListener.volume = 0f;
            else if (_view != null && _view.sldVolume != null) AudioListener.volume = _view.sldVolume.value;
        }

        private void ApplyMuteVisual()
        {
            if (_view == null || _view.btnMuteBg == null) return;
            // is-on 상태: 빨강 강조 (USS .settings-mute-btn.is-on 대응)
            _view.btnMuteBg.color = _isMuted
                ? new Color(220f / 255f, 70f / 255f, 70f / 255f, 0.55f)
                : UguiTheme.SurfaceMid;
        }

        private void LoadSettingsToUI()
        {
            if (_view == null) return;

            float vol = PlayerPrefs.HasKey(UIManager.PrefKeyVolume) ? PlayerPrefs.GetFloat(UIManager.PrefKeyVolume) : 1f;
            bool muted = PlayerPrefs.GetInt(UIManager.PrefKeyMute, 0) == 1;
            bool powerSave = PlayerPrefs.GetInt(UIManager.PrefKeyPowerSave, 0) == 1;
            bool hideItem = PlayerPrefs.GetInt(UIManager.PrefKeyHideItem, 0) == 1;
            bool damageText = PlayerPrefs.GetInt(UIManager.PrefKeyDamageText, 1) == 1;
            bool screenShake = PlayerPrefs.GetInt(UIManager.PrefKeyScreenShake, 1) == 1;
            bool push = PlayerPrefs.GetInt(UIManager.PrefKeyPush, 0) == 1;
            bool nightPush = PlayerPrefs.GetInt(UIManager.PrefKeyNightPush, 0) == 1;

            if (_view.sldVolume != null) _view.sldVolume.SetValueWithoutNotify(vol);
            _isMuted = muted;
            ApplyMuteVisual();
            ApplyVolumeToSystem();

            if (_view.tglPowerSave != null) _view.tglPowerSave.SetIsOnWithoutNotify(powerSave);
            if (_view.tglHideItem != null) _view.tglHideItem.SetIsOnWithoutNotify(hideItem);
            if (_view.tglDamageText != null) _view.tglDamageText.SetIsOnWithoutNotify(damageText);
            if (_view.tglScreenShake != null) _view.tglScreenShake.SetIsOnWithoutNotify(screenShake);
            if (_view.tglPush != null) _view.tglPush.SetIsOnWithoutNotify(push);
            if (_view.tglNightPush != null) _view.tglNightPush.SetIsOnWithoutNotify(nightPush);

            if (_view.lblServer != null) _view.lblServer.text = "현재 서버: null";
        }

        private void SaveSettingsFromUI()
        {
            if (_view == null) return;

            if (_view.sldVolume != null) PlayerPrefs.SetFloat(UIManager.PrefKeyVolume, _view.sldVolume.value);
            PlayerPrefs.SetInt(UIManager.PrefKeyMute, _isMuted ? 1 : 0);
            if (_view.tglPowerSave != null) PlayerPrefs.SetInt(UIManager.PrefKeyPowerSave, _view.tglPowerSave.isOn ? 1 : 0);
            if (_view.tglHideItem != null) PlayerPrefs.SetInt(UIManager.PrefKeyHideItem, _view.tglHideItem.isOn ? 1 : 0);
            if (_view.tglDamageText != null) PlayerPrefs.SetInt(UIManager.PrefKeyDamageText, _view.tglDamageText.isOn ? 1 : 0);
            if (_view.tglScreenShake != null) PlayerPrefs.SetInt(UIManager.PrefKeyScreenShake, _view.tglScreenShake.isOn ? 1 : 0);
            if (_view.tglPush != null) PlayerPrefs.SetInt(UIManager.PrefKeyPush, _view.tglPush.isOn ? 1 : 0);
            if (_view.tglNightPush != null) PlayerPrefs.SetInt(UIManager.PrefKeyNightPush, _view.tglNightPush.isOn ? 1 : 0);
            PlayerPrefs.Save();
        }
    }
}
