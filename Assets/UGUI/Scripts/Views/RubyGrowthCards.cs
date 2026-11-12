using KingdomIdle.Balance;
using UnityEngine;
namespace KingdomIdle.UGUI
{
    public sealed class RubyGrowthCards : MonoBehaviour
    {
        private readonly EnhanceCardView[] _cards = new EnhanceCardView[2];
        private readonly GachaPullButtonView[] _buy = new GachaPullButtonView[2];
        private readonly (int level, long ruby, bool unlocked, bool capped, int notation, int revision, int queued, string pending)[] _shown = new (int,long,bool,bool,int,int,int,string)[2];
        private readonly bool[] _hasShown = new bool[2];
        private void OnEnable() { LocalProgression.Changed += Refresh; NumberNotation.Changed += Refresh; RubyProgression.Changed += Refresh; RubyProgression.Completed += OnCompleted; Refresh(); }
        private void OnDisable() { LocalProgression.Changed -= Refresh; NumberNotation.Changed -= Refresh; RubyProgression.Changed -= Refresh; RubyProgression.Completed -= OnCompleted; }
        private void OnCompleted(bool success, string message) => UIManager.Instance?.ShowToast(message);
        public void Build(Transform parent)
        {
            if (_cards[0] != null) return;
            var catalog = UIManager.Instance != null ? UIManager.Instance.Catalog : null;
            if (catalog == null || catalog.itemEnhanceCard == null || catalog.itemGachaPullButton == null) return;
            for (int i=0;i<2;i++)
            {
                int index=i;
                _cards[i] = Instantiate(catalog.itemEnhanceCard,parent,false).GetComponent<EnhanceCardView>();
                _buy[i] = Instantiate(catalog.itemGachaPullButton,_cards[i].ButtonRow,false).GetComponent<GachaPullButtonView>();
                _buy[i].Button.onClick.AddListener(()=> {
                    // 과거 미확정 요청은 지우지 않는다. 초기화 요청은 상태 조회만 하며 새 초기화를 시작하지 않는다.
                    bool ok = RubyProgression.HasPendingRequest
                        ? LocalProgression.State.PendingRubyRequestIsReset ? RubyProgression.RefreshPending() : RubyProgression.RetryPending()
                        : RubyProgression.Enhance(index==1);
                    if (!ok) UIManager.Instance.ShowToast("루비 조건과 전투 보상 정산 상태를 확인해 주세요.");
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
                long? cost=BalanceMath.RubyCost(level);
                bool unlocked=RubyProgression.Unlocked;
                long ruby=LocalProgression.Balance(eCurrency.Ruby);
                bool capped=i==1 && state.AccountLevel>=200;
                var signature=(level,ruby,unlocked,capped,NumberNotation.Revision,RubyProgression.Revision,state.PendingRubyReset,state.PendingRubyRequestId);
                if(_hasShown[i] && _shown[i].Equals(signature))continue;
                _shown[i]=signature;_hasShown[i]=true;
                bool available = RubyProgression.Available, busy = RubyProgression.IsBusy;
                bool pending = RubyProgression.HasPendingRequest, queued = RubyProgression.WaitingForWave;
                string detail = !available ? "서버 연동 준비 중" : busy ? "서버에 확인 중입니다." :
                    pending ? "이전 요청의 결과 확인이 필요합니다." : queued ? "이전 성장 요청 처리 대기" :
                    !unlocked ? "메인 2-5 클리어 후 해금" : cost.HasValue && !capped
                        ? $"×{BalanceMath.RubyMultiplier(level):0.00}  →  ×{BalanceMath.RubyMultiplier(level+1):0.00}"
                        : $"현재 ×{BalanceMath.RubyMultiplier(level):0.00}";
                _cards[i].Set(i==1?"경험치 획득":"골드 획득",$"Lv. {level}/50",detail,UguiTheme.RoyalBlue);
                if (pending && state.PendingRubyRequestExperience == (i==1))
                {
                    _buy[i].Set("이전 요청 확인",state.PendingRubyRequestIsReset ? "서버 결과 조회" : "같은 요청 재시도",available && !busy);
                }
                else
                {
                    bool ready = available && !busy && !pending && !queued;
                    string upgradeCost = cost.HasValue ? $"{NumberNotation.Format(cost.Value)} 루비" : "강화 완료";
                    _buy[i].Set(busy ? "확인 중…" : !cost.HasValue || capped ? "최대 단계" : "1회 강화",upgradeCost,
                        ready && unlocked && cost.HasValue && ruby>=cost.Value && !capped);
                }
            }
        }
    }
}
