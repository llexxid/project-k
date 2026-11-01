using UnityEngine;
using Scripts.Core;
using Scripts.Core.Manager;
using TMPro;
using Scripts.Monster;
using KingdomIdle.Balance;

namespace KingdomIdle.UGUI
{
    /// <summary>
    /// 메인 화면 웨이브/스테이지 HUD 컨트롤러 (UGUI).
    /// 기존 스테이지 이벤트와 읽기 전용 실행 스냅샷으로 메인·던전 표시를 갱신한다.
    /// </summary>
    public static class WaveUIController
    {
        private static WaveHudView _view;
        private static StageManager _sm;
        private static Transform _deathHome;
        private static GameObject _deathPopup;
        private static Transform _returnHome;
        private static string _returnRun;
        private static Monster _boss;
        private static int _displayedSecond = int.MinValue;

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
            _sm.OnStageEnter += HandleStageEnter;
            _sm.OnMonsterKilled += HandleMonsterKilled;
            _sm.OnStageCleared += HandleStageCleared;
            LocalProgression.AccountChanged += HandleAccountChanged;
            if (_view.btnStageSelect != null) _view.btnStageSelect.onClick.AddListener(OpenStageSelect);
            if (_view.btnDungeonReturn != null) _view.btnDungeonReturn.onClick.AddListener(OpenReturn);
            if (_view.returnPopup != null)
            {
                _returnHome = _view.returnPopup.transform.parent;
                _view.returnConfirm.onClick.AddListener(ConfirmReturn);
                _view.returnCancel.onClick.AddListener(CloseReturn);
                _view.returnBackdrop.onClick.AddListener(CloseReturn);
                ModalBackHandler.Bind(_view.returnPopup, CloseReturn);
            }

            // 버튼/토글 바인딩
            if (_view.btnLoopIcon != null)
                _view.btnLoopIcon.onClick.AddListener(OnLoopIconClicked);
            if (_view.btnStageAction != null)
                _view.btnStageAction.onClick.AddListener(OnStageActionClicked);

            if (_view.tglBossChain != null)
            {
                SyncBossAutoChallenge(_sm.BossAutoChallenge);
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
            BindBoss(null);
            StageSelectPopupController.Dispose();
            LocalProgression.AccountChanged -= HandleAccountChanged;
            if (_view != null)
            {
                if (_view.btnStageSelect != null) _view.btnStageSelect.onClick.RemoveListener(OpenStageSelect);
                if (_view.btnDungeonReturn != null) _view.btnDungeonReturn.onClick.RemoveListener(OpenReturn);
                if (_view.returnPopup != null)
                {
                    CloseReturn();
                    _view.returnConfirm.onClick.RemoveListener(ConfirmReturn);
                    _view.returnCancel.onClick.RemoveListener(CloseReturn);
                    _view.returnBackdrop.onClick.RemoveListener(CloseReturn);
                    if (_returnHome != null) _view.returnPopup.transform.SetParent(_returnHome, false);
                    else Object.Destroy(_view.returnPopup);
                }
            }
            _returnHome = null;
            if (_view != null)
            {
                if (_view.btnLoopIcon != null) _view.btnLoopIcon.onClick.RemoveListener(OnLoopIconClicked);
                if (_view.btnStageAction != null) _view.btnStageAction.onClick.RemoveListener(OnStageActionClicked);
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
                _sm.OnStageEnter -= HandleStageEnter;
                _sm.OnMonsterKilled -= HandleMonsterKilled;
                _sm.OnStageCleared -= HandleStageCleared;
                _sm = null;
            }

            _view = null;
            _displayedSecond = int.MinValue;
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

        private static void OnStageActionClicked()
        {
            if (_sm == null || !_sm.TryMoveToMainStage(_sm.CycleFrontierStage))
                UIManager.Instance?.ShowToast("전투 전환이 끝난 후 다시 시도해 주세요.");
        }

        private static void OpenStageSelect() => StageSelectPopupController.Show();
        private static void HandleStageEnter(StageDefinition definition) { CloseReturn(); RefreshHud(); StageSelectPopupController.Refresh(); }
        private static void HandleStageCleared(StageDefinition definition) { RefreshHud(); StageSelectPopupController.Refresh(); }
        private static void HandleMonsterKilled(StageDefinition definition, Monster monster) => RefreshHud();
        private static void HandleAccountChanged() { CloseReturn(); StageSelectPopupController.Hide(); RefreshHud(); }
        private static void OpenReturn()
        {
            if (_view?.returnPopup == null || _sm == null || !_sm.TryGetHudSnapshot(out var state) ||
                state.Definition.Type == eStageType.Main || _sm.CurrentRunState != eStageRunState.Running) return;
            _returnRun = state.RunId;
            _view.returnPopup.transform.SetParent(UIManager.Instance.LayerPopups, false);
            _view.returnPopup.transform.SetAsLastSibling();
            _view.returnPopup.SetActive(true);
            UITween.PopIn(_view.returnPanel);
        }
        private static void CloseReturn() { if (_view?.returnPopup != null) _view.returnPopup.SetActive(false); _returnRun = null; }
        private static void ConfirmReturn()
        {
            bool valid = _sm != null && _sm.TryGetHudSnapshot(out var state) && state.RunId == _returnRun &&
                state.Definition.Type != eStageType.Main && _sm.CurrentRunState == eStageRunState.Running;
            CloseReturn();
            if (valid) _sm.ReturnToMainStage();
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
        }

        private static void HandleLoopModeChanged(bool isLoop)
        {
            if (_view != null && _view.btnLoopIcon != null)
                _view.btnLoopIcon.gameObject.SetActive(isLoop);
            if (_sm != null) UpdateStageLabel(_sm.CurrentStageNumber, _sm.CurrentWaveNumber, _sm.IsBossWave);
        }

        private static void HandleBossAutoChallengeChanged(bool enabled)
        {
            SyncBossAutoChallenge(enabled);
            if (_sm != null) UpdateStageLabel(_sm.CurrentStageNumber, _sm.CurrentWaveNumber, _sm.IsBossWave);
        }

        private static void SyncBossAutoChallenge(bool enabled)
        {
            if (_view == null || _view.tglBossChain == null) return;
            if (_view.tglBossChain.isOn != enabled)
                _view.tglBossChain.SetIsOnWithoutNotify(enabled);
            _view.tglBossChain.GetComponent<ToggleSwitchView>()?.Refresh();
        }

        private static void HandleDefeatPopupShow()
        {
            if (_view == null) return;
            CloseReturn(); StageSelectPopupController.Hide();
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
            if (_sm?.CurrentDefinition == null) return;
            bool needsBoss = _sm.IsBossWave || _sm.CurrentDefinition.Type == eStageType.RubyDungeon;
            if (_displayedSecond == Mathf.CeilToInt(ratio * _sm.CurrentDefinition.TimeLimitSec) && (!needsBoss || _boss != null)) return;
            RefreshHud();
        }

        // ── 유틸 ──

        private static void UpdateStageLabel(int stageNum, int wave, bool isBoss)
        {
            RefreshHud();
        }

        private static void RefreshHud()
        {
            if (_view == null || _sm == null || !_sm.TryGetHudSnapshot(out var state)) return;
            var definition = state.Definition;
            _displayedSecond = Mathf.CeilToInt(state.RemainingTime);
            bool dungeon = definition.Type != eStageType.Main;
            bool cleared = !dungeon && _sm.IsCycleStageCleared(definition.Id);
            bool boss = !dungeon && StageParser.IsBossWave(definition.Id);
            SetHidden(_view.mainHud, dungeon);
            SetHidden(_view.mainProgress, dungeon || cleared);
            SetHidden(_view.chapterProgress, dungeon || cleared);
            SetHidden(_view.bossChallengeRoot, dungeon || cleared);
            SetHidden(_view.bossTimerBar, true);
            SetHidden(_view.dungeonObjective, !dungeon);
            SetHidden(_view.dungeonReturn, !dungeon);
            SetHidden(_view.btnStageAction?.gameObject, dungeon || !cleared);
            if (_view.guide != null) _view.guide.SetStageVisible(!dungeon);
            if (_view.btnStageSelect != null) _view.btnStageSelect.interactable = !dungeon;
            if (_view.btnStageAction != null) _view.btnStageAction.interactable = _sm.CurrentRunState == eStageRunState.Running;
            Set(_view.lblStage, $"스테이지 {definition.StageNumber}-{definition.WaveNumber}  ›");
            Set(_view.stageActionLabel, "최고 스테이지 이동");
            if (!dungeon)
            {
                Set(_view.progressLabel, boss ? "보스 HP" : $"처치 {state.Kills} / {state.Target}");
                Set(_view.remainingLabel, boss ? $"{Mathf.CeilToInt(state.RemainingTime)}초" : $"다음 스테이지까지 {Mathf.Max(0, state.Target-state.Kills)}마리");
                Fill(_view.progressFill, boss ? state.Boss != null ? state.Boss.GetHpRatio() : 0 : (float)state.Kills/state.Target);
                if (_view.chapterSteps != null)
                    for (int i=0;i<_view.chapterSteps.Length;i++)
                    {
                        Color color = i < definition.WaveNumber-1 ? UguiTheme.BronzeLight : i == definition.WaveNumber-1 ? UguiTheme.Parchment : UguiTheme.RusticSurface;
                        if (_view.chapterSteps[i].color != color) _view.chapterSteps[i].color = color;
                    }
            }
            else
            {
                bool gold = definition.Type == eStageType.GoldDungeon;
                Set(_view.dungeonName, $"{(gold?"골드":"루비")} 던전 · {definition.StageNumber}단계");
                Set(_view.dungeonTitle, gold ? $"미믹 처치 {state.Kills} / {state.Target}" : $"보스 {Mathf.Min(state.Kills+1,state.Target)} / {state.Target}");
                Set(_view.dungeonTime, $"{Mathf.Max(0,Mathf.CeilToInt(state.RemainingTime))}초");
                Set(_view.dungeonProgress, gold ? "" : $"현재 보스 HP  {Mathf.RoundToInt((state.Boss != null ? state.Boss.GetHpRatio() : 0)*100)}%");
                SetHidden(_view.dungeonProgress?.gameObject, gold);
                Set(_view.dungeonReward, gold ? $"획득 골드 +{NumberNotation.Format(state.EarnedGold)}" : $"처치 완료 {state.Kills} / {state.Target}");
                Fill(_view.dungeonFill, gold ? (float)state.Kills/state.Target : state.Boss != null ? state.Boss.GetHpRatio() : 0);
                if (_view.btnDungeonReturn != null) _view.btnDungeonReturn.interactable = _sm.CurrentRunState == eStageRunState.Running;
            }
            BindBoss(state.Boss);
            if (_returnRun != null && (_returnRun != state.RunId || _sm.CurrentRunState != eStageRunState.Running)) CloseReturn();
        }
        private static void BindBoss(Monster boss)
        {
            if (_boss == boss) return;
            if (_boss != null) _boss.OnHpChanged -= HandleBossHp;
            _boss = boss;
            if (_boss != null) _boss.OnHpChanged += HandleBossHp;
        }
        private static void HandleBossHp(float ratio) => RefreshHud();
        private static void Set(TMP_Text label, string value) { if (label != null && label.text != value) label.text = value; }
        private static void Fill(UnityEngine.UI.Image image, float value) { if (image != null && image.fillAmount != Mathf.Clamp01(value)) image.fillAmount = Mathf.Clamp01(value); }

        private static void SetHidden(GameObject go, bool hidden)
        {
            if (go == null) return;
            if (go.activeSelf == hidden) go.SetActive(!hidden);
        }
    }
}
