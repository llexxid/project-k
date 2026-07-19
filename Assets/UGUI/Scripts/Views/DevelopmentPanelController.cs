using System.Collections.Generic;
using Scripts.Core;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace KingdomIdle.UGUI
{
    /// <summary>
    /// 육성 패널 컨트롤러 (UITKDevelopmentPanelController 이식).
    /// 모든 캐릭터 공용 강화(공격력 / 체력) 기능을 제공한다.
    /// 뽑기 패널과 동일한 비주얼 언어(설명/보유 바/ x1·x10 버튼 행)를 사용해
    /// 전반적인 UI 일관성을 유지한다.
    ///
    /// 서버 동기화는 StatEnhanceManager.TryEnhanceEx 내부에서
    /// 낙관적 로컬 차감 + PlayFab CloudScript(OnEnChantATK / OnEnChantHP) 호출로
    /// 처리되며, 실패 시 자동 롤백된다. 클라이언트는 UI만 담당한다.
    /// </summary>
    public static class DevelopmentPanelController
    {
        // 방치형 표준: x1 / x10
        private static readonly int[] PullCounts = { 1, 10 };

        // 실제로 플레이어 스탯에 반영되는 강화 항목만 노출.
        private static readonly StatEnhanceManager.EnhanceType[] EnhanceTypes =
        {
            StatEnhanceManager.EnhanceType.Attack,
            StatEnhanceManager.EnhanceType.MaxHP,
        };

        private static DevelopmentPanelView _view;
        private static bool _subscribedCurrency;
        private static StatEnhanceManager _subscribedEnhanceMgr;

        public static void Populate(DevelopmentPanelView view)
        {
            if (view == null) return;

            _view = view;
            if (_view.content == null) return;

            // 패널이 닫히면 뷰 참조 해제 (통화 이벤트 구독은 원본과 동일하게 유지하되
            // 핸들러가 파괴된 뷰를 만지지 않도록 가드)
            view.OnClosed = () =>
            {
                if (_view == view) _view = null;
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
                    _subscribedEnhanceMgr.OnEnhanced -= OnEnhanced;
                mgr.OnEnhanced += OnEnhanced;
                _subscribedEnhanceMgr = mgr;
            }
        }

        private static void OnCurrencyChanged(eCurrency currency, long amount)
        {
            if (currency != eCurrency.Gold) return;
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

            var content = _view.content;
            UguiRuntimeFactory.Clear(content);

            // 설명 (.ka-dev-desc: 22px @70%)
            var desc = UguiRuntimeFactory.Label(content,
                "골드를 소비해 모든 캐릭터의 공격력과 체력을 영구 강화합니다.",
                22f, new Color(1f, 1f, 1f, 0.70f), TextAlignmentOptions.Left, wrap: true);
            UguiRuntimeFactory.Preferred(desc, height: 60f);

            // 보유 골드 바 (.ka-dev-gold-bar: 26px gold bold)
            EconomyBridge.TryGetAmount(eCurrency.Gold, out long gold);
            var goldBar = UguiRuntimeFactory.Label(content,
                $"보유 골드  {gold:N0} G", 26f, UguiTheme.AccentGoldStrong, TextAlignmentOptions.Left, bold: true);
            UguiRuntimeFactory.Preferred(goldBar, height: 44f);

            var mgr = StatEnhanceManager.Instance;

            int cardCount = 0;
            foreach (var type in EnhanceTypes)
            {
                if (!StatEnhanceManager.IsStatImplemented(type)) continue;

                BuildEnhanceCard(content, mgr, type, gold);
                cardCount++;
            }

            if (cardCount == 0)
            {
                var empty = UguiRuntimeFactory.Label(content, "강화 가능한 항목이 없습니다.",
                    24f, new Color(1f, 1f, 1f, 0.40f), TextAlignmentOptions.Center);
                UguiRuntimeFactory.Preferred(empty, height: 60f);
            }
        }

        /// <summary>.ka-enhance-card: bg white@6% radius12 + 3px 좌측 액센트, 이름/레벨/효과/버튼 행.</summary>
        private static void BuildEnhanceCard(
            RectTransform parent, StatEnhanceManager mgr, StatEnhanceManager.EnhanceType type, long gold)
        {
            int level = mgr != null ? mgr.GetLevel(type) : 0;
            string bonusText = mgr != null ? mgr.GetBonusText(type) : "+0%";
            string typeName = StatEnhanceManager.GetTypeName(type);

            var card = UguiRuntimeFactory.Box(parent, "EnhanceCard", UguiTheme.SurfaceFaint);
            UguiRuntimeFactory.VerticalLayout(card.gameObject, 8f, new RectOffset(16, 16, 14, 14));

            // 좌측 액센트 바 (border-left: 3px #64B4FF@55%)
            var accent = UguiRuntimeFactory.Box(card.transform, "Accent", new Color(100f / 255f, 180f / 255f, 1f, 0.55f), rounded: false);
            var accentRt = accent.rectTransform;
            accentRt.anchorMin = new Vector2(0f, 0f);
            accentRt.anchorMax = new Vector2(0f, 1f);
            accentRt.pivot = new Vector2(0f, 0.5f);
            accentRt.anchoredPosition = Vector2.zero;
            accentRt.sizeDelta = new Vector2(3f, 0f);
            var accentLe = accent.gameObject.AddComponent<LayoutElement>();
            accentLe.ignoreLayout = true;

            // 헤더: 이름 + 레벨 배지
            var header = UguiRuntimeFactory.Container(card.transform, "Header");
            UguiRuntimeFactory.HorizontalLayout(header.gameObject, 10f, null, TextAnchor.MiddleLeft);
            UguiRuntimeFactory.Preferred(header, height: 40f);

            var nameLbl = UguiRuntimeFactory.Label(header, typeName, 28f, UguiTheme.TextPrimary, TextAlignmentOptions.Left, bold: true);
            UguiRuntimeFactory.Flexible(nameLbl, 1f);

            UguiRuntimeFactory.Label(header, $"Lv. {level}", 22f, new Color(100f / 255f, 180f / 255f, 1f, 1f), TextAlignmentOptions.Right, bold: true);

            // 현재 효과
            var bonusLbl = UguiRuntimeFactory.Label(card.transform, $"현재 효과  {bonusText}", 20f,
                UguiTheme.SuccessGreenBright);
            UguiRuntimeFactory.Preferred(bonusLbl, height: 30f);

            // 버튼 행 (x1 / x10)
            var btnRow = UguiRuntimeFactory.Container(card.transform, "BtnRow");
            UguiRuntimeFactory.HorizontalLayout(btnRow.gameObject, 10f, null, TextAnchor.MiddleCenter, expandWidth: true);
            UguiRuntimeFactory.Preferred(btnRow.gameObject.AddComponent<LayoutElement>(), height: 90f);

            for (int i = 0; i < PullCounts.Length; i++)
            {
                int count = PullCounts[i];
                int cost = mgr != null ? mgr.GetCost(type, count) : 0;
                bool canAfford = gold >= cost;

                // 캡처용 로컬 변수 — 람다가 순회 변수를 그대로 붙잡지 않도록 복사.
                var capturedType = type;
                var capturedCount = count;

                var btnBg = UguiRuntimeFactory.Box(btnRow, $"BtnEnhanceX{count}",
                    canAfford ? UguiTheme.AccentBlue : UguiTheme.DisabledGrey, raycastTarget: true);
                UguiRuntimeFactory.Flexible(btnBg, 1f);
                UguiRuntimeFactory.VerticalLayout(btnBg.gameObject, 2f, new RectOffset(0, 0, 12, 12), TextAnchor.MiddleCenter);

                var btn = btnBg.gameObject.AddComponent<Button>();
                btn.targetGraphic = btnBg;
                btn.colors = UguiTheme.MakeColorBlock();
                btnBg.gameObject.AddComponent<PlayClickSfxOnClick>();
                btn.onClick.AddListener(() => OnEnhanceClicked(capturedType, capturedCount));
                btn.interactable = canAfford;

                // 픽셀 키트 버튼 스킨
                var skinCatalog = UIManager.Instance != null ? UIManager.Instance.Catalog : null;
                UguiPixelSkin.ApplyButton(btnBg, btn,
                    canAfford ? UguiTheme.AccentBlue : UguiTheme.DisabledGrey, skinCatalog);

                // 버튼 내부 텍스트: 상단 "강화 x1" + 하단 "50 G"
                var titleLbl = UguiRuntimeFactory.Label(btnBg.transform, $"강화 x{count}", 26f,
                    canAfford ? UguiTheme.TextPrimary : new Color(1f, 1f, 1f, 0.40f),
                    TextAlignmentOptions.Center, bold: true);
                UguiRuntimeFactory.Preferred(titleLbl, height: 36f);

                var costLbl = UguiRuntimeFactory.Label(btnBg.transform, $"{cost:N0} G", 20f,
                    canAfford ? UguiTheme.AccentGoldStrong : new Color(1f, 1f, 1f, 0.40f),
                    TextAlignmentOptions.Center);
                UguiRuntimeFactory.Preferred(costLbl, height: 28f);
            }
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
            var uiMgr = UIManager.Instance;
            if (uiMgr != null) uiMgr.ShowToast(msg);
        }
    }
}
