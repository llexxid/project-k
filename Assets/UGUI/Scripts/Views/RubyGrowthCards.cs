using KingdomIdle.Balance;
using UnityEngine;
namespace KingdomIdle.UGUI
{
    public sealed class RubyGrowthCards : MonoBehaviour
    {
        private readonly EnhanceCardView[] _cards = new EnhanceCardView[2];
        private readonly GachaPullButtonView[] _buy = new GachaPullButtonView[2], _reset = new GachaPullButtonView[2];
        private readonly (int level, long spent, long ruby, bool unlocked, bool capped, bool reset, int notation, int revision, int queued, string pending)[] _shown = new (int,long,long,bool,bool,bool,int,int,int,string)[2];
        private readonly bool[] _hasShown = new bool[2];
        private void OnEnable() { LocalProgression.Changed += Refresh; NumberNotation.Changed += Refresh; RubyProgression.Changed += Refresh; RubyProgression.Completed += OnCompleted; Refresh(); }
        private void OnDisable() { LocalProgression.Changed -= Refresh; NumberNotation.Changed -= Refresh; RubyProgression.Changed -= Refresh; RubyProgression.Completed -= OnCompleted; }
        private void OnCompleted(bool success, string message) => UIManager.Instance?.ShowToast(message);
        public void Build(Transform parent)
        {
            var catalog = UIManager.Instance.Catalog;
            for (int i=0;i<2;i++)
            {
                int index=i;
                _cards[i] = Instantiate(catalog.itemEnhanceCard,parent,false).GetComponent<EnhanceCardView>();
                _buy[i] = Instantiate(catalog.itemGachaPullButton,_cards[i].ButtonRow,false).GetComponent<GachaPullButtonView>();
                _reset[i] = Instantiate(catalog.itemGachaPullButton,_cards[i].ButtonRow,false).GetComponent<GachaPullButtonView>();
                _buy[i].Button.onClick.AddListener(()=> {
                    bool ok = RubyProgression.HasPendingRequest ? RubyProgression.RetryPending() : RubyProgression.Enhance(index==1);
                    if (!ok) UIManager.Instance.ShowToast("루비 조건과 전투 보상 정산 상태를 확인해 주세요.");
                });
                _reset[i].Button.onClick.AddListener(()=> {
                    bool pending = RubyProgression.HasPendingRequest;
                    bool ok = pending ? RubyProgression.RefreshPending() : RubyProgression.Reset(index==1);
                    if (!ok) UIManager.Instance.ShowToast("서버 연결과 초기화 조건을 확인해 주세요.");
                    else if (!pending && RubyProgression.WaitingForWave) UIManager.Instance.ShowToast("일반 웨이브 종료 후 초기화 · 실제 사용 루비 80% 반환");
                });
            }
            Refresh();
        }
        private void Refresh()
        {
            var state=LocalProgression.State;
            for(int i=0;i<2;i++)
            {
                if(_cards[i]==null) continue;
                int level=i==1?state.RubyExpLevel:state.RubyGoldLevel;
                long spent=i==1?state.RubyExpSpent:state.RubyGoldSpent;
                long? cost=BalanceMath.RubyCost(level);
                bool unlocked=RubyProgression.Unlocked;
                long ruby=LocalProgression.Balance(eCurrency.Ruby);
                bool capped=i==1 && state.AccountLevel>=200;
                bool reset=level>0 && state.PendingRubyReset==0 && QuestPeriod.IsLaterDay(LocalProgression.KstDay,state.RubyResetDay) && ChangeJob.CanQueueChange;
                var signature=(level,spent,ruby,unlocked,capped,reset,NumberNotation.Revision,RubyProgression.Revision,state.PendingRubyReset,state.PendingRubyRequestId);
                if(_hasShown[i] && _shown[i].Equals(signature))continue;
                _shown[i]=signature;_hasShown[i]=true;
                bool available = RubyProgression.Available, busy = RubyProgression.IsBusy;
                bool pending = RubyProgression.HasPendingRequest, queued = RubyProgression.WaitingForWave;
                string detail = !available ? "서버 연동 준비 중" : busy ? "서버에 확인 중입니다." :
                    pending ? "확인되지 않은 루비 요청이 있습니다." : queued ? "일반 웨이브 종료 후 초기화" :
                    unlocked ? $"사냥·방치 ×{BalanceMath.RubyMultiplier(level):0.00}" : "메인 2-5 클리어 후 해금";
                _cards[i].Set(i==1?"EXP 획득 강화":"골드 획득 강화",$"Lv. {level}/50",detail);
                if (pending && state.PendingRubyRequestExperience == (i==1))
                {
                    _buy[i].Set("같은 요청 재시도",state.PendingRubyRequestIsReset ? "초기화 확인" : "강화 확인",available && !busy);
                    _reset[i].Set("서버 상태 확인","결과 조회",available && !busy);
                }
                else
                {
                    bool ready = available && !busy && !pending && !queued;
                    string upgradeCost = cost.HasValue ? $"루비 · 보유 {NumberNotation.Format(ruby)} · 필요 {NumberNotation.Format(cost.Value)}" : "MAX";
                    _buy[i].Set("1회 강화",upgradeCost,ready && unlocked && cost.HasValue && ruby>=cost.Value && !capped);
                    _reset[i].Set("초기화 80% 반환",$"{NumberNotation.Format(BalanceMath.Floor(spent*.8m))} 루비",ready && reset);
                }
            }
        }
    }
}
