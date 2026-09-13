using System.Collections.Generic;
using System.Linq;
using KingdomIdle.Balance;
using UnityEngine;
namespace KingdomIdle.UGUI
{
    public sealed class BalanceQuestPanel : MonoBehaviour
    {
        private GuidePanelView _view;
        private sealed class Row { public QuestDefinition Quest; public GuideStepRowView View; public string Pending; public UnityEngine.UI.Button ClaimArea; }
        private readonly List<Row> _rows = new();
        private long _revision=-1;
        private int _claims=-1;
        private float _next;
        public void Bind(GuidePanelView view) { _view=view; Build(); }
        private void Update()
        {
            if(_view==null || Time.unscaledTime<_next) return;
            _next=Time.unscaledTime+.5f;
            var state=LocalProgression.State;
            if(state.QuestDay!=LocalProgression.KstDay || state.QuestWeek!=QuestEconomy.Week) LocalProgression.Execute("quest-period",s=>true);
            if(_claims!=state.Claims.Count) { Build(); return; }
            if(_revision!=state.Revision) Refresh();
        }
        private void Build()
        {
            foreach(Transform child in _view.listContent) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            _rows.Clear();
            var state=LocalProgression.State;
            var all=QuestEconomy.Definitions;
            var guide=all.FirstOrDefault(q=>q.Category==eQuestCategory.Guide && !state.Claims.Contains(QuestEconomy.Key(q,state)));
            if(guide!=null) Add(guide,null);
            foreach(var q in all.Where(q=>q.IsRepeatable && !state.Claims.Contains(QuestEconomy.Key(q,state)))) Add(q,null);
            // One next milestone per achievement family; completed predecessors remain in the receipt ledger.
            foreach(var group in all.Where(q=>q.Category==eQuestCategory.Achievement).GroupBy(q=>q.QuestId/100))
            { var q=group.FirstOrDefault(q=>!state.Claims.Contains(QuestEconomy.Key(q,state)));if(q!=null) Add(q,null); }
            foreach(var pending in state.PendingQuests.Where(x=>x.Value.ExpiresUtc>=LocalProgression.UtcNow))
            { var q=all.FirstOrDefault(q=>q.QuestId==pending.Value.Id);if(q!=null && QuestEconomy.Key(q,state)!=pending.Key) Add(q,pending.Key); }
            _claims=state.Claims.Count;
            if(_view.progressFill!=null) _view.progressFill.transform.parent.gameObject.SetActive(false);
            if(_view.progressLabel!=null) _view.progressLabel.text="가이드 · 일일 · 주간 · 업적";
            Refresh();
        }
        private void Add(QuestDefinition q,string pending)
        {
            var catalog=UIManager.Instance.Catalog;
            var row=Instantiate(catalog.itemGuideStepRow,_view.listContent,false).GetComponent<GuideStepRowView>();
            var binding=new Row { Quest=q,View=row,Pending=pending };_rows.Add(binding);
            row.checkButton.gameObject.SetActive(true);
            var touch=row.checkButton.GetComponent<UnityEngine.UI.LayoutElement>() ?? row.checkButton.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
            touch.minWidth=84;touch.preferredWidth=84;touch.minHeight=84;touch.preferredHeight=84;
            if(row.checkLabel!=null) row.checkLabel.fontSize=23;
            row.checkButton.onClick.AddListener(()=> { if(QuestEconomy.Claim(q.QuestId,pending)) Build(); });
            var area=row.GetComponent<UnityEngine.UI.Button>() ?? row.gameObject.AddComponent<UnityEngine.UI.Button>();
            area.targetGraphic=row.GetComponent<UnityEngine.UI.Image>();area.transition=UnityEngine.UI.Selectable.Transition.None;
            area.onClick.AddListener(()=> { if(QuestEconomy.Claim(q.QuestId,pending)) Build(); });binding.ClaimArea=area;
            if(row.hintLabel!=null) row.hintLabel.fontSize=23;
        }
        private void Refresh()
        {
            var state=LocalProgression.State;_revision=state.Revision;
            foreach(var row in _rows)
            {
                var q=row.Quest;
                long progress=System.Math.Min(q.RequiredCount,QuestEconomy.Progress(q,state));
                bool can=row.Pending!=null || QuestEconomy.CanClaim(q,state);
                string type=q.Category==eQuestCategory.Guide?"가이드":q.Category==eQuestCategory.Daily?"일일":q.Category==eQuestCategory.Weekly?"주간":"업적";
                row.View.Set($"{type} · {(can?"보상 수령":$"{progress:N0}/{q.RequiredCount:N0}")}",q.Description,QuestEconomy.RewardText(q.RewardGroupId)+(row.Pending!=null?" · 이전 기간 보관분":""),false);
                row.View.checkButton.interactable=can;row.ClaimArea.interactable=can;
                if(row.View.checkLabel!=null) row.View.checkLabel.text=can?"받기":"";
                if(row.View.checkIcon!=null) row.View.checkIcon.gameObject.SetActive(false);
            }
        }
    }
}
