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

        private static void BuildEnhanceView()
        {
            _content.Add(MakeLabel("강화", "ka-section-title"));
            _content.Add(MakeLabel("모든 캐릭터에게 공용으로 적용됩니다.", "ka-placeholder-text"));

            var items = new string[] { "공격력 강화", "크리티컬 강화", "치명타 피해 강화", "HP 강화" };
            foreach (var item in items)
            {
                var row = new VisualElement();
                row.AddToClassList("ka-enhance-row");
                row.Add(MakeLabel(item, "ka-enhance-name"));

                var btnRow = new VisualElement();
                btnRow.AddToClassList("ka-enhance-btn-row");

                var btn1 = new Button(() => ShowToast("강화 시스템 미구현"));
                btn1.text = "강화 x1";
                btn1.AddToClassList("ka-small-btn");
                btnRow.Add(btn1);

                var btn10 = new Button(() => ShowToast("강화 시스템 미구현"));
                btn10.text = "강화 x10";
                btn10.AddToClassList("ka-small-btn");
                btnRow.Add(btn10);

                row.Add(btnRow);
                _content.Add(row);
            }
        }

        // ── 유틸 ──

        private static Label MakeLabel(string text, string className)
        {
            var lbl = new Label(text);
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
