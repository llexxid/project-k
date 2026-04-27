using System.Collections.Generic;
using Scripts.Core;
using UnityEngine.UIElements;

namespace KingdomIdle.UIToolkit
{
    /// <summary>
    /// 육성 패널 컨트롤러.
    /// 모든 캐릭터 공용 강화(공격력 / 체력) 기능을 제공한다.
    /// 뽑기 패널과 동일한 비주얼 언어(설명/보유 바/ x1·x10 버튼 행)를 사용해
    /// 전반적인 UI 일관성을 유지한다.
    ///
    /// 버튼은 표준 <see cref="Button"/> + clicked 이벤트를 사용한다.
    /// 일부 모바일 안정화 경로(RegisterMobileTap)가 이벤트 전파와 충돌해
    /// 클릭이 누락되는 현상이 있었기 때문에, 뽑기 패널과 동일한 방식으로
    /// 되돌려 안정성을 확보했다.
    ///
    /// 서버 동기화는 <see cref="StatEnhanceManager.TryEnhanceEx"/> 내부에서
    /// 낙관적 로컬 차감 + PlayFab CloudScript(OnEnChantATK / OnEnChantHP) 호출로
    /// 처리되며, 실패 시 자동 롤백된다. 클라이언트는 UI만 담당한다.
    /// </summary>
    public static class UITKDevelopmentPanelController
    {
        // 방치형 표준: x1 / x10
        private static readonly int[] PullCounts = { 1, 10 };

        // 실제로 플레이어 스탯에 반영되는 강화 항목만 노출.
        // CritRate / CritDamage / ExpGain 은 더미라서 StatEnhanceManager.IsStatImplemented 로도 걸러진다.
        private static readonly StatEnhanceManager.EnhanceType[] EnhanceTypes =
        {
            StatEnhanceManager.EnhanceType.Attack,
            StatEnhanceManager.EnhanceType.MaxHP,
        };

        private static ScrollView _content;
        private static readonly List<Button> _activeButtons = new List<Button>();
        private static bool _subscribedCurrency;
        private static StatEnhanceManager _subscribedEnhanceMgr;

        public static void Populate(VisualElement panelRoot)
        {
            if (panelRoot == null) return;

            _content = panelRoot.Q<ScrollView>("DevContent");
            if (_content == null) return;

            SubscribeEvents();
            Refresh();
        }

        // ── 이벤트 구독 ────────────────────────────────────────────────
        // 골드 변경 / 강화 완료 시 자동 새로고침. 정적 컨트롤러이므로 한 번만 구독한다.

        private static void SubscribeEvents()
        {
            // 통화 이벤트는 한 번만 구독.
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
                    _subscribedEnhanceMgr.OnEnhanced -= OnEnhanced;
                mgr.OnEnhanced += OnEnhanced;
                _subscribedEnhanceMgr = mgr;
            }
        }

        private static void OnCurrencyChanged(eCurrency currency, long amount)
        {
            if (currency != eCurrency.Gold) return;
            if (_content == null || _content.panel == null) return;
            Refresh();
        }

        private static void OnEnhanced()
        {
            if (_content == null || _content.panel == null) return;
            Refresh();
        }

        // ── 본문 ──────────────────────────────────────────────────────

        private static void Refresh()
        {
            if (_content == null) return;

            _content.Clear();
            _activeButtons.Clear();

            // 설명
            var desc = new Label("골드를 소비해 모든 캐릭터의 공격력과 체력을 영구 강화합니다.");
            desc.AddToClassList("ka-dev-desc");
            _content.Add(desc);

            // 보유 골드 바
            EconomyBridge.TryGetAmount(eCurrency.Gold, out long gold);
            var goldBar = new Label($"보유 골드  {gold:N0} G");
            goldBar.AddToClassList("ka-dev-gold-bar");
            _content.Add(goldBar);

            var mgr = StatEnhanceManager.Instance;

            foreach (var type in EnhanceTypes)
            {
                if (!StatEnhanceManager.IsStatImplemented(type)) continue;

                _content.Add(BuildEnhanceCard(mgr, type, gold));
            }

            if (_content.childCount <= 2)
            {
                var empty = new Label("강화 가능한 항목이 없습니다.");
                empty.AddToClassList("ka-placeholder-text");
                _content.Add(empty);
            }
        }

        private static VisualElement BuildEnhanceCard(
            StatEnhanceManager mgr, StatEnhanceManager.EnhanceType type, long gold)
        {
            int level = mgr != null ? mgr.GetLevel(type) : 0;
            string bonusText = mgr != null ? mgr.GetBonusText(type) : "+0%";
            string typeName = StatEnhanceManager.GetTypeName(type);

            var card = new VisualElement();
            card.AddToClassList("ka-enhance-card");

            // 헤더: 이름 + 레벨 배지
            var header = new VisualElement();
            header.AddToClassList("ka-enhance-card-header");

            var nameLbl = new Label(typeName);
            nameLbl.AddToClassList("ka-enhance-card-name");
            header.Add(nameLbl);

            var levelLbl = new Label($"Lv. {level}");
            levelLbl.AddToClassList("ka-enhance-card-level");
            header.Add(levelLbl);

            card.Add(header);

            // 현재 효과
            var bonusLbl = new Label($"현재 효과  {bonusText}");
            bonusLbl.AddToClassList("ka-enhance-card-bonus");
            card.Add(bonusLbl);

            // 버튼 행 (x1 / x10)
            var btnRow = new VisualElement();
            btnRow.AddToClassList("ka-enhance-btn-row");

            for (int i = 0; i < PullCounts.Length; i++)
            {
                int count = PullCounts[i];
                int cost = mgr != null ? mgr.GetCost(type, count) : 0;
                bool canAfford = gold >= cost;

                // 캡처용 로컬 변수 — 람다가 순회 변수를 그대로 붙잡지 않도록 복사.
                var capturedType = type;
                var capturedCount = count;

                var btn = new Button(() => OnEnhanceClicked(capturedType, capturedCount));
                btn.AddToClassList("ka-enhance-pull-btn");

                // 버튼 내부 텍스트: 상단 "강화 x1" + 하단 "50 G"
                var titleLbl = new Label($"강화 x{count}");
                titleLbl.AddToClassList("ka-enhance-pull-btn-title");
                titleLbl.pickingMode = PickingMode.Ignore;
                btn.Add(titleLbl);

                var costLbl = new Label($"{cost:N0} G");
                costLbl.AddToClassList("ka-enhance-pull-btn-cost");
                costLbl.pickingMode = PickingMode.Ignore;
                btn.Add(costLbl);

                if (!canAfford)
                {
                    btn.SetEnabled(false);
                    btn.AddToClassList("ka-enhance-pull-btn-disabled");
                }

                _activeButtons.Add(btn);
                btnRow.Add(btn);
            }

            card.Add(btnRow);
            return card;
        }

        // ── 액션 ──────────────────────────────────────────────────────
        // 서버 동기화는 StatEnhanceManager 내부에서 수행된다.
        // (Attack / MaxHP 는 OnEnChantATK / OnEnChantHP CloudScript 호출)
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

        private static void ShowToast(string msg)
        {
            var uiMgr = UITKUIManager.Instance;
            if (uiMgr != null) uiMgr.ShowToast(msg);
        }
    }
}
