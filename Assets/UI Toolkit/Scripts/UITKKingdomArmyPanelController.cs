using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using KingdomIdle.KingdomArmy;

namespace KingdomIdle.UIToolkit
{
    /// <summary>
    /// 왕국군 패널 컨트롤러.
    /// 플로우: 왕국군 메뉴 → (강화/장비/스킬/전직) 서브 메뉴
    /// 전직 메뉴 → 전직 선택지 → 전직 상세 팝업
    /// </summary>
    public static class UITKKingdomArmyPanelController
    {
        private enum SubMenu { Character, Enhance, Equipment, Skill, JobChange }

        private static int _activeMemberIndex;
        private static SubMenu _activeSubMenu;

        private static VisualElement _memberTabs;
        private static ScrollView _content;
        private static VisualElement _navBar;

        // 캐시된 플레이어 목록
        private static List<Player> _players;
        private static KingdomArmyManager _mgr;

        // ── 진입점 ──

        public static void Populate(VisualElement panelRoot)
        {
            if (panelRoot == null) return;

            _memberTabs = panelRoot.Q<VisualElement>("ArmyMemberTabs");
            _content = panelRoot.Q<ScrollView>("ArmyContent");
            _navBar = panelRoot.Q<VisualElement>("ArmyNavBar");

            if (_memberTabs == null || _content == null || _navBar == null) return;

            _mgr = KingdomArmyManager.Instance;
            if (_mgr == null)
            {
                _content.Add(new Label("KingdomArmyManager를 씬에 배치해주세요."));
                return;
            }

            _players = _mgr.GetPlayers();
            _activeMemberIndex = 0;
            _activeSubMenu = SubMenu.Character;

            BuildMemberTabs();
            BuildNavBar();
            Refresh();
        }

        // ── 상단 멤버 탭 (왕국군1 / 왕국군2 / 왕국군3) ──

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

        // ── 하단 네비게이션 (강화 / 장비 / 왕국군스킬 / 전직) ──

        private static void BuildNavBar()
        {
            _navBar.Clear();

            var navItems = new (SubMenu menu, string label)[]
            {
                (SubMenu.Character, "캐릭터"),
                (SubMenu.Enhance, "강화"),
                (SubMenu.Equipment, "장비"),
                (SubMenu.Skill, "왕국군 스킬"),
                (SubMenu.JobChange, "전직"),
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
                // SubMenu enum 순서와 네비 버튼 순서 일치
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
                case SubMenu.Character: BuildCharacterView(); break;
                case SubMenu.Enhance:   BuildEnhanceView();   break;
                case SubMenu.Equipment: BuildEquipmentView(); break;
                case SubMenu.Skill:     BuildSkillView();     break;
                case SubMenu.JobChange: BuildJobChangeView(); break;
            }
        }

        // ══════════════════════════════════════
        //  캐릭터 정보 (왕국군 캐릭터창)
        // ══════════════════════════════════════

        private static void BuildCharacterView()
        {
            var player = GetCurrentPlayer();
            if (player == null)
            {
                _content.Add(new Label("플레이어 정보를 불러올 수 없습니다."));
                return;
            }

            var ps = player.playerStatus;

            // 초상화 + 직업명
            var header = new VisualElement();
            header.AddToClassList("ka-char-header");

            var portrait = new VisualElement();
            portrait.AddToClassList("ka-char-portrait");

            // 스프라이트가 있으면 표시
            var sr = player.GetComponent<SpriteRenderer>();
            if (sr != null && sr.sprite != null)
                portrait.style.backgroundImage = new StyleBackground(sr.sprite);

            header.Add(portrait);

            var infoCol = new VisualElement();
            infoCol.AddToClassList("ka-char-info");
            infoCol.Add(MakeLabel($"직업: {ps.JobName}", "ka-stat-line"));
            infoCol.Add(MakeLabel($"HP: {ps.HP} / {ps.MaxHP}", "ka-stat-line"));
            infoCol.Add(MakeLabel($"공격력: {ps.Atk}", "ka-stat-line"));
            infoCol.Add(MakeLabel($"이동속도: {ps.MovSpeed}", "ka-stat-line"));
            infoCol.Add(MakeLabel($"공격속도: {ps.AtkSpeed:F2}초", "ka-stat-line"));
            header.Add(infoCol);

            _content.Add(header);

            // 장착 장비 (더미)
            _content.Add(MakeLabel("장착 장비", "ka-section-title"));
            _content.Add(MakeLabel("장비 시스템 미구현", "ka-placeholder-text"));
        }

        // ══════════════════════════════════════
        //  강화 (더미)
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

        // ══════════════════════════════════════
        //  장비 (더미)
        // ══════════════════════════════════════

        private static void BuildEquipmentView()
        {
            _content.Add(MakeLabel("장비", "ka-section-title"));
            _content.Add(MakeLabel("장비 시스템 미구현", "ka-placeholder-text"));

            // 더미 장비 슬롯
            var grid = new VisualElement();
            grid.AddToClassList("ka-equip-grid");

            var slots = new string[] { "무기", "방어구", "투구", "장갑", "신발", "악세서리" };
            foreach (var slot in slots)
            {
                var card = new VisualElement();
                card.AddToClassList("ka-equip-slot");
                card.Add(MakeLabel(slot, "ka-equip-slot-name"));
                card.Add(MakeLabel("비어있음", "ka-equip-slot-empty"));
                grid.Add(card);
            }
            _content.Add(grid);
        }

        // ══════════════════════════════════════
        //  왕국군 스킬 (더미)
        // ══════════════════════════════════════

        private static void BuildSkillView()
        {
            _content.Add(MakeLabel("왕국군 스킬", "ka-section-title"));

            var player = GetCurrentPlayer();
            if (player == null || player.playerStatus == null)
            {
                _content.Add(MakeLabel("플레이어 정보 없음", "ka-placeholder-text"));
                return;
            }

            // 현재 직업의 스킬 표시
            var changeJob = player.GetComponent<ChangeJob>();
            if (changeJob == null)
            {
                _content.Add(MakeLabel("스킬 정보 없음", "ka-placeholder-text"));
                return;
            }

            // JobDatabase에서 현재 직업의 스킬 목록 가져오기
            var jobDB = _mgr.JobDB;
            if (jobDB == null) return;

            var jobData = jobDB.GetJob(player.playerStatus.JobName);
            if (jobData == null || jobData.skills == null)
            {
                _content.Add(MakeLabel("직업 스킬이 없습니다.", "ka-placeholder-text"));
                return;
            }

            foreach (var skill in jobData.skills)
            {
                if (skill == null) continue;

                var row = new VisualElement();
                row.AddToClassList("ka-skill-row");

                var info = new VisualElement();
                info.AddToClassList("ka-skill-info");
                info.Add(MakeLabel(skill.skillName, "ka-skill-name"));
                info.Add(MakeLabel($"데미지: {skill.damage}  쿨타임: {skill.cooldown}초", "ka-skill-detail"));
                row.Add(info);

                var btnRow = new VisualElement();
                btnRow.AddToClassList("ka-enhance-btn-row");

                var btn1 = new Button(() => ShowToast("스킬 강화 미구현"));
                btn1.text = "강화 x1";
                btn1.AddToClassList("ka-small-btn");
                btnRow.Add(btn1);

                var btn10 = new Button(() => ShowToast("스킬 강화 미구현"));
                btn10.text = "강화 x10";
                btn10.AddToClassList("ka-small-btn");
                btnRow.Add(btn10);

                row.Add(btnRow);
                _content.Add(row);
            }
        }

        // ══════════════════════════════════════
        //  전직 메뉴 (왕국군전직메뉴)
        // ══════════════════════════════════════

        private static void BuildJobChangeView()
        {
            _content.Add(MakeLabel("전직", "ka-section-title"));

            var jobDB = _mgr.JobDB;
            if (jobDB == null || jobDB.Count == 0)
            {
                _content.Add(MakeLabel("직업 데이터가 없습니다.", "ka-placeholder-text"));
                return;
            }

            var grid = new VisualElement();
            grid.AddToClassList("ka-job-grid");

            for (int i = 0; i < jobDB.Count; i++)
            {
                var job = jobDB.GetJob(i);
                if (job == null) continue;

                // 창병(Spearman)은 전직 목록에서 제외
                if (job.jobName == "Spearman") continue;

                int idx = i;
                var card = new Button(() => ShowJobDetail(job));
                card.AddToClassList("ka-job-card");

                // 전직 이미지
                var imgVe = new VisualElement();
                imgVe.AddToClassList("ka-job-card-img");
                if (job.jobSprite != null)
                    imgVe.style.backgroundImage = new StyleBackground(job.jobSprite);
                card.Add(imgVe);

                // 직업명
                card.Add(MakeLabel(job.jobName, "ka-job-card-name"));

                // 파편 현황
                int owned = _mgr.GetFragments(job.jobName);
                int cost = _mgr.GetFragmentCost(job.jobName);
                var fragLbl = MakeLabel($"파편: {owned}/{cost}", "ka-job-card-frag");
                if (owned >= cost)
                    fragLbl.AddToClassList("ka-frag-ready");
                card.Add(fragLbl);

                grid.Add(card);
            }

            _content.Add(grid);
        }

        // ══════════════════════════════════════
        //  전직 상세 팝업 (왕국군전직상세메뉴)
        // ══════════════════════════════════════

        private static void ShowJobDetail(JobData job)
        {
            _content.Clear();

            // 뒤로가기 버튼
            var backBtn = new Button(() => { _activeSubMenu = SubMenu.JobChange; Refresh(); });
            backBtn.text = "← 전직 목록";
            backBtn.AddToClassList("ka-back-btn");
            _content.Add(backBtn);

            _content.Add(MakeLabel("전직 상세", "ka-section-title"));

            // 직업 이미지 + 이름
            var header = new VisualElement();
            header.AddToClassList("ka-job-detail-header");

            var imgVe = new VisualElement();
            imgVe.AddToClassList("ka-job-detail-img");
            if (job.jobSprite != null)
                imgVe.style.backgroundImage = new StyleBackground(job.jobSprite);
            header.Add(imgVe);

            var nameCol = new VisualElement();
            nameCol.AddToClassList("ka-job-detail-info");
            nameCol.Add(MakeLabel(job.jobName, "ka-job-detail-name"));

            // 스탯 표시
            nameCol.Add(MakeLabel($"HP: {job.maxHP}", "ka-stat-line"));
            nameCol.Add(MakeLabel($"공격력: {job.atk}", "ka-stat-line"));
            nameCol.Add(MakeLabel($"이동속도: {job.movSpeed}", "ka-stat-line"));
            nameCol.Add(MakeLabel($"공격속도: {job.atkSpeed:F2}초", "ka-stat-line"));

            header.Add(nameCol);
            _content.Add(header);

            // 스킬 목록
            if (job.skills != null && job.skills.Count > 0)
            {
                _content.Add(MakeLabel("직업 스킬", "ka-subsection-title"));
                foreach (var skill in job.skills)
                {
                    if (skill == null) continue;
                    _content.Add(MakeLabel($"  {skill.skillName} — 데미지: {skill.damage}, 쿨타임: {skill.cooldown}초", "ka-stat-line"));
                }
            }

            // 전직 파편 현황
            _content.Add(MakeLabel("전직 파편", "ka-subsection-title"));

            int owned = _mgr.GetFragments(job.jobName);
            int cost = _mgr.GetFragmentCost(job.jobName);
            var fragBar = new VisualElement();
            fragBar.AddToClassList("ka-frag-bar");

            var fragLbl = MakeLabel($"{job.jobName} 전직 파편: {owned} / {cost}", "ka-frag-status");
            if (owned >= cost) fragLbl.AddToClassList("ka-frag-ready");
            fragBar.Add(fragLbl);
            _content.Add(fragBar);

            // 전직하기 버튼
            var player = GetCurrentPlayer();
            bool canChange = _mgr.CanChangeJob(job.jobName) && player != null;
            bool alreadyThis = player != null && player.playerStatus?.JobName == job.jobName;

            var changeBtn = new Button(() => OnJobChangeClicked(player, job));
            if (alreadyThis)
            {
                changeBtn.text = "현재 직업";
                changeBtn.AddToClassList("ka-change-btn");
                changeBtn.AddToClassList("ka-change-btn-disabled");
                changeBtn.SetEnabled(false);
            }
            else if (canChange)
            {
                changeBtn.text = "전직하기";
                changeBtn.AddToClassList("ka-change-btn");
            }
            else
            {
                changeBtn.text = $"전직하기 (파편 부족: {owned}/{cost})";
                changeBtn.AddToClassList("ka-change-btn");
                changeBtn.AddToClassList("ka-change-btn-disabled");
                changeBtn.SetEnabled(false);
            }

            _content.Add(changeBtn);
        }

        private static void OnJobChangeClicked(Player player, JobData job)
        {
            if (player == null || _mgr == null) return;

            bool success = _mgr.TryChangeJob(player, job.jobName);
            if (success)
            {
                ShowToast($"{job.jobName}(으)로 전직 완료!");
                // 탭 라벨 갱신
                BuildMemberTabs();
                // 상세 화면 갱신
                ShowJobDetail(job);
            }
            else
            {
                ShowToast("전직에 실패했습니다.");
            }
        }

        // ── 유틸 ──

        private static Player GetCurrentPlayer()
        {
            if (_players == null || _activeMemberIndex >= _players.Count) return null;
            return _players[_activeMemberIndex];
        }

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
