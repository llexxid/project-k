using UnityEngine;
using UnityEngine.UIElements;

namespace KingdomIdle.UIToolkit
{
    /// <summary>
    /// 육성 패널 컨트롤러.
    /// 모든 캐릭터 공용 강화 기능을 제공한다. (캐릭터별 탭 없음)
    /// </summary>
    public static class UITKDevelopmentPanelController
    {
        private enum SubMenu { Enhance }

        private static SubMenu _activeSubMenu;

        private static ScrollView _content;
        private static VisualElement _navBar;

        public static void Populate(VisualElement panelRoot)
        {
            if (panelRoot == null) return;

            _content = panelRoot.Q<ScrollView>("DevContent");
            _navBar = panelRoot.Q<VisualElement>("DevNavBar");

            if (_content == null || _navBar == null) return;

            _activeSubMenu = SubMenu.Enhance;

            BuildNavBar();
            Refresh();
        }

        // ── 하단 네비게이션 ──

        private static void BuildNavBar()
        {
            _navBar.Clear();

            var navItems = new (SubMenu menu, string label)[]
            {
                (SubMenu.Enhance, "강화"),
            };

            foreach (var (menu, label) in navItems)
            {
                var m = menu;
                var btn = new Button(() => { _activeSubMenu = m; Refresh(); UpdateNavStyles(); });
                btn.text = label;
                btn.AddToClassList("ka-nav-btn");
                _navBar.Add(btn);
            }
            UpdateNavStyles();
        }

        private static void UpdateNavStyles()
        {
            if (_navBar == null) return;
            int idx = 0;
            foreach (var child in _navBar.Children())
            {
                if (idx == (int)_activeSubMenu)
                    child.AddToClassList("ka-nav-btn-active");
                else
                    child.RemoveFromClassList("ka-nav-btn-active");
                idx++;
            }
        }

        // ── 콘텐츠 라우터 ──

        private static void Refresh()
        {
            _content.Clear();

            switch (_activeSubMenu)
            {
                case SubMenu.Enhance: BuildEnhanceView(); break;
            }
        }

        // ══════════════════════════════════════
        //  강화 (모든 캐릭터 공용)
        // ══════════════════════════════════════

        // 실제 구현된 강화 항목만 노출. 더미 항목은 IsStatImplemented 필터로 숨김.
        private static readonly StatEnhanceManager.EnhanceType[] _enhanceTypes = new[]
        {
            StatEnhanceManager.EnhanceType.Attack,
            StatEnhanceManager.EnhanceType.MaxHP,
        };

        private static void BuildEnhanceView()
        {
            _content.Add(MakeLabel("강화", "ka-section-title"));
            _content.Add(MakeLabel("모든 캐릭터에게 공용으로 적용됩니다.", "ka-placeholder-text"));

            var mgr = StatEnhanceManager.Instance;

            foreach (var type in _enhanceTypes)
            {
                // 더미 항목 숨김
                if (!StatEnhanceManager.IsStatImplemented(type)) continue;

                var t = type;
                string typeName = StatEnhanceManager.GetTypeName(t);
                int level = mgr != null ? mgr.GetLevel(t) : 0;
                string bonusText = mgr != null ? mgr.GetBonusText(t) : "";

                var row = new VisualElement();
                row.AddToClassList("ka-enhance-row");

                // 상단: 이름 + 레벨
                var header = new VisualElement();
                header.AddToClassList("ka-enhance-header");
                header.Add(MakeLabel(typeName, "ka-enhance-name"));
                header.Add(MakeLabel($"Lv. {level}", "ka-enhance-level"));
                row.Add(header);

                // 보너스 표시
                row.Add(MakeLabel($"효과: {bonusText}", "ka-enhance-bonus"));

                // 하단: 버튼
                int cost1 = mgr != null ? mgr.GetCost(t, 1) : 0;
                int cost10 = mgr != null ? mgr.GetCost(t, 10) : 0;

                var btnRow = new VisualElement();
                btnRow.AddToClassList("ka-enhance-btn-row");

                var btn1 = new Button(() => OnEnhanceClicked(t, 1));
                btn1.AddToClassList("ka-small-btn");
                btnRow.Add(btn1);

                // 버튼 내부: 텍스트 + 비용을 분리 표시
                var lbl1 = MakeLabel("강화 x1", null);
                var cost1Lbl = MakeLabel($"{FormatGold(cost1)} G", "ka-enhance-cost");
                btn1.Add(lbl1);
                btn1.Add(cost1Lbl);

                var btn10 = new Button(() => OnEnhanceClicked(t, 10));
                btn10.AddToClassList("ka-small-btn");
                btnRow.Add(btn10);

                var lbl10 = MakeLabel("강화 x10", null);
                var cost10Lbl = MakeLabel($"{FormatGold(cost10)} G", "ka-enhance-cost");
                btn10.Add(lbl10);
                btn10.Add(cost10Lbl);

                row.Add(btnRow);
                _content.Add(row);
            }
        }

        private static string FormatGold(int gold)
        {
            return gold.ToString("N0");
        }

        private static void OnEnhanceClicked(StatEnhanceManager.EnhanceType type, int count)
        {
            var mgr = StatEnhanceManager.Instance;
            if (mgr == null)
            {
                ShowToast("강화 시스템이 초기화되지 않았습니다.");
                return;
            }

            if (mgr.TryEnhance(type, count))
            {
                string name = StatEnhanceManager.GetTypeName(type);
                string bonus = mgr.GetBonusText(type);
                int lv = mgr.GetLevel(type);
                ShowToast($"{name} Lv.{lv} ({bonus}) 강화 완료!");
                Refresh();
            }
            else
            {
                ShowToast("골드가 부족합니다.");
            }
        }

        // ── 유틸 ──

        private static Label MakeLabel(string text, string className)
        {
            var lbl = new Label(text);
            if (!string.IsNullOrEmpty(className))
                lbl.AddToClassList(className);
            return lbl;
        }

        private static void ShowToast(string msg)
        {
            var uiMgr = UITKUIManager.Instance;
            if (uiMgr != null) uiMgr.ShowToast(msg);
        }
    }
}
