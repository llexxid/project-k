using UnityEngine;
using Scripts.Core;

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
                _view.tglBossChain.onValueChanged.AddListener(OnBossChainToggled);
            }

            if (_view.btnDeathYes != null)
                _view.btnDeathYes.onClick.AddListener(OnDeathYes);

            if (_view.btnDeathNo != null)
                _view.btnDeathNo.onClick.AddListener(OnDeathNo);

            // 초기 상태
            SetHidden(_view.bossTimerBar, true);
            SetHidden(_view.deathPopup, true);
            if (_view.btnLoopIcon != null) _view.btnLoopIcon.gameObject.SetActive(false);

            UpdateStageLabel(_sm.StageNumber, _sm.WaveNumber, _sm.IsBossWave);
        }

        public static void Dispose()
        {
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
        }

        private static void HandleBossAutoChallengeChanged(bool enabled)
        {
            if (_view != null && _view.tglBossChain != null)
                _view.tglBossChain.SetIsOnWithoutNotify(enabled);
        }

        private static void HandleDefeatPopupShow()
        {
            if (_view == null) return;
            SetHidden(_view.deathPopup, false);
            SetHidden(_view.bossTimerBar, true);
        }

        private static void HandleDefeatPopupHide()
        {
            if (_view != null) SetHidden(_view.deathPopup, true);
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
            _view.lblStage.text = isBoss
                ? $"보스 {stageNum}"
                : $"스테이지 {stageNum}-{wave}";
        }

        private static void SetHidden(GameObject go, bool hidden)
        {
            if (go == null) return;
            go.SetActive(!hidden);
        }
    }
}
