using KingdomIdle.Balance;
using UnityEngine;
namespace KingdomIdle.UGUI
{
    public sealed class RubyGrowthCards : MonoBehaviour
    {
        private readonly EnhanceCardView[] _cards = new EnhanceCardView[2];
        private readonly GachaPullButtonView[] _buy = new GachaPullButtonView[2], _reset = new GachaPullButtonView[2];
        private void OnEnable() { LocalProgression.Changed += Refresh; NumberNotation.Changed += Refresh; Refresh(); }
        private void OnDisable() { LocalProgression.Changed -= Refresh; NumberNotation.Changed -= Refresh; }
        public void Build(Transform parent)
        {
            var catalog = UIManager.Instance.Catalog;
            for (int i=0;i<2;i++)
            {
                int index=i;
                _cards[i] = Instantiate(catalog.itemEnhanceCard,parent,false).GetComponent<EnhanceCardView>();
                _buy[i] = Instantiate(catalog.itemGachaPullButton,_cards[i].ButtonRow,false).GetComponent<GachaPullButtonView>();
                _reset[i] = Instantiate(catalog.itemGachaPullButton,_cards[i].ButtonRow,false).GetComponent<GachaPullButtonView>();
                _buy[i].Button.onClick.AddListener(()=> { if (!RubyProgression.Enhance(index==1)) UIManager.Instance.ShowToast("루비와 강화 조건을 확인해 주세요."); });
                _reset[i].Button.onClick.AddListener(()=> { bool ok=RubyProgression.Reset(index==1); UIManager.Instance.ShowToast(ok?"일반 웨이브 종료 후 초기화 · 실제 사용 루비 80% 반환":"하루 1회, 일반 전투에서만 초기화할 수 있습니다."); });
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
                _cards[i].Set(i==1?"EXP 획득 강화":"골드 획득 강화",$"Lv. {level}/50",unlocked?$"메인·방치 ×{BalanceMath.RubyMultiplier(level):0.00} · 루비 {NumberNotation.Format(LocalProgression.Balance(eCurrency.Ruby))}":"메인 2-5 클리어 후 해금");
                _buy[i].Set("1회 강화",cost.HasValue?$"{NumberNotation.Format(cost.Value)} 루비":"MAX",unlocked && cost.HasValue && LocalProgression.Balance(eCurrency.Ruby)>=cost && (i==0||state.AccountLevel<200));
                _reset[i].Set("초기화 80% 반환",$"{NumberNotation.Format(BalanceMath.Floor(spent*.8m))} 루비",level>0 && state.PendingRubyReset==0 && state.RubyResetDay!=LocalProgression.KstDay && ChangeJob.CanQueueChange);
            }
        }
    }
}
