using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using KingdomIdle.KingdomArmy;

namespace KingdomIdle.UIToolkit
{
    /// <summary>
    /// 왕국군 패널 컨트롤러.
    /// 플로우: 왕국군 메뉴 → (종합/장비/스킬/전직) 서브 메뉴
    /// 전직 메뉴 → 전직 선택지 → 전직 상세 팝업
    /// </summary>
    public static class UITKKingdomArmyPanelController
    {
        private enum SubMenu { Character, Equipment, Skill, JobChange }

        private static int _activeMemberIndex;
        private static SubMenu _activeSubMenu;

        private static VisualElement _memberTabs;
        private static ScrollView _content;
        private static VisualElement _navBar;

        // 캐시된 플레이어 목록
        private static List<Player> _players;
        private static KingdomArmyManager _mgr;

        // 종합 탭 실시간 갱신용 캐시
        private static IVisualElementScheduledItem _charUpdateSchedule;
        private static Label _lblHp;
        private static VisualElement _charPortrait;
        private static VisualElement _charPortraitInner;

        /// <summary>초기 idle 스프라이트 기준 1px당 표시 크기 (고정 스케일)</summary>
        private static float _portraitScale;
        private const float PORTRAIT_SIZE = 120f;

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

        // ── 하단 네비게이션 (종합 / 장비 / 스킬 / 전직) ──

        private static void BuildNavBar()
        {
            _navBar.Clear();

            var navItems = new (SubMenu menu, string label)[]
            {
                (SubMenu.Character, "종합"),
                (SubMenu.Equipment, "장비"),
                (SubMenu.Skill, "스킬"),
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
            // 이전 실시간 갱신 스케줄 해제
            _charUpdateSchedule?.Pause();
            _charUpdateSchedule = null;
            _lblHp = null;
            _charPortrait = null;
            _charPortraitInner = null;

            _content.Clear();

            switch (_activeSubMenu)
            {
                case SubMenu.Character: BuildCharacterView(); break;
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

            _charPortrait = new VisualElement();
            _charPortrait.AddToClassList("ka-char-portrait");

            _charPortraitInner = new VisualElement();
            _charPortraitInner.AddToClassList("ka-char-portrait-inner");
            _charPortrait.Add(_charPortraitInner);

            // idle 스프라이트 기준으로 고정 스케일 산출
            var sr = player.GetComponent<SpriteRenderer>();
            if (sr != null && sr.sprite != null)
            {
                float idleH = sr.sprite.rect.height;
                _portraitScale = (idleH > 0f) ? PORTRAIT_SIZE / idleH : 1f;
                ApplyPortraitSprite(sr.sprite);
            }

            header.Add(_charPortrait);

            var infoCol = new VisualElement();
            infoCol.AddToClassList("ka-char-info");
            infoCol.Add(MakeLabel($"직업: {ps.JobName}", "ka-stat-line"));

            _lblHp = MakeLabel($"HP: {ps.HP} / {ps.MaxHP}", "ka-stat-line");
            infoCol.Add(_lblHp);

            infoCol.Add(MakeLabel($"공격력: {ps.Atk}", "ka-stat-line"));
            infoCol.Add(MakeLabel($"이동속도: {ps.MovSpeed}", "ka-stat-line"));
            infoCol.Add(MakeLabel($"공격속도: {ps.AtkSpeed:F2}초", "ka-stat-line"));
            header.Add(infoCol);

            _content.Add(header);

            // 장착 장비 표시
            _content.Add(MakeLabel("장착 장비", "ka-section-title"));
            var equipped = player.equipmentManager?.GetEquipped(eEquipmentSlot.Weapon);
            if (equipped != null)
                _content.Add(MakeLabel($"{equipped.baseData.equipmentName} +{equipped.enhancementLevel} (ATK +{equipped.GetFinalAtk()})", "ka-stat-line"));
            else
                _content.Add(MakeLabel("없음", "ka-placeholder-text"));

            // 실시간 갱신 스케줄 (200ms 간격으로 HP, 초상화 스프라이트 업데이트)
            _charUpdateSchedule = _content.schedule.Execute(() =>
            {
                var p = GetCurrentPlayer();
                if (p == null) return;

                // HP 갱신
                if (_lblHp != null)
                    _lblHp.text = $"HP: {p.playerStatus.HP} / {p.playerStatus.MaxHP}";

                // 초상화 스프라이트 실시간 갱신 (고정 스케일 유지)
                if (_charPortraitInner != null)
                {
                    var sprRend = p.GetComponent<SpriteRenderer>();
                    if (sprRend != null && sprRend.sprite != null)
                        ApplyPortraitSprite(sprRend.sprite);
                }
            }).Every(200);
        }

        // ══════════════════════════════════════
        //  장비 (인벤토리 내 장비만 표시)
        // ══════════════════════════════════════

        private static void BuildEquipmentView()
        {
            var player = GetCurrentPlayer();
            string jobName = player?.playerStatus?.JobName ?? "";
            var equipMgr = player?.equipmentManager;

            _content.Add(MakeLabel("장비", "ka-section-title"));

            // ── 현재 장착 슬롯 표시 ──
            _content.Add(MakeLabel("장착 중", "ka-subsection-title"));
            var equippedRow = new VisualElement();
            equippedRow.AddToClassList("ka-equip-grid");

            var equipped = equipMgr?.GetEquipped(eEquipmentSlot.Weapon);
            var equippedCard = new VisualElement();
            equippedCard.AddToClassList("ka-equip-slot");
            if (equipped != null)
            {
                equippedCard.AddToClassList("ka-equip-equipped");
                equippedCard.Add(MakeLabel("무기", "ka-equip-slot-name"));
                var iconVe = new VisualElement();
                iconVe.AddToClassList("ka-equip-icon");
                if (equipped.baseData.icon != null)
                    iconVe.style.backgroundImage = new StyleBackground(equipped.baseData.icon);
                equippedCard.Add(iconVe);
                string enhStr = equipped.enhancementLevel > 0 ? $" +{equipped.enhancementLevel}" : "";
                equippedCard.Add(MakeLabel($"{equipped.baseData.equipmentName}{enhStr}", "ka-equip-slot-name"));
                equippedCard.Add(MakeLabel($"ATK +{equipped.GetFinalAtk()}  HP +{equipped.GetFinalMaxHP()}", "ka-equip-slot-empty"));

                // 장착 해제 버튼
                var unequipBtn = new Button(() =>
                {
                    equipMgr.Unequip(eEquipmentSlot.Weapon);
                    ShowToast($"{equipped.baseData.equipmentName} 해제");
                    Refresh();
                });
                unequipBtn.text = "해제";
                unequipBtn.AddToClassList("ka-small-btn");
                equippedCard.Add(unequipBtn);
            }
            else
            {
                equippedCard.Add(MakeLabel("무기", "ka-equip-slot-name"));
                equippedCard.Add(MakeLabel("비어있음", "ka-equip-slot-empty"));
            }
            equippedRow.Add(equippedCard);
            _content.Add(equippedRow);

            // ── 인벤토리 내 장비 목록 ──
            _content.Add(MakeLabel("보유 장비", "ka-subsection-title"));

            if (equipMgr?.Inventory == null || equipMgr.Inventory.Items.Count == 0)
            {
                _content.Add(MakeLabel("보유한 장비가 없습니다.", "ka-placeholder-text"));
                return;
            }

            var grid = new VisualElement();
            grid.AddToClassList("ka-equip-grid");

            // 1차: 장착가능(해당 전직) > 장착불가  2차: 등급 내림차순  3차: 강화레벨 내림차순
            var sortedItems = equipMgr.Inventory.Items
                .OrderByDescending(i => i.baseData.IsAllowedForJob(jobName) ? 1 : 0)
                .ThenByDescending(i => i.baseData.rarity)
                .ThenByDescending(i => i.enhancementLevel)
                .ToList();

            foreach (var item in sortedItems)
            {
                BuildInventoryEquipCard(grid, item, jobName, equipped, equipMgr);
            }
            _content.Add(grid);
        }

        private static void BuildInventoryEquipCard(
            VisualElement grid, EquipmentInstance item, string jobName,
            EquipmentInstance equipped, EquipmentManager equipMgr)
        {
            bool isAllowed = item.baseData.IsAllowedForJob(jobName);
            bool isEquipped = equipped != null && equipped == item;

            var card = new Button(() => ShowEquipmentActionPopup(item, isEquipped, isAllowed, equipMgr));
            card.AddToClassList("ka-equip-slot");

            // 전직에 맞지 않는 장비는 어둡게
            if (!isAllowed)
                card.AddToClassList("ka-equip-dimmed");

            // 장착된 장비는 테두리 색 변경
            if (isEquipped)
                card.AddToClassList("ka-equip-equipped");

            // 등급 표시
            string rarityStr = item.baseData.rarity switch
            {
                eEquipmentRarity.Normal => "[일반]",
                eEquipmentRarity.Rare   => "[레어]",
                eEquipmentRarity.Epic   => "[에픽]",
                _ => ""
            };

            // 아이콘
            var iconVe = new VisualElement();
            iconVe.AddToClassList("ka-equip-icon");
            if (item.baseData.icon != null)
                iconVe.style.backgroundImage = new StyleBackground(item.baseData.icon);
            card.Add(iconVe);

            // 이름 + 강화
            string enhStr = item.enhancementLevel > 0 ? $" +{item.enhancementLevel}" : "";
            card.Add(MakeLabel($"{rarityStr} {item.baseData.equipmentName}{enhStr}", "ka-equip-slot-name"));

            // 스탯
            card.Add(MakeLabel($"ATK +{item.GetFinalAtk()}  HP +{item.GetFinalMaxHP()}", "ka-equip-slot-empty"));

            if (isEquipped)
                card.Add(MakeLabel("장착 중", "ka-frag-ready"));

            grid.Add(card);
        }

        // ── 장비 액션 팝업 (장착/강화 선택) ──

        private static void ShowEquipmentActionPopup(
            EquipmentInstance item, bool isEquipped, bool isAllowed, EquipmentManager equipMgr)
        {
            _content.Clear();

            // 뒤로가기
            var backBtn = new Button(() => { _activeSubMenu = SubMenu.Equipment; Refresh(); });
            backBtn.text = "← 장비 목록";
            backBtn.AddToClassList("ka-back-btn");
            _content.Add(backBtn);

            _content.Add(MakeLabel("장비 상세", "ka-section-title"));

            // 장비 정보
            var infoBox = new VisualElement();
            infoBox.AddToClassList("ka-job-detail-header");

            var iconVe = new VisualElement();
            iconVe.AddToClassList("ka-job-detail-img");
            if (item.baseData.icon != null)
                iconVe.style.backgroundImage = new StyleBackground(item.baseData.icon);
            infoBox.Add(iconVe);

            var infoCol = new VisualElement();
            infoCol.AddToClassList("ka-job-detail-info");

            string rarityStr = item.baseData.rarity switch
            {
                eEquipmentRarity.Normal => "일반",
                eEquipmentRarity.Rare   => "레어",
                eEquipmentRarity.Epic   => "에픽",
                _ => ""
            };

            string enhStr = item.enhancementLevel > 0 ? $" +{item.enhancementLevel}" : "";
            infoCol.Add(MakeLabel($"{item.baseData.equipmentName}{enhStr}", "ka-job-detail-name"));
            infoCol.Add(MakeLabel($"등급: {rarityStr}", "ka-stat-line"));
            infoCol.Add(MakeLabel($"공격력 보너스: +{item.GetFinalAtk()}", "ka-stat-line"));
            infoCol.Add(MakeLabel($"HP 보너스: +{item.GetFinalMaxHP()}", "ka-stat-line"));
            infoCol.Add(MakeLabel($"강화 레벨: {item.enhancementLevel} / {item.baseData.maxEnhancementLevel}", "ka-stat-line"));

            if (isEquipped)
                infoCol.Add(MakeLabel("현재 장착 중", "ka-frag-ready"));

            infoBox.Add(infoCol);
            _content.Add(infoBox);

            // ── 액션 버튼들 ──
            var btnRow = new VisualElement();
            btnRow.AddToClassList("ka-equip-action-row");

            // 장착 / 해제 버튼
            if (isEquipped)
            {
                var unequipBtn = new Button(() =>
                {
                    equipMgr.Unequip(item.baseData.slot);
                    ShowToast($"{item.baseData.equipmentName} 해제");
                    Refresh();
                });
                unequipBtn.text = "해제";
                unequipBtn.AddToClassList("ka-action-btn");
                btnRow.Add(unequipBtn);
            }
            else if (isAllowed)
            {
                var equipBtn = new Button(() =>
                {
                    equipMgr.Equip(item);
                    ShowToast($"{item.baseData.equipmentName} 장착!");
                    Refresh();
                });
                equipBtn.text = "장착";
                equipBtn.AddToClassList("ka-action-btn");
                btnRow.Add(equipBtn);
            }
            else
            {
                var disabledBtn = new Button();
                disabledBtn.text = "장착 불가 (직업 제한)";
                disabledBtn.AddToClassList("ka-action-btn");
                disabledBtn.AddToClassList("ka-action-btn-disabled");
                disabledBtn.SetEnabled(false);
                btnRow.Add(disabledBtn);
            }

            // 강화 버튼
            BuildEnhanceButton(btnRow, item, equipMgr);

            _content.Add(btnRow);

            // 강화 정보 표시
            BuildEnhanceInfo(item, equipMgr);
        }

        /// <summary>
        /// 강화 버튼을 생성한다. 왕국군 장비 탭과 인벤토리에서 공용 사용.
        /// </summary>
        private static void BuildEnhanceButton(VisualElement parent, EquipmentInstance item, EquipmentManager equipMgr)
        {
            if (item.IsMaxLevel())
            {
                var maxBtn = new Button();
                maxBtn.text = "강화 MAX";
                maxBtn.AddToClassList("ka-action-btn");
                maxBtn.AddToClassList("ka-action-btn-disabled");
                maxBtn.SetEnabled(false);
                parent.Add(maxBtn);
                return;
            }

            var enhBtn = new Button(() => TryEnhanceEquipment(item, equipMgr));
            enhBtn.text = "강화";
            enhBtn.AddToClassList("ka-action-btn");
            enhBtn.AddToClassList("ka-action-btn-enhance");
            parent.Add(enhBtn);
        }

        /// <summary>
        /// 강화 시도. 재료 부족 시 부족 수량을 토스트로 안내.
        /// </summary>
        private static void TryEnhanceEquipment(EquipmentInstance item, EquipmentManager equipMgr)
        {
            if (item.IsMaxLevel())
            {
                ShowToast("이미 최대 강화 레벨입니다.");
                return;
            }

            int needed = item.GetMaterialCount();
            int available = 0;
            if (equipMgr?.Inventory != null)
            {
                foreach (var inv in equipMgr.Inventory.Items)
                {
                    if (inv != item && inv.baseData == item.baseData)
                        available++;
                }
            }

            if (available < needed)
            {
                int shortage = needed - available;
                ShowToast($"동일 장비 부족! (보유: {available}/{needed}개, {shortage}개 부족)");
                return;
            }

            bool success = equipMgr.TryEnhance(item);
            if (success)
            {
                float nextRate = item.GetEnhanceSuccessRate() * 100f;
                ShowToast($"강화 성공! {item.baseData.equipmentName} +{item.enhancementLevel} (다음 확률: {nextRate:F0}%)");
            }
            else
            {
                ShowToast($"강화 실패... 재료 {needed}개가 소모되었습니다.");
            }

            // 현재 화면이 액션 팝업이면 다시 표시
            ShowEquipmentActionPopup(item,
                equipMgr.GetEquipped(item.baseData.slot) == item,
                item.baseData.IsAllowedForJob(GetCurrentPlayer()?.playerStatus?.JobName ?? ""),
                equipMgr);
        }

        /// <summary>강화 관련 정보 (필요 재료, 성공 확률 등)</summary>
        private static void BuildEnhanceInfo(EquipmentInstance item, EquipmentManager equipMgr)
        {
            if (item.IsMaxLevel()) return;

            _content.Add(MakeLabel("강화 정보", "ka-subsection-title"));

            int needed = item.GetMaterialCount();
            int available = 0;
            if (equipMgr?.Inventory != null)
            {
                foreach (var inv in equipMgr.Inventory.Items)
                {
                    if (inv != item && inv.baseData == item.baseData)
                        available++;
                }
            }

            float successRate = item.GetEnhanceSuccessRate() * 100f;

            var matLabel = MakeLabel($"필요 재료: {item.baseData.equipmentName} x{needed} (보유: {available}개)", "ka-stat-line");
            if (available < needed)
                matLabel.AddToClassList("ka-equip-dimmed-text");
            _content.Add(matLabel);

            _content.Add(MakeLabel($"성공 확률: {successRate:F0}%", "ka-stat-line"));

            // 강화 후 예상 스탯
            int nextAtk = item.baseData.bonusAtk + (int)(item.baseData.bonusAtk * item.baseData.atkGrowthPerLevel * (item.enhancementLevel + 1));
            int nextHP = item.baseData.bonusMaxHP + (int)(item.baseData.bonusMaxHP * item.baseData.hpGrowthPerLevel * (item.enhancementLevel + 1));
            _content.Add(MakeLabel($"강화 시 예상: ATK +{item.GetFinalAtk()} → +{nextAtk}  HP +{item.GetFinalMaxHP()} → +{nextHP}", "ka-stat-line"));
        }

        // ══════════════════════════════════════
        //  왕국군 스킬 (더미)
        // ══════════════════════════════════════

        private static void BuildSkillView()
        {
            _content.Add(MakeLabel("스킬", "ka-section-title"));

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

        /// <summary>
        /// 초상화 내부 요소에 스프라이트를 적용한다.
        /// idle 때 산출한 _portraitScale을 그대로 사용하므로
        /// 공격 모션처럼 폭이 넓은 프레임이 와도 캐릭터 크기가 일정하다.
        /// 컨테이너(.ka-char-portrait)의 overflow:hidden이 넘치는 부분을 클리핑한다.
        /// </summary>
        private static void ApplyPortraitSprite(Sprite sprite)
        {
            if (_charPortraitInner == null || sprite == null) return;

            float w = sprite.rect.width  * _portraitScale;
            float h = sprite.rect.height * _portraitScale;

            _charPortraitInner.style.width  = w;
            _charPortraitInner.style.height = h;

            // 컨테이너 중앙 정렬
            _charPortraitInner.style.left = (PORTRAIT_SIZE - w) / 2f;
            _charPortraitInner.style.top  = (PORTRAIT_SIZE - h) / 2f;

            _charPortraitInner.style.backgroundImage = new StyleBackground(sprite);
        }

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
