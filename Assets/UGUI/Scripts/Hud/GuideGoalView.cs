using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using KingdomIdle.UI;
using KingdomIdle.Balance;

namespace KingdomIdle.UGUI
{
    // Both HUD and menu bind the same quest state; only their presentation differs.
    public sealed class GuideGoalView : MonoBehaviour
    {
        public TMP_Text description, progress, actionLabel, stepLabel;
        public Image progressFill;
        public Button actionButton;
        public GameObject body;
        public bool hideWithPanels = true;
        public bool compact;
        private QuestManager _manager;
        private UIManager _ui;
        private QuestRuntimeState _state;
        private QuestDefinition _definition;
        // 표시한 계정·기간의 권리를 보관한다. 클릭할 때 현재 계정으로 토큰을 다시 만들지 않는다.
        private QuestClaimToken _claimToken;
        private bool _hasClaimToken, _claimable, _claimRequested;
        private Coroutine _connection;
        private bool _breathing;
        /// <summary>전투 HUD 인스턴스만 던전에서 접는다. 재활성화 시 기존 구독으로 최신 목표를 복원한다.</summary>
        internal void SetStageVisible(bool visible)
        {
            if (gameObject.activeSelf != visible) gameObject.SetActive(visible);
        }

        private void OnEnable()
        {
            if (actionButton != null) actionButton.onClick.AddListener(Act);
            _connection = StartCoroutine(ConnectWhenReady());
        }

        private IEnumerator ConnectWhenReady()
        {
            var tick = new WaitForSecondsRealtime(.25f);
            while (QuestManager.Instance == null || UIManager.Instance == null) yield return tick;
            _manager = QuestManager.Instance;
            _ui = UIManager.Instance;
            _manager.OnGuideQuestChanged += Refresh;
            NumberNotation.Changed += ReadCurrent;
            _manager.OnQuestProgressChanged += Refresh;
            _manager.OnClaimCompleted += OnClaimCompleted;
            _manager.AddWatcher(false);
            _ui.PanelStackChanged += Visibility;
            ReadCurrent();
            _connection = null;
        }

        private void OnDisable()
        {
            NumberNotation.Changed -= ReadCurrent;
            if (_connection != null) StopCoroutine(_connection);
            _connection = null;
            if (actionButton != null) actionButton.onClick.RemoveListener(Act);
            if (_ui != null) _ui.PanelStackChanged -= Visibility;
            if (_manager != null)
            {
                _manager.OnGuideQuestChanged -= Refresh;
                _manager.OnQuestProgressChanged -= Refresh;
                _manager.OnClaimCompleted -= OnClaimCompleted;
                _manager.RemoveWatcher(false);
            }
            _claimRequested = false;
            StopBreathing();
            _manager = null;
            _ui = null;
            _hasClaimToken = false;
            _claimable = false;
        }

        private void ReadCurrent()
        {
            var state = _manager.GetActiveGuideState();
            Refresh(state, state != null ? _manager.GetQuestDefinition(state.QuestId) : null);
        }

        private void Refresh(QuestRuntimeState state, QuestDefinition definition)
        {
            if (definition != null && definition.Category != eQuestCategory.Guide) return;
            _state = state;
            _definition = definition;
            _hasClaimToken = false;
            _claimable = false;
            bool valid = state != null && definition != null;
            if (valid)
            {
                // 구형 가이드 표시 API는 유지하되, 수령 입력은 같은 화면 바인딩 시점의 불변 token을 사용한다.
                if (_manager != null)
                    foreach (var row in _manager.GetSnapshot(eQuestCategory.Guide).Rows)
                        if (row.Token.QuestId == state.QuestId && !row.IsPending)
                        {
                            _claimToken = row.Token;
                            _hasClaimToken = true;
                            _claimable = row.CanClaim;
                            break;
                        }
                Set(description, definition.Description);
                Set(stepLabel, $"가이드 {definition.QuestId-10000:N0}");
                int required = Mathf.Max(1, definition.RequiredCount);
                Set(progress, $"{NumberNotation.Format(Mathf.Clamp(state.CurrentProgress, 0, required))}/{NumberNotation.Format(required)}");
                if (progressFill != null) progressFill.fillAmount = Mathf.Clamp01((float)state.CurrentProgress / required);
                bool claimable = state.IsCompleted && _claimable;
                bool canNavigate = QuestNavigation.CanNavigate(definition.ObjectiveType);
                Set(actionLabel, claimable ? (definition.RewardGroupId == 0 ? "다음 목표  ›" : "보상 받기  ›")
                    : state.IsCompleted ? "보상 대기" : canNavigate ? "이동  ›" : compact ? "자세히  ›" : "전투에서 진행");
                if (actionButton != null) actionButton.interactable = compact || claimable || (!state.IsCompleted && canNavigate);
            }
            else if (!hideWithPanels)
            {
                Set(stepLabel, "가이드");
                Set(description, "진행 중인 가이드가 없습니다.");
                Set(progress, "");
                Set(actionLabel, "");
                if (progressFill != null) progressFill.fillAmount = 0;
                if (actionButton != null) actionButton.interactable = false;
            }
            Visibility();
        }

        /// <summary>서버 수령 결과. 이 HUD가 요청한 수령만 안내한다(패널에서 요청한 수령은 패널이 안내한다).</summary>
        private void OnClaimCompleted(QuestClaimToken token, bool ok, string message)
        {
            if (!_claimRequested) return;
            _claimRequested = false;
            if (ok) UIManager.Instance?.ShowToast("가이드 완료");
            else UIManager.Instance?.ShowToast(string.IsNullOrEmpty(message) ? "보상을 받지 못했습니다. 잠시 후 다시 시도해 주세요." : message);
            if (_manager != null) ReadCurrent();
        }

        private static void Set(TMP_Text label, string text)
        {
            if (label != null && label.text != text) label.text = text;
        }

        private void Visibility()
        {
            bool show = !hideWithPanels || (_state != null && _definition != null &&
                (_ui == null || (!_ui.HasActiveTabPanel && !_ui.HasBlockingPanel)));
            if (body != null && body.activeSelf != show) body.SetActive(show);
            bool breathe = body != null && body.activeInHierarchy && _state != null && _state.IsCompleted &&
                _manager != null && _claimable;
            if (breathe == _breathing) return;
            StopBreathing();
            if (breathe && actionLabel != null)
            {
                UITween.BreathScale(actionLabel.rectTransform, .018f, 2.8f);
                _breathing = true;
            }
        }

        private void StopBreathing()
        {
            if (actionLabel != null) UITween.StopBreathScale(actionLabel.rectTransform);
            _breathing = false;
        }

        private void Act()
        {
            if (_state == null || _manager == null) return;
            if (_state.IsCompleted && _hasClaimToken && _claimable)
            {
                // 서버(OnClaimQuestReward)에 요청한다. 결과는 OnClaimCompleted에서 알리고, 응답의 Quests로 다음 가이드가 바뀐다.
                var result = _manager.TryClaim(_claimToken);
                if (result.Status == QuestClaimStatus.Pending)
                {
                    _claimRequested = true;
                    if (actionButton != null) actionButton.interactable = false;
                    return;
                }
                if (result.Status == QuestClaimStatus.Busy) UIManager.Instance?.ShowToast("이전 보상을 처리 중입니다. 잠시 후 다시 시도해 주세요.");
                else if (result.Status == QuestClaimStatus.NetworkNotReady) UIManager.Instance?.ShowToast("서버 로그인이 필요합니다.");
                ReadCurrent();
            }
            else if (!_state.IsCompleted && _definition != null && QuestNavigation.CanNavigate(_definition.ObjectiveType))
                QuestNavigation.Navigate(_definition.ObjectiveType, _definition.TargetId);
            else if (compact)
                UIManager.Instance?.PushPanel(UIPanelId.Guide, null, false, false);
        }
    }
}
