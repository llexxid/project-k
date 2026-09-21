using KingdomIdle.Balance;
using System.Collections.Generic;
using Scripts.Core;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace KingdomIdle.UGUI
{
    // Shared gold growth and ruby income progression; all costs come from the transaction service.
    public static class DevelopmentPanelController
    {
        // 방치형 표준: x1 / x10
        private static readonly int[] PullCounts = { 1, 10, -1 };

        // 실제로 플레이어 스탯에 반영되는 강화 항목만 노출.
        private static readonly StatEnhanceManager.EnhanceType[] EnhanceTypes =
        {
            StatEnhanceManager.EnhanceType.Attack,
            StatEnhanceManager.EnhanceType.MaxHP,
        };

        private static DevelopmentPanelView _view;
        private static DevelopmentBodyView _body;
        private static bool _rubyTab;
        private static readonly List<NavTabButtonView> GrowthTabs = new();
        private static bool _subscribedCurrency;
        private static StatEnhanceManager _subscribedEnhanceMgr;
        private sealed class CardBinding
        {
            internal EnhanceCardView View;
            internal StatEnhanceManager.EnhanceType Type;
            internal readonly List<GachaPullButtonView> Buttons = new();
        }
        private static readonly List<CardBinding> Cards = new();

        /// <summary>중첩 육성 화면이 닫힌 뒤 원래 패널의 정적 바인딩과 변경 구독을 복구한다.</summary>
        internal static void Restore(DevelopmentPanelView view)
        {
            if (_view != view) Populate(view);
        }

        /// <summary>안내 재개 때 골드 탭을 복원한다. 기존 UI 갱신을 재사용하고 강화는 실행하지 않는다.</summary>
        internal static void PrepareGuide() { if (_rubyTab) { _rubyTab = false; Refresh(); } }
        internal static bool GuideReady => _view != null && _view.gameObject.activeInHierarchy && !_rubyTab;

        /// <summary>패널 생성 때 본문과 경제 구독을 연결한다. view를 보관하고 닫힘 콜백에서 참조·구독을 정리한다.</summary>
        public static void Populate(DevelopmentPanelView view)
        {
            if (view == null) return;

            _view = view;
            NumberNotationBinding.Bind(view, Refresh);
            if (_view.content == null) return;

            // 패널이 닫히면 뷰 참조 해제 (통화 이벤트 구독은 원본과 동일하게 유지하되
            // 핸들러가 파괴된 뷰를 만지지 않도록 가드)
            view.OnClosed = () =>
            {
                if (_view == view)
                {
                    _view = null; _body = null; Cards.Clear(); GrowthTabs.Clear();
                    EconomyBridge.OnAmountChanged -= OnCurrencyChanged;
                    _subscribedCurrency = false;
                    if (_subscribedEnhanceMgr != null)
                    {
                        _subscribedEnhanceMgr.OnEnhanced -= OnEnhanced;
                        _subscribedEnhanceMgr.OnEnhanceCompleted -= OnEnhanceCompleted;
                    }
                    _subscribedEnhanceMgr = null;
                }
            };

            SubscribeEvents();
            Refresh();
        }

        // ── 이벤트 구독 ────────────────────────────────────────────────
        // 골드 변경 / 강화 완료 시 자동 새로고침. 정적 컨트롤러이므로 한 번만 구독한다.

        private static void SubscribeEvents()
        {
            if (!_subscribedCurrency)
            {
                EconomyBridge.OnAmountChanged += OnCurrencyChanged;
                _subscribedCurrency = true;
            }

            // 강화 매니저는 Populate 시점에 아직 초기화 전일 수 있으므로
            // 현재 Instance 를 매번 확인해 (필요 시 재구독) 지연 등장한 매니저에도 대응.
            var mgr = StatEnhanceManager.Instance;
            if (mgr != null && _subscribedEnhanceMgr != mgr)
            {
                if (_subscribedEnhanceMgr != null)
                {
                    _subscribedEnhanceMgr.OnEnhanced -= OnEnhanced;
                    _subscribedEnhanceMgr.OnEnhanceCompleted -= OnEnhanceCompleted;
                }
                mgr.OnEnhanced += OnEnhanced;
                mgr.OnEnhanceCompleted += OnEnhanceCompleted;
                _subscribedEnhanceMgr = mgr;
            }
        }

        private static void OnCurrencyChanged(eCurrency currency, long amount)
        {
            if (currency != eCurrency.Gold && currency != eCurrency.Ruby) return;
            if (_view == null || _view.content == null) return;
            Refresh();
        }

        private static void OnEnhanced()
        {
            if (_view == null || _view.content == null) return;
            Refresh();
        }

        // ── 본문 ──────────────────────────────────────────────────────

        private static void Refresh()
        {
            if (_view == null || _view.content == null) return;

            var body = EnsureBody(_view.content);
            if (body == null) return;

            // 보유 골드 바 (.ka-dev-gold-bar: 26px gold bold) — 텍스트만 갱신
            EconomyBridge.TryGetAmount(eCurrency.Gold, out long gold);
            string balance = _rubyTab ? $"보유 루비  {NumberNotation.Format(LocalProgression.Balance(eCurrency.Ruby))}" : $"보유 골드  {NumberNotation.Format(gold)} G";
            if (body.goldLabel != null && body.goldLabel.text != balance) body.goldLabel.text = balance;
            string description = _rubyTab ? "환생에도 유지되는 영구 성장 · 메인 사냥과 방치 보상을 강화합니다." : "골드로 모든 왕국군의 공격력과 체력을 강화합니다.";
            if (body.descLabel != null && body.descLabel.text != description) body.descLabel.text = description;

            var mgr = StatEnhanceManager.Instance;
            if (Cards.Count == 0)
            {
                foreach (var type in EnhanceTypes)
                    if (StatEnhanceManager.IsStatImplemented(type)) BuildEnhanceCard(body.CardsRoot, mgr, type, gold);
                if (body.rubyCardsRoot != null && body.rubyCardsRoot.GetComponent<RubyGrowthCards>() == null)
                    body.rubyCardsRoot.gameObject.AddComponent<RubyGrowthCards>().Build(body.rubyCardsRoot);
            }
            body.CardsRoot.gameObject.SetActive(!_rubyTab);
            if (body.rubyCardsRoot != null) body.rubyCardsRoot.gameObject.SetActive(_rubyTab);
            for (int i=0;i<GrowthTabs.Count;i++) GrowthTabs[i].SetSelected((i==1)==_rubyTab, _rubyTab ? UguiTheme.RarityArcane : UguiTheme.AccentGold);
            foreach (var card in Cards)
            {
                if (card.View == null || mgr == null) continue;
                card.View.Set(StatEnhanceManager.GetTypeName(card.Type), $"Lv. {mgr.GetLevel(card.Type):N0}",
                    $"전체 왕국군 ×{BalanceMath.GoldMultiplier(mgr.GetLevel(card.Type)):0.###}");
                for (int i = 0; i < card.Buttons.Count; i++)
                {
                    int count = PullCounts[i]; long cost = mgr.GetCost(card.Type, count);
                    int actual = count < 0 ? BalanceMath.AffordableGoldLevels(mgr.GetLevel(card.Type), gold) : System.Math.Min(count,300-mgr.GetLevel(card.Type));
                    card.Buttons[i].Set(mgr.IsEnhancing ? "강화 처리 중…" : (count < 0 ? $"최대 {actual}회" : $"{actual}회 강화"),
                        cost < 0 ? "강화 한도" : $"{NumberNotation.Format(cost)} 골드",
                        !mgr.IsEnhancing && cost >= 0 && actual > 0 && gold >= cost);
                }
            }

            // 빈 상태 라벨 토글 (강화 항목이 하나도 없을 때만 표시)
            if (body.emptyLabel != null)
                body.emptyLabel.gameObject.SetActive(!_rubyTab && Cards.Count == 0);
        }

        /// <summary>본문 셸(Body_Development)을 스크롤 콘텐츠에 1회 인스턴스화하고 캐시한다.</summary>
        private static DevelopmentBodyView EnsureBody(RectTransform content)
        {
            // 유효한 캐시(동일 콘텐츠 하위)면 재사용. 패널 재오픈 시 콘텐츠가 바뀌면 재생성.
            if (_body != null && _body.transform.parent == content) return _body;

            ClearChildren(content);
            Cards.Clear();

            var cat = UIManager.Instance != null ? UIManager.Instance.Catalog : null;
            if (cat == null || cat.bodyDevelopment == null)
            {
                Debug.LogWarning("[DevelopmentPanel] 카탈로그의 bodyDevelopment 프리팹이 없습니다.");
                _body = null;
                return null;
            }

            var go = Object.Instantiate(cat.bodyDevelopment, content, false);
            _body = go.GetComponent<DevelopmentBodyView>();
            GrowthTabs.Clear();
            if (_body != null && _body.navBar != null)
            {
                for (int i=0;i<2;i++)
                {
                    bool ruby=i==1;
                    var tab=Object.Instantiate(cat.itemNavTabButton,_body.navBar,false).GetComponent<NavTabButtonView>();
                    tab.name=ruby?"RubyGrowthTab":"GoldGrowthTab";
                    tab.SetLabel(ruby?"루비 영구 성장":"골드 강화");tab.SetIcon(ruby?cat.iconGem:cat.iconCoin);
                    tab.Button.onClick.AddListener(()=> {
                        _rubyTab=ruby;Refresh();
                        var scroll=_view?.GetComponentInChildren<ScrollRect>();if(scroll!=null)scroll.verticalNormalizedPosition=1;
                    });
                    if (!ruby) FeatureGuideAnchor.Bind(tab.Button, Direction.GameDirectTarget.GoldGrowthTab);
                    GrowthTabs.Add(tab);
                }
            }
            if (_body == null)
            {
                Debug.LogError("[DevelopmentPanel] DevelopmentBodyView 컴포넌트가 없습니다.");
                Object.Destroy(go);
            }
            return _body;
        }

        /// <summary>자식 전부 비활성화 후 파괴 (동적 리스트 재구성용).</summary>
        private static void ClearChildren(Transform parent)
        {
            if (parent == null) return;
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                var child = parent.GetChild(i).gameObject;
                child.SetActive(false);
                Object.Destroy(child);
            }
        }

        /// <summary>.ka-enhance-card: 이름/레벨/효과/버튼 행. Item_EnhanceCard 프리팹 인스턴스화.</summary>
        /// <returns>카드를 실제로 생성했으면 true.</returns>
        private static bool BuildEnhanceCard(
            RectTransform parent, StatEnhanceManager mgr, StatEnhanceManager.EnhanceType type, long gold)
        {
            int level = mgr != null ? mgr.GetLevel(type) : 0;
            string bonusText = mgr != null ? mgr.GetBonusText(type) : "+0%";
            string typeName = StatEnhanceManager.GetTypeName(type);

            var cat = UIManager.Instance != null ? UIManager.Instance.Catalog : null;
            if (cat == null || cat.itemEnhanceCard == null) return false;

            // 프리팹 카드 (외형은 Item_EnhanceCard.prefab 에서 편집)
            var go = Object.Instantiate(cat.itemEnhanceCard, parent, false);
            var view = go.GetComponent<EnhanceCardView>();
            if (view == null) { Object.Destroy(go); return false; }
            var binding = new CardBinding { View = view, Type = type };
            Cards.Add(binding);

            view.Set(typeName, $"Lv. {level}", $"현재 효과  {bonusText}");

            // x1 / x10 버튼을 Item_GachaPullButton 스타일(제목+비용)의 큰 버튼으로
            for (int i = 0; i < PullCounts.Length; i++)
            {
                int count = PullCounts[i];
                long cost = mgr != null ? mgr.GetCost(type, count) : 0;
                bool canAfford = gold >= cost;
                var capturedType = type;
                var capturedCount = count;

                if (cat.itemGachaPullButton != null)
                {
                    var btnGo = Object.Instantiate(cat.itemGachaPullButton, view.ButtonRow, false);
                    var pull = btnGo.GetComponent<GachaPullButtonView>();
                    if (pull != null)
                    {
                        binding.Buttons.Add(pull);
                        pull.Set($"강화 x{count}", $"{NumberNotation.Format(cost)} G", canAfford, null);
                        pull.Button.onClick.AddListener(() => OnEnhanceClicked(capturedType, capturedCount));
                        if (type == StatEnhanceManager.EnhanceType.Attack && count == 1)
                            FeatureGuideAnchor.Bind(pull.Button, Direction.GameDirectTarget.AttackOnce);
                        continue;
                    }
                }

                // 폴백: 액션 버튼
                if (cat.itemActionButton != null)
                {
                    var abGo = Object.Instantiate(cat.itemActionButton, view.ButtonRow, false);
                    var ab = abGo.GetComponent<ActionButtonView>();
                    if (ab != null)
                    {
                        ab.Set($"강화 x{count} ({NumberNotation.Format(cost)}G)", UguiTheme.BtnSpend, canAfford);
                        ab.OnClick(() => OnEnhanceClicked(capturedType, capturedCount));
                    }
                }
            }

            return true;
        }

        // ── 액션 ──────────────────────────────────────────────────────
        // 서버 동기화는 StatEnhanceManager 내부에서 수행된다.
        // 실패 시 TryEnhanceEx 가 구체적 사유를 반환하고 로컬 상태는 자동 롤백된다.

        private static void OnEnhanceClicked(StatEnhanceManager.EnhanceType type, int count)
        {
            var mgr = StatEnhanceManager.Instance;
            if (mgr == null)
            {
                ShowToast("강화 시스템이 초기화되지 않았습니다.");
                return;
            }

            var result = mgr.TryEnhanceEx(type, count);
            switch (result)
            {
                case StatEnhanceManager.EnhanceResult.Pending:
                case StatEnhanceManager.EnhanceResult.Busy:
                    Refresh();
                    break;
                case StatEnhanceManager.EnhanceResult.Success:
                {
                    string n = StatEnhanceManager.GetTypeName(type);
                    string b = mgr.GetBonusText(type);
                    int lv = mgr.GetLevel(type);
                    ShowToast($"{n} Lv.{lv} ({b})");
                    // UI 갱신은 OnEnhanced 이벤트 핸들러가 Refresh 를 호출해 수행.
                    break;
                }
                case StatEnhanceManager.EnhanceResult.NotEnoughGold:
                    ShowToast("골드가 부족합니다.");
                    break;
                case StatEnhanceManager.EnhanceResult.NetworkNotReady:
                    ShowToast("네트워크 세션이 준비되지 않았습니다.");
                    break;
                default:
                    ShowToast("강화에 실패했습니다.");
                    break;
            }
        }

        private static void OnEnhanceCompleted(StatEnhanceManager.EnhanceType type, bool success)
        {
{
            Refresh();
        }
    }

        private static void ShowToast(string msg)
        {
            var uiMgr = UIManager.Instance;
            if (uiMgr != null) uiMgr.ShowToast(msg);
        }
    }
}
