using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using KingdomIdle.UI;

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
        private Coroutine _connection;
        private bool _breathing;

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
            _manager.OnQuestProgressChanged += Refresh;
            _ui.PanelStackChanged += Visibility;
            ReadCurrent();
            _connection = null;
        }

        private void OnDisable()
        {
            if (_connection != null) StopCoroutine(_connection);
            _connection = null;
            if (actionButton != null) actionButton.onClick.RemoveListener(Act);
            if (_ui != null) _ui.PanelStackChanged -= Visibility;
            if (_manager != null)
            {
                _manager.OnGuideQuestChanged -= Refresh;
                _manager.OnQuestProgressChanged -= Refresh;
            }
            StopBreathing();
            _manager = null;
            _ui = null;
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
            bool valid = state != null && definition != null;
            if (valid)
            {
                Set(description, definition.Description);
                Set(stepLabel, $"가이드 {definition.QuestId-10000:N0}");
                int required = Mathf.Max(1, definition.RequiredCount);
                Set(progress, $"{Mathf.Clamp(state.CurrentProgress, 0, required):N0}/{required:N0}");
                if (progressFill != null) progressFill.fillAmount = Mathf.Clamp01((float)state.CurrentProgress / required);
                bool claimable = state.IsCompleted && _manager.CanClaimReward(state.QuestId);
                bool canNavigate = Destination().HasValue;
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
                _manager != null && _manager.CanClaimReward(_state.QuestId);
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

        private UIPanelId? Destination()
        {
            if (_definition == null) return null;
            switch (_definition.ObjectiveType)
            {
                case eQuestObjectiveType.GachaUse: return UIPanelId.Gacha;
                case eQuestObjectiveType.DungeonEnter:
                case eQuestObjectiveType.DungeonClear: return UIPanelId.Dungeon;
                case eQuestObjectiveType.LevelUp:
                case eQuestObjectiveType.StatEnhance: return UIPanelId.Development;
                case eQuestObjectiveType.EquipmentObtain: return UIPanelId.Inventory;
                case eQuestObjectiveType.EquipmentEquip:
                case eQuestObjectiveType.JobChange:
                case eQuestObjectiveType.SkillEquip:
                case eQuestObjectiveType.Enhance: return UIPanelId.KingdomArmy;
                default: return null;
            }
        }

        private void Act()
        {
            if (_state == null || _manager == null) return;
            if (_state.IsCompleted && _manager.CanClaimReward(_state.QuestId))
            {
                _manager.ClaimQuestReward(_state.QuestId);
                ReadCurrent();
            }
            else if (!_state.IsCompleted && Destination() is UIPanelId panel)
                UIManager.Instance?.PushPanel(panel, null, true, true);
            else if (compact)
                UIManager.Instance?.PushPanel(UIPanelId.Guide, null, false, false);
        }
    }
}
