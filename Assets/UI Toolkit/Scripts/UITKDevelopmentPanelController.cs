using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using KingdomIdle.KingdomArmy;

namespace KingdomIdle.UIToolkit
{
    /// <summary>
    /// 육성 패널 컨트롤러.
    /// 왕국군 멤버별 강화 기능을 제공한다.
    /// </summary>
    public static class UITKDevelopmentPanelController
    {
        private enum SubMenu { Enhance }

        private static int _activeMemberIndex;
        private static SubMenu _activeSubMenu;

        private static VisualElement _memberTabs;
        private static ScrollView _content;
        private static VisualElement _navBar;

        private static List<Player> _players;
        private static KingdomArmyManager _mgr;

        public static void Populate(VisualElement panelRoot)
        {
            if (panelRoot == null) return;

            _memberTabs = panelRoot.Q<VisualElement>("DevMemberTabs");
            _content = panelRoot.Q<ScrollView>("DevContent");
            _navBar = panelRoot.Q<VisualElement>("DevNavBar");

            if (_memberTabs == null || _content == null || _navBar == null) return;

            _mgr = KingdomArmyManager.Instance;
            if (_mgr == null)
            {
                _content.Add(new Label("KingdomArmyManager를 씬에 배치해주세요."));
                return;
            }

            _players = _mgr.GetPlayers();
            _activeMemberIndex = 0;
            _activeSubMenu = SubMenu.Enhance;

            BuildMemberTabs();
            BuildNavBar();
            Refresh();
        }

        // ── 상단 멤버 탭 ──

        private static void BuildMemberTabs()
        {
            _memberTabs.Clear();
            int count = Mathf.Max(_players.Count, 3);
            for (int i = 0; i < count; i++)
            {
                int idx = i;
                string label = $"왕국군{i + 1}";
                if (i < _players.Count && _players[i] != null)
                {
                    string job = _players[i].playerStatus?.JobName;
                    if (!string.IsNullOrEmpty(job))
                        label = $"왕국군{i + 1} ({job})";
                }

                var btn = new Button(() => { _activeMemberIndex = idx; Refresh(); UpdateMemberTabStyles(); });
                btn.text = label;
                btn.AddToClassList("ka-member-tab");
                _memberTabs.Add(btn);
            }
            UpdateMemberTabStyles();
        }

        private static void UpdateMemberTabStyles()
        {
            if (_memberTabs == null) return;
            for (int i = 0; i < _memberTabs.childCount; i++)
            {
                if (i == _activeMemberIndex)
                    _memberTabs[i].AddToClassList("ka-member-tab-active");
                else
                    _memberTabs[i].RemoveFromClassList("ka-member-tab-active");
            }
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
        //  강화 (왕국군에서 이동)
        // ══════════════════════════════════════

        private static void BuildEnhanceView()
        {
            _content.Add(MakeLabel("강화", "ka-section-title"));

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
