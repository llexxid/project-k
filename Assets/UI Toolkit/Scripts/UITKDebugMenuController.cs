// ┌──────────────────────────────────────────────────────────────────┐
// │  DEBUG MENU — 이 파일 하나만 삭제하면 디버그 기능이 모두 제거됩니다.  │
// │  추가로 UITKUIManager.InitDebugMenu() 호출 한 줄도 제거하세요.       │
// └──────────────────────────────────────────────────────────────────┘
using System;
using UnityEngine;
using UnityEngine.UIElements;
using Scripts.Core;
using KingdomIdle.KingdomArmy;

namespace KingdomIdle.UIToolkit
{
    /// <summary>
    /// 디버그 메뉴: 재화 +1000, 플레이어/몬스터 무적 토글.
    /// 제거 시 이 파일 삭제 + UITKUIManager에서 InitDebugMenu() 호출 제거.
    /// </summary>
    public static class UITKDebugMenuController
    {
        // ── 무적 플래그 (외부에서 참조) ──
        public static bool PlayerInvincible { get; private set; }
        public static bool MonsterInvincible { get; private set; }

        private static VisualElement _overlay;
        private static bool _isOpen;

        /// <summary>
        /// HudTop 왼쪽에 디버그 버튼을 추가하고, 팝업 오버레이를 생성한다.
        /// UITKUIManager.BindMainScreen()에서 호출.
        /// </summary>
        public static void Init(VisualElement root)
        {
            // ── 1) 디버그 버튼: 재화 버튼 바로 왼쪽 (hud-right-wrap 첫 번째) ──
            var hudRightWrap = root.Q<VisualElement>(className: "hud-right-wrap");
            if (hudRightWrap == null) return;

            var btn = new Button(() => Toggle(root)) { text = "DBG" };
            btn.name = "BtnDebugMenu";
            btn.AddToClassList("debug-menu-btn");
            hudRightWrap.Insert(0, btn); // 재화 버튼 바로 왼쪽에 삽입

            // ── 2) 팝업 오버레이 (처음엔 숨김) ──
            BuildOverlay(root);
        }

        private static void Toggle(VisualElement root)
        {
            if (_isOpen) Close();
            else Open(root);
        }

        private static void Open(VisualElement root)
        {
            if (_overlay == null) BuildOverlay(root);
            RefreshToggles();
            _overlay.RemoveFromClassList("hidden");
            _isOpen = true;
        }

        public static void Close()
        {
            _overlay?.AddToClassList("hidden");
            _isOpen = false;
        }

        // ── 토글 라벨 갱신 ──
        private static Label _lblPlayerToggle;
        private static Label _lblMonsterToggle;

        private static void RefreshToggles()
        {
            if (_lblPlayerToggle != null)
                _lblPlayerToggle.text = PlayerInvincible ? "플레이어 무적: ON" : "플레이어 무적: OFF";
            if (_lblMonsterToggle != null)
                _lblMonsterToggle.text = MonsterInvincible ? "몬스터 무적: ON" : "몬스터 무적: OFF";
        }

        // ── UI 빌드 ──
        private static void BuildOverlay(VisualElement root)
        {
            // Layer_Overlays까지 올라가야 다른 패널/팝업 위에 렌더링됨
            var docRoot = root;
            while (docRoot.parent != null) docRoot = docRoot.parent;
            var targetLayer = docRoot.Q<VisualElement>("Layer_Overlays") ?? root;

            _overlay = new VisualElement();
            _overlay.AddToClassList("debug-overlay");
            _overlay.AddToClassList("hidden");

            // 백드롭 (클릭하면 닫기)
            var backdrop = new VisualElement();
            backdrop.AddToClassList("debug-backdrop");
            backdrop.RegisterCallback<ClickEvent>(_ => Close());
            _overlay.Add(backdrop);

            // 패널
            var panel = new VisualElement();
            panel.AddToClassList("debug-panel");

            // 타이틀
            var title = new Label("Debug Menu");
            title.AddToClassList("debug-title");
            panel.Add(title);

            // ── 재화 버튼들 ──
            var sectionLabel = new Label("재화 추가");
            sectionLabel.AddToClassList("debug-section-label");
            panel.Add(sectionLabel);

            var currencyGrid = new VisualElement();
            currencyGrid.AddToClassList("debug-currency-grid");

            // ClassFragment 제외 (별도 시스템)
            AddCurrencyRow(currencyGrid, "골드", eCurrency.Gold);
            AddCurrencyRow(currencyGrid, "고대주화", eCurrency.AncientCoin);
            AddCurrencyRow(currencyGrid, "왕국물자", eCurrency.KingdomSupply);
            AddCurrencyRow(currencyGrid, "수련서", eCurrency.TrainingTome);
            AddCurrencyRow(currencyGrid, "비전지식", eCurrency.ArcaneKnowledge);

            panel.Add(currencyGrid);

            // ── 전직 파편 (통합) ──
            // 직업별이 아니라 단일 통합 파편이므로 버튼 하나만 둔다.
            var fragLabel = new Label("전직 파편 추가");
            fragLabel.AddToClassList("debug-section-label");
            panel.Add(fragLabel);

            var fragGrid = new VisualElement();
            fragGrid.AddToClassList("debug-currency-grid");
            AddUnifiedFragmentRow(fragGrid);
            panel.Add(fragGrid);

            // ── 토글 섹션 ──
            var toggleLabel = new Label("전투 옵션");
            toggleLabel.AddToClassList("debug-section-label");
            panel.Add(toggleLabel);

            // 플레이어 무적
            var playerRow = new VisualElement();
            playerRow.AddToClassList("debug-toggle-row");
            _lblPlayerToggle = new Label("플레이어 무적: OFF");
            _lblPlayerToggle.AddToClassList("debug-toggle-label");
            var btnPlayerToggle = new Button(() =>
            {
                PlayerInvincible = !PlayerInvincible;
                RefreshToggles();
            }) { text = "전환" };
            btnPlayerToggle.AddToClassList("debug-toggle-btn");
            playerRow.Add(_lblPlayerToggle);
            playerRow.Add(btnPlayerToggle);
            panel.Add(playerRow);

            // 몬스터 무적
            var monsterRow = new VisualElement();
            monsterRow.AddToClassList("debug-toggle-row");
            _lblMonsterToggle = new Label("몬스터 무적: OFF");
            _lblMonsterToggle.AddToClassList("debug-toggle-label");
            var btnMonsterToggle = new Button(() =>
            {
                MonsterInvincible = !MonsterInvincible;
                RefreshToggles();
            }) { text = "전환" };
            btnMonsterToggle.AddToClassList("debug-toggle-btn");
            monsterRow.Add(_lblMonsterToggle);
            monsterRow.Add(btnMonsterToggle);
            panel.Add(monsterRow);

            // 닫기 버튼
            var btnClose = new Button(Close) { text = "닫기" };
            btnClose.AddToClassList("debug-close-btn");
            panel.Add(btnClose);

            _overlay.Add(panel);
            targetLayer.Add(_overlay);
        }

        private static void AddCurrencyRow(VisualElement parent, string label, eCurrency currency)
        {
            var row = new VisualElement();
            row.AddToClassList("debug-currency-row");

            var lbl = new Label(label);
            lbl.AddToClassList("debug-currency-name");

            var btn = new Button(() =>
            {
                EconomyBridge.Add(currency, 1000);
                UITKUIManager.Instance?.ShowToast($"{label} +1000");
            }) { text = "+1000" };
            btn.AddToClassList("debug-currency-btn");

            row.Add(lbl);
            row.Add(btn);
            parent.Add(row);
        }

        private static void AddUnifiedFragmentRow(VisualElement parent)
        {
            var row = new VisualElement();
            row.AddToClassList("debug-currency-row");

            var lbl = new Label("전직 파편 (통합)");
            lbl.AddToClassList("debug-currency-name");

            var btn = new Button(() =>
            {
                // 매니저 경유 — UI 갱신 이벤트(OnStateChanged) 전파를 유지하기 위함.
                var mgr = KingdomArmyManager.Instance;
                if (mgr != null)
                {
                    mgr.AddFragments(50);
                }
                else
                {
                    // 매니저 미초기화 시 Wallet 에 직접 지급.
                    EconomyBridge.Add(eCurrency.ClassFragment, 50);
                }
                UITKUIManager.Instance?.ShowToast("전직 파편 +50");
            }) { text = "+50" };
            btn.AddToClassList("debug-currency-btn");

            row.Add(lbl);
            row.Add(btn);
            parent.Add(row);
        }
    }
}
