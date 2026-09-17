using UnityEngine;
using Scripts.Core;
using Scripts.Core.Manager;

namespace KingdomIdle.UGUI
{
    /// <summary>
    /// 메인 화면 웨이브/스테이지 HUD 컨트롤러 (UGUI).
    /// StageManager 이벤트 7종을 구독한다 — 기존 UIToolkit WaveUIController와 동일 계약.
    /// </summary>
    public static class WaveUIController
    {
        private static WaveHudView _view;
        private static StageManager _sm;
        private static Transform _deathHome;
        private static GameObject _deathPopup;

        public static void Init(WaveHudView view)
        {
            Dispose();

            _view = view;
            if (_view == null)
            {
                Debug.LogWarning("[WaveUIController] WaveHudView가 null — 초기화 생략");
                return;
            }

            _sm = StageManager.Instance;
            if (_sm == null)
            {
                Debug.LogWarning("[WaveUIController] StageManager.Instance가 null — 이벤트 미등록");
                return;
            }
            _deathPopup = _view.deathPopup;
            if (_deathPopup != null)
            {
                _deathHome = _deathPopup.transform.parent;
                ModalBackHandler.Bind(_deathPopup, OnDeathNo);
            }

            // 이벤트 구독
            _sm.OnWaveChanged += HandleWaveChanged;
            _sm.OnLoopModeChanged += HandleLoopModeChanged;
            _sm.OnBossAutoChallengeChanged += HandleBossAutoChallengeChanged;
            _sm.OnDefeatPopupShow += HandleDefeatPopupShow;
            _sm.OnDefeatPopupHide += HandleDefeatPopupHide;
            _sm.OnDeathPopupTick += HandleDeathPopupTick;
            _sm.OnBossTimerTick += HandleBossTimerTick;

            // 버튼/토글 바인딩
            if (_view.btnLoopIcon != null)
                _view.btnLoopIcon.onClick.AddListener(OnLoopIconClicked);

            if (_view.tglBossChain != null)
            {
                _view.tglBossChain.SetIsOnWithoutNotify(_sm.BossAutoChallenge);
                _view.tglBossChain.GetComponent<ToggleSwitchView>()?.Refresh();
                _view.tglBossChain.onValueChanged.AddListener(OnBossChainToggled);
            }

            if (_view.btnDeathYes != null)
                _view.btnDeathYes.onClick.AddListener(OnDeathYes);

            if (_view.btnDeathNo != null)
                _view.btnDeathNo.onClick.AddListener(OnDeathNo);

            // 초기 상태
            SetHidden(_view.bossTimerBar, !_sm.IsBossWave);
            SetHidden(_view.deathPopup, true);
            HandleLoopModeChanged(_sm.IsLoopMode);

            UpdateStageLabel(_sm.CurrentStageNumber, _sm.CurrentWaveNumber, _sm.IsBossWave);
        }

        public static void Dispose()
        {
            if (_view != null)
            {
                if (_view.btnLoopIcon != null) _view.btnLoopIcon.onClick.RemoveListener(OnLoopIconClicked);
                if (_view.tglBossChain != null) _view.tglBossChain.onValueChanged.RemoveListener(OnBossChainToggled);
                if (_view.btnDeathYes != null) _view.btnDeathYes.onClick.RemoveListener(OnDeathYes);
                if (_view.btnDeathNo != null) _view.btnDeathNo.onClick.RemoveListener(OnDeathNo);
            }
            if (_deathPopup != null)
            {
                _deathPopup.SetActive(false);
                if (_deathHome != null) _deathPopup.transform.SetParent(_deathHome, false);
                else Object.Destroy(_deathPopup);
            }
            _deathPopup = null;
            _deathHome = null;
            if (_sm != null)
            {
                _sm.OnWaveChanged -= HandleWaveChanged;
                _sm.OnLoopModeChanged -= HandleLoopModeChanged;
                _sm.OnBossAutoChallengeChanged -= HandleBossAutoChallengeChanged;
                _sm.OnDefeatPopupShow -= HandleDefeatPopupShow;
                _sm.OnDefeatPopupHide -= HandleDefeatPopupHide;
                _sm.OnDeathPopupTick -= HandleDeathPopupTick;
                _sm.OnBossTimerTick -= HandleBossTimerTick;
                _sm = null;
            }

            _view = null;
        }

        // ── 버튼 핸들러 ──

        private static void OnLoopIconClicked()
        {
            if (_sm != null) _sm.StopLoop();
        }

        private static void OnBossChainToggled(bool value)
        {
            if (_sm != null) _sm.SetBossAutoChallenge(value);
        }

        private static void OnDeathYes()
        {
            if (_sm != null) _sm.ChooseDefeatAction(true);
        }

        private static void OnDeathNo()
        {
            if (_sm != null) _sm.ChooseDefeatAction(false);
        }

        // ── 이벤트 핸들러 ──

        private static void HandleWaveChanged(int stageNum, int wave, bool isBoss)
        {
            UpdateStageLabel(stageNum, wave, isBoss);
            if (_view != null) SetHidden(_view.bossTimerBar, !isBoss);
        }

        private static void HandleLoopModeChanged(bool isLoop)
        {
            if (_view != null && _view.btnLoopIcon != null)
                _view.btnLoopIcon.gameObject.SetActive(isLoop);
            if (_sm != null) UpdateStageLabel(_sm.CurrentStageNumber, _sm.CurrentWaveNumber, _sm.IsBossWave);
        }

        private static void HandleBossAutoChallengeChanged(bool enabled)
        {
            if (_view != null && _view.tglBossChain != null)
            {
                _view.tglBossChain.SetIsOnWithoutNotify(enabled);
                _view.tglBossChain.GetComponent<ToggleSwitchView>()?.Refresh();
            }
        }

        private static void HandleDefeatPopupShow()
        {
            if (_view == null) return;
            bool bossReturn = _sm != null && _sm.IsBossWave && _sm.CurrentDefinition != null &&
                _sm.CurrentDefinition.Type == eStageType.Main;
            if (_view.btnDeathYes != null) _view.btnDeathYes.gameObject.SetActive(!bossReturn);
            if (_view.lblDeathMsg != null) _view.lblDeathMsg.text = bossReturn
                ? "이전 스테이지에서 성장한 뒤 다시 도전하세요.\n시간이 지나면 자동으로 복귀합니다."
                : "다시 도전하거나 전투에 복귀할 수 있습니다.\n시간이 지나면 자동으로 복귀합니다.";
            var ui = UIManager.Instance;
            if (_deathPopup != null && ui != null && ui.LayerPopups != null)
            {
                _deathPopup.transform.SetParent(ui.LayerPopups, false);
                _deathPopup.transform.SetAsLastSibling();
            }
            SetHidden(_view.deathPopup, false);
            var panel = _deathPopup != null ? _deathPopup.transform.Find("Panel") as RectTransform : null;
            if (panel != null) UITween.PopIn(panel, .18f, .96f);
            SetHidden(_view.bossTimerBar, true);
        }

        private static void HandleDefeatPopupHide()
        {
            if (_view != null) SetHidden(_view.deathPopup, true);
            if (_deathPopup != null && _deathHome != null) _deathPopup.transform.SetParent(_deathHome, false);
        }

        private static void HandleDeathPopupTick(float ratio)
        {
            if (_view != null && _view.deathTimerFill != null)
                _view.deathTimerFill.fillAmount = Mathf.Clamp01(ratio);
        }

        private static void HandleBossTimerTick(float ratio)
        {
            if (_view != null && _view.bossTimerFill != null)
                _view.bossTimerFill.fillAmount = Mathf.Clamp01(ratio);
        }

        // ── 유틸 ──

        private static void UpdateStageLabel(int stageNum, int wave, bool isBoss)
        {
            if (_view == null || _view.lblStage == null) return;
            var kind = _sm != null ? StageParser.GetStageType(_sm.CurrentStage) : Scripts.Core.eStageType.Main;
            _view.lblStage.text = kind == Scripts.Core.eStageType.GoldDungeon ? $"골드 던전 · {stageNum}단계"
                : kind == Scripts.Core.eStageType.RubyDungeon ? $"루비 던전 · {stageNum}단계"
                : isBoss
                ? $"보스 {stageNum}"
                : _sm != null && _sm.IsLoopMode ? $"반복 사냥 {stageNum}-{wave}" : $"스테이지 {stageNum}-{wave}";
        }

        private static void SetHidden(GameObject go, bool hidden)
        {
            if (go == null) return;
            go.SetActive(!hidden);
        }
    }
}
