using UnityEngine;
using UnityEngine.UIElements;
using Scripts.Core;

namespace KingdomIdle.UIToolkit
{
    public static class WaveUIController
    {
        private static Label _lblStage;
        private static Button _btnLoopIcon;
        private static Toggle _tglBossChain;
        private static Label _lblBossChain;

        // 보스 타이머
        private static VisualElement _bossTimerBar;
        private static VisualElement _bossTimerFill;

        // 사망 팝업
        private static VisualElement _deathPopup;
        private static Label _lblDeathMsg;
        private static Button _btnDeathYes;
        private static Button _btnDeathNo;
        private static VisualElement _deathTimerBar;
        private static VisualElement _deathTimerFill;

        private static WaveManager _wm;

        public static void Init(VisualElement root)
        {
            Dispose();

            _wm = WaveManager.Instance;
            if (_wm == null)
            {
                Debug.LogWarning("[WaveUIController] WaveManager.Instance가 null — 이벤트 미등록");
                return;
            }

            // 요소 바인딩
            _lblStage = root.Q<Label>("LblStage");
            _btnLoopIcon = root.Q<Button>("BtnLoopIcon");
            _tglBossChain = root.Q<Toggle>("TglBossChain");
            _lblBossChain = root.Q<Label>("LblBossChain");

            _bossTimerBar = root.Q<VisualElement>("BossTimerBar");
            _bossTimerFill = root.Q<VisualElement>("BossTimerFill");

            _deathPopup = root.Q<VisualElement>("DeathPopup");
            _lblDeathMsg = root.Q<Label>("LblDeathMsg");
            _btnDeathYes = root.Q<Button>("BtnDeathYes");
            _btnDeathNo = root.Q<Button>("BtnDeathNo");
            _deathTimerBar = root.Q<VisualElement>("DeathTimerBar");
            _deathTimerFill = root.Q<VisualElement>("DeathTimerFill");

            // 이벤트 구독
            _wm.OnWaveChanged += HandleWaveChanged;
            _wm.OnLoopModeChanged += HandleLoopModeChanged;
            _wm.OnBossAutoChallengeChanged += HandleBossAutoChallengeChanged;
            _wm.OnDeathPopupShow += HandleDeathPopupShow;
            _wm.OnDeathPopupHide += HandleDeathPopupHide;
            _wm.OnDeathPopupTick += HandleDeathPopupTick;
            _wm.OnBossTimerTick += HandleBossTimerTick;

            // 버튼 클릭
            if (_btnLoopIcon != null)
                _btnLoopIcon.clicked += () => _wm.DisableLoopMode();

            if (_tglBossChain != null)
            {
                _tglBossChain.value = _wm.BossAutoChallenge;
                _tglBossChain.RegisterValueChangedCallback(evt =>
                    _wm.SetBossAutoChallenge(evt.newValue));
            }

            if (_btnDeathYes != null)
                _btnDeathYes.clicked += () => _wm.OnDeathPopupChoose(true);

            if (_btnDeathNo != null)
                _btnDeathNo.clicked += () => _wm.OnDeathPopupChoose(false);

            // 초기 상태
            SetHidden(_bossTimerBar, true);
            SetHidden(_deathPopup, true);
            SetHidden(_btnLoopIcon, true);

            UpdateStageLabel(_wm.CurrentStageNumber, _wm.CurrentWave, _wm.IsBossWave);
        }

        public static void Dispose()
        {
            if (_wm == null) return;
            _wm.OnWaveChanged -= HandleWaveChanged;
            _wm.OnLoopModeChanged -= HandleLoopModeChanged;
            _wm.OnBossAutoChallengeChanged -= HandleBossAutoChallengeChanged;
            _wm.OnDeathPopupShow -= HandleDeathPopupShow;
            _wm.OnDeathPopupHide -= HandleDeathPopupHide;
            _wm.OnDeathPopupTick -= HandleDeathPopupTick;
            _wm.OnBossTimerTick -= HandleBossTimerTick;
            _wm = null;
        }

        // ── 이벤트 핸들러 ──

        private static void HandleWaveChanged(int stageNum, int wave, bool isBoss)
        {
            UpdateStageLabel(stageNum, wave, isBoss);
            SetHidden(_bossTimerBar, !isBoss);
        }

        private static void HandleLoopModeChanged(bool isLoop)
        {
            SetHidden(_btnLoopIcon, !isLoop);
        }

        private static void HandleBossAutoChallengeChanged(bool enabled)
        {
            if (_tglBossChain != null)
                _tglBossChain.SetValueWithoutNotify(enabled);
        }

        private static void HandleDeathPopupShow()
        {
            SetHidden(_deathPopup, false);
            SetHidden(_bossTimerBar, true);
        }

        private static void HandleDeathPopupHide()
        {
            SetHidden(_deathPopup, true);
        }

        private static void HandleDeathPopupTick(float ratio)
        {
            if (_deathTimerFill != null)
                _deathTimerFill.style.width = Length.Percent(ratio * 100f);
        }

        private static void HandleBossTimerTick(float ratio)
        {
            if (_bossTimerFill != null)
                _bossTimerFill.style.width = Length.Percent(ratio * 100f);
        }

        // ── 유틸 ──

        private static void UpdateStageLabel(int stageNum, int wave, bool isBoss)
        {
            if (_lblStage == null) return;
            _lblStage.text = isBoss
                ? $"보스 {stageNum}"
                : $"스테이지 {stageNum}-{wave}";
        }

        private static void SetHidden(VisualElement el, bool hidden)
        {
            if (el == null) return;
            if (hidden)
                el.AddToClassList("hidden");
            else
                el.RemoveFromClassList("hidden");
        }
    }
}
