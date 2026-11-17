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
        // 서버가 지원하는 요청 횟수만 선택한다. 각 카드에는 선택한 횟수의 주행동 하나만 표시한다.
        private static readonly int[] PullCounts = { 1, 10, -1 };
        private static int _selectedCount = 1;
        internal const float GrowthTabHeight = 128f, PurchaseCountHeight = 96f;

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
        private static readonly List<NavTabButtonView> CountTabs = new();
        private static bool _subscribedCurrency;
        private static StatEnhanceManager _subscribedEnhanceMgr;
        private sealed class CardBinding
        {
            internal EnhanceCardView View;
            internal StatEnhanceManager.EnhanceType Type;
            internal GachaPullButtonView Button;
            internal ActionButtonView FallbackButton;
        }
        private static readonly List<CardBinding> Cards = new();

        /// <summary>중첩 육성 화면이 닫힌 뒤 원래 패널의 정적 바인딩과 변경 구독을 복구한다.</summary>
        internal static void Restore(DevelopmentPanelView view)
        {
            if (_view != view) Populate(view);
        }

        /// <summary>안내 재개 때 골드 탭을 복원한다. 기존 UI 갱신을 재사용하고 강화는 실행하지 않는다.</summary>
        internal static void PrepareGuide()
        {
            _rubyTab = false;
            _selectedCount = 1;
            Refresh();
        }
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
                    _view = null; _body = null; Cards.Clear(); GrowthTabs.Clear(); CountTabs.Clear();
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
            string description = _rubyTab ? "환생 후에도 유지 · 사냥과 방치 보상에 적용" : "모든 왕국군에 적용 · 환생 시 골드 강화 초기화";
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
            if (body.purchaseCountBar != null) body.purchaseCountBar.gameObject.SetActive(!_rubyTab);
            for (int i=0;i<GrowthTabs.Count;i++) GrowthTabs[i].SetSelected((i==1)==_rubyTab, UguiTheme.RoyalBlue);
            for (int i=0;i<CountTabs.Count;i++) CountTabs[i].SetSelected(PullCounts[i] == _selectedCount, UguiTheme.RoyalBlue);
            foreach (var card in Cards)
            {
                if (card.View == null || mgr == null) continue;
                int level = mgr.GetLevel(card.Type);
                int actual = mgr.GetPurchaseCount(card.Type, _selectedCount);
                long cost = mgr.GetCost(card.Type, _selectedCount);
                string current = $"×{BalanceMath.GoldMultiplier(level):0.###}";
                string next = $"×{BalanceMath.GoldMultiplier(level + actual):0.###}";
                card.View.Set(StatEnhanceManager.GetTypeName(card.Type), $"Lv. {mgr.GetLevel(card.Type):N0}",
                    actual > 0 ? $"{current}  →  {next}" : $"현재 {current}", UguiTheme.RoyalBlue);
                bool maxed = level >= BalanceMath.GoldCap;
                string title = mgr.IsEnhancing ? "강화 중…" : maxed ? "최대 단계" :
                    _selectedCount < 0 ? $"최대 {actual}회" : $"{actual}회 강화";
                string price = maxed ? "강화 완료" : cost < 0 ? "골드 부족" : $"{NumberNotation.Format(cost)} 골드";
                bool ready = !mgr.IsEnhancing && cost >= 0 && actual > 0 && gold >= cost;
                card.Button?.Set(title, price, ready);
                card.FallbackButton?.Set($"{title} · {price}", UguiTheme.ActionGold, ready);
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
            // 본문 셸은 어두운 목재이며 카드 내부에만 밝은 정보면이 있다. 구 프리팹 재사용도 같은 대비로 복구한다.
            if (_body != null)
            {
                if (_body.goldLabel != null) _body.goldLabel.color = UguiTheme.TextPrimary;
                if (_body.descLabel != null) _body.descLabel.color = UguiTheme.TextSecondary;
                if (_body.emptyLabel != null) _body.emptyLabel.color = UguiTheme.TextSecondary;
            }
            GrowthTabs.Clear();
            CountTabs.Clear();
            if (_body != null && _body.navBar != null)
            {
                for (int i=0;i<2;i++)
                {
                    bool ruby=i==1;
                    var tab=Object.Instantiate(cat.itemNavTabButton,_body.navBar,false).GetComponent<NavTabButtonView>();
                    tab.name=ruby?"RubyGrowthTab":"GoldGrowthTab";
                    tab.SetLabel(ruby?"루비 영구 성장":"골드 강화");tab.SetIcon(ruby?cat.iconGem:cat.iconCoin);
                    var layout=tab.GetComponent<LayoutElement>();
                    if(layout!=null)layout.minHeight=layout.preferredHeight=GrowthTabHeight;
                    tab.Button.onClick.AddListener(()=> {
                        _rubyTab=ruby;Refresh();
                        var scroll=_view?.GetComponentInChildren<ScrollRect>();if(scroll!=null)scroll.verticalNormalizedPosition=1;
                    });
                    if (!ruby) FeatureGuideAnchor.Bind(tab.Button, Direction.GameDirectTarget.GoldGrowthTab);
                    GrowthTabs.Add(tab);
                }
            }
            if (_body != null && _body.purchaseCountBar != null && cat.itemNavTabButton != null)
            {
                for (int i = 0; i < PullCounts.Length; i++)
                {
                    int count = PullCounts[i];
                    var tab = Object.Instantiate(cat.itemNavTabButton, _body.purchaseCountBar, false).GetComponent<NavTabButtonView>();
                    tab.name = count < 0 ? "MaximumCount" : $"Count{count}";
                    tab.SetLabel(count < 0 ? "최대" : $"{count}회");
                    tab.SetIcon(null);
                    var layout=tab.GetComponent<LayoutElement>();
                    if(layout!=null)layout.minHeight=layout.preferredHeight=PurchaseCountHeight;
                    tab.Button.onClick.AddListener(() => { _selectedCount = count; Refresh(); });
                    CountTabs.Add(tab);
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

            // 각 카드의 유일한 주버튼은 클릭 시점에 선택된 횟수를 기존 강화 진입점으로 보낸다.
            if (cat.itemGachaPullButton != null)
            {
                var btnGo = Object.Instantiate(cat.itemGachaPullButton, view.ButtonRow, false);
                var pull = btnGo.GetComponent<GachaPullButtonView>();
                if (pull != null)
                {
                    binding.Button = pull;
                    pull.Button.onClick.AddListener(() => OnEnhanceClicked(type, _selectedCount));
                    if (type == StatEnhanceManager.EnhanceType.Attack)
                        FeatureGuideAnchor.Bind(pull.Button, Direction.GameDirectTarget.AttackOnce);
                    return true;
                }
                Object.Destroy(btnGo);
            }
            if (cat.itemActionButton != null)
            {
                var abGo = Object.Instantiate(cat.itemActionButton, view.ButtonRow, false);
                var ab = abGo.GetComponent<ActionButtonView>();
                if (ab != null)
                {
                    binding.FallbackButton = ab;
                    ab.OnClick(() => OnEnhanceClicked(type, _selectedCount));
                    if (type == StatEnhanceManager.EnhanceType.Attack)
                        FeatureGuideAnchor.Bind(ab.Button, Direction.GameDirectTarget.AttackOnce);
                }
            }

            return true;
        }

        // ── 액션 ──────────────────────────────────────────────────────
        // 서버 호출은 StatEnhanceManager 내부에서 수행된다. 정상 경로는 Pending을 돌려주고
        // 결과(성공/서버 거절)는 OnEnhanceCompleted로 온다. 로컬에서 바로 거절되는 사유만 여기서 안내한다.

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
                case StatEnhanceManager.EnhanceResult.MaxLevel:
                    ShowToast("최대 강화 단계입니다.");
                    break;
                default:
                    ShowToast("강화에 실패했습니다.");
                    break;
            }
        }

        // 서버 응답 뒤 호출된다. 성공은 적용된 단계로, 실패는 서버 ErrorCode로 안내한다.
        private static void OnEnhanceCompleted(StatEnhanceManager.EnhanceType type, bool success)
        {
            var mgr = StatEnhanceManager.Instance;
            if (success && mgr != null)
                ShowToast($"{StatEnhanceManager.GetTypeName(type)} Lv.{mgr.GetLevel(type)} ({mgr.GetBonusText(type)})");
            else if (!success)
                ShowToast(DescribeFailure(mgr?.LastFailure));
            Refresh();
        }

        // 서버 ErrorCode → 문구 표는 NetworkManager.DescribeFailure가 공통으로 가진다.
        private static string DescribeFailure(PlayFab.PlayFabError error) => Scripts.Core.Manager.NetworkManager.DescribeFailure(error, "강화");

        private static void ShowToast(string msg)
        {
            var uiMgr = UIManager.Instance;
            if (uiMgr != null) uiMgr.ShowToast(msg);
        }
    }
}
