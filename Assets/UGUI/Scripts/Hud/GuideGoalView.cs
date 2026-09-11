using UnityEngine;
using UnityEngine.UI;
using TMPro;
using KingdomIdle.UI;
namespace KingdomIdle.UGUI
{
    public sealed class GuideGoalView : MonoBehaviour
    {
        public TMP_Text description, progress, actionLabel;
        public Button actionButton;
        public GameObject body;
        private QuestManager _manager;
        private QuestRuntimeState _state;
        private QuestDefinition _definition;
        private void OnEnable() { if(actionButton!=null)actionButton.onClick.AddListener(Act); if(UIManager.Instance!=null)UIManager.Instance.PanelStackChanged+=Visibility; Connect(); }
        private void Start() => Connect();
        private void OnDisable()
        {
            if(actionLabel!=null)UITween.StopBreathScale(actionLabel.rectTransform);
            if(actionButton!=null)actionButton.onClick.RemoveListener(Act);
            if(UIManager.Instance!=null)UIManager.Instance.PanelStackChanged-=Visibility;
            if(_manager!=null){_manager.OnGuideQuestChanged-=Refresh;_manager.OnQuestProgressChanged-=Refresh;}
            _manager=null;
        }
        private void Connect()
        {
            if(_manager==null && QuestManager.Instance!=null)
            {_manager=QuestManager.Instance;_manager.OnGuideQuestChanged+=Refresh;_manager.OnQuestProgressChanged+=Refresh;}
            var state=_manager!=null?_manager.GetActiveGuideState():null;
            Refresh(state,state!=null?_manager.GetQuestDefinition(state.QuestId):null);
        }
        private void Refresh(QuestRuntimeState state, QuestDefinition definition)
        {
            if(definition!=null && definition.Category!=eQuestCategory.Guide)return;
            _state=state;_definition=definition;
            bool valid=state!=null && definition!=null;
            Visibility();
            if(!valid)return;
            description.text=definition.Description;
            progress.text=state.IsCompleted?"목표 달성":$"{Mathf.Min(state.CurrentProgress,definition.RequiredCount):N0} / {definition.RequiredCount:N0}";
            actionLabel.text=state.IsCompleted?(definition.RewardGroupId==0?"다음 목표":"보상 받기"):(Destination().HasValue?"이동":"진행 중");
            actionButton.interactable=state.IsCompleted?_manager.CanClaimReward(state.QuestId):Destination().HasValue;
            AnimateAction();
        }
        private void Visibility()
        {
            var ui=UIManager.Instance;
            if(body!=null)body.SetActive(_state!=null&&_definition!=null&&(ui==null||(!ui.HasActiveTabPanel&&!ui.HasBlockingPanel)));
            AnimateAction();
        }
        private void AnimateAction()
        {
            if(actionLabel==null)return;
            UITween.StopBreathScale(actionLabel.rectTransform);
            if(body!=null&&body.activeInHierarchy&&_state!=null&&_state.IsCompleted&&actionButton.interactable)
                UITween.BreathScale(actionLabel.rectTransform,.018f,2.8f);
        }
        private UIPanelId? Destination()
        {
            if(_definition==null)return null;
            switch(_definition.ObjectiveType)
            {
                case eQuestObjectiveType.GachaUse:return UIPanelId.Gacha;
                case eQuestObjectiveType.DungeonEnter:case eQuestObjectiveType.DungeonClear:return UIPanelId.Dungeon;
                case eQuestObjectiveType.EquipmentEquip:case eQuestObjectiveType.JobChange:case eQuestObjectiveType.Enhance:return UIPanelId.KingdomArmy;
                default:return null;
            }
        }
        private void Act()
        {
            if(_state==null)return;
            if(_state.IsCompleted){_manager.ClaimQuestReward(_state.QuestId);Connect();}
            else if(Destination() is UIPanelId panel)UIManager.Instance?.PushPanel(panel,null,true,true);
        }
    }
}
