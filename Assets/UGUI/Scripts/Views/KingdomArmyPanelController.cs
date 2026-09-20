using KingdomIdle.Balance;
using Scripts.Core;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using KingdomIdle.KingdomArmy;

namespace KingdomIdle.UGUI
{
    /// <summary>
    /// 왕국군 패널 컨트롤러 (프리팹 기반).
    /// 각 서브 화면(종합/장비/스킬/전직)과 드릴다운(장비 상세/전직 상세)은
    /// 콘텐츠 프리팹(Panel_KA*) 을 스크롤 Content 에 Instantiate 하고 View 로 값을 채운다.
    /// 반복 셀/행은 기존 Item_* 프리팹(EquipCell/JobCard/SkillRow/NavTabButton/ActionButton/StatCompareRow)을 재사용한다.
    /// 코드로 UI 구조를 생성하지 않는다(UguiRuntimeFactory 참조 제거 완료).
    ///
    /// 플로우: 왕국군 메뉴 → (종합/장비/스킬/전직) 서브 메뉴
    /// 전직 메뉴 → 전직 선택지 → 전직 상세 팝업
    /// </summary>
    public static class KingdomArmyPanelController
    {
        private enum SubMenu { Character, Equipment, Skill, JobChange }

        private static readonly Color MemberTabActive = new Color(60f / 255f, 140f / 255f, 80f / 255f, 0.60f);
        private static readonly Color StatLineColor = new Color(1f, 1f, 1f, 0.70f);
        private static readonly Color PlaceholderColor = new Color(1f, 1f, 1f, 0.40f);
        private static readonly Color FragLockedColor = UguiTheme.WarnRed;

        private static int _activeMemberIndex;
        private static SubMenu _activeSubMenu;
        private static int _pendingMemberIndex = -1;   // 다음 Populate에서 선택할 멤버 (파티 HUD 초상화 탭 라우팅)
        private static SubMenu? _pendingSubMenu;
        public static void SetPendingEquipmentTab() => _pendingSubMenu = SubMenu.Equipment;
        public static void SetPendingJobChangeTab() => _pendingSubMenu = SubMenu.JobChange;

        /// <summary>패널을 열기 전에 호출하면 해당 멤버가 선택된 상태로 열린다 (1회성).</summary>
        public static void SetPendingMemberIndex(int index) => _pendingMemberIndex = index;

        private static KingdomArmyPanelView _view;
        private static System.Type _contentPage;
        private static readonly List<NavTabButtonView> _memberTabButtons = new();
        private static readonly List<NavTabButtonView> _navButtons = new();
        private static readonly List<SubMenu> _navMenus = new();

        // 캐시된 플레이어 목록
        private static List<Player> _players;
        private static KingdomArmyManager _mgr;

        // 종합 탭 실시간 갱신용 캐시 (FrameTick 타이머)
        private static bool _charTickSubscribed;
        private static float _charTickTimer;
        private const float CharTickInterval = 0.2f;
        private static KACharacterSheetView _charSheet;   // 종합 시트(실시간 HP 갱신용)
        private static bool _statDetailOpen;
        private static PlayerStatus _shownStatus;
        private static bool _statsDirty;
        private static PlayerStatus.StatBreakdown _shownAttack, _shownHealth;
        private static Image _charPortraitInner;

        /// <summary>초기 idle 스프라이트 기준 1px당 표시 크기 (고정 스케일)</summary>
        private static float _portraitScale;
        private const float PORTRAIT_SIZE = 120f;

        private static UIViewCatalog Cat =>
            UIManager.Instance != null ? UIManager.Instance.Catalog : null;

        // ── 진입점 ──

        /// <summary>겹쳐 열린 왕국군 화면을 닫은 뒤 원래 인스턴스의 정적 바인딩을 복구한다.</summary>
        internal static void Restore(KingdomArmyPanelView view)
        {
            if (_view != view) Populate(view);
        }

        public static void Populate(KingdomArmyPanelView view)
        {
            // 예약 인덱스는 어떤 경로로 빠져나가든 여기서 소비한다 — 아래 early return 에 걸려
            // 살아남으면 나중에 엉뚱한 열기(탭 버튼 등)에서 뒤늦게 발화한다.
            int pendingMember = _pendingMemberIndex;
            _pendingMemberIndex = -1;
            var pendingMenu = _pendingSubMenu;
            _pendingSubMenu = null;

            if (view == null) return;

            _view = view;
            _contentPage = null;
            NumberNotationBinding.Bind(view, Refresh);
            if (_view.memberTabs == null || _view.content == null || _view.navBar == null) return;

            view.OnClosed = () =>
            {
                UnsubscribeCharTick();
                if (_view == view)
                {
                    _view = null;
                    _memberTabButtons.Clear();
                    _navButtons.Clear();
                    _charSheet = null;
                    _charPortraitInner = null;
                }
            };

            _mgr = KingdomArmyManager.Instance;
            if (_mgr == null)
            {
                ShowMessageInContent("KingdomArmyManager를 씬에 배치해주세요.");
                return;
            }

            _players = _mgr.GetPlayers();
            _activeMemberIndex = pendingMember >= 0 && _players != null && _players.Count > 0
                ? Mathf.Clamp(pendingMember, 0, _players.Count - 1)
                : 0;
            _activeSubMenu = pendingMenu ?? SubMenu.Character;

            BuildMemberTabs();
            BuildNavBar();
            Refresh();
        }

        // ── 상단 멤버 탭 (왕국군1 / 왕국군2 / 왕국군3) ──

        private static void BuildMemberTabs()
        {
            if (_view == null) return;
            ClearChildren(_view.memberTabs);
            _memberTabButtons.Clear();

            var prefab = GetNavPrefab();
            if (prefab == null) return;

            int count = Mathf.Max(_players.Count, 3);
            for (int i = 0; i < count; i++)
            {
                int idx = i;
                string label = $"왕국군{i + 1}";
                if (i < _players.Count && _players[i] != null)
                {
                    string job = _players[i].playerStatus?.JobName;
                    if (!string.IsNullOrEmpty(job))
                        label = $"왕국군{i + 1}\n{JobData.GetDisplayName(job)}";
                }

                var go = Object.Instantiate(prefab, _view.memberTabs, false);
                var tab = go.GetComponent<NavTabButtonView>();
                if (tab == null) continue;

                tab.SetLabel(label);
                // 멤버 탭은 해당 캐릭터의 직업 스프라이트를 아이콘으로 — 누가 누구인지 즉시 구분
                Sprite memberIcon = null;
                if (i < _players.Count && _players[i] != null && _mgr.JobDB != null && _players[i].playerStatus != null)
                {
                    var jobData = _mgr.JobDB.GetJob(_players[i].playerStatus.JobName);
                    if (jobData != null) memberIcon = jobData.Portrait;
                }
                tab.SetIcon(memberIcon);
                tab.Button.onClick.AddListener(() =>
                {
                    _activeMemberIndex = idx;
                    Refresh();
                    UpdateMemberTabStyles();
                });
                _memberTabButtons.Add(tab);
            }
            UpdateMemberTabStyles();
        }

        private static void UpdateMemberTabStyles()
        {
            for (int i = 0; i < _memberTabButtons.Count; i++)
                _memberTabButtons[i].SetSelected(i == _activeMemberIndex, MemberTabActive);
        }

        // ── 하단 네비게이션 (종합 / 장비 / 스킬 / 전직) ──

        private static void BuildNavBar()
        {
            if (_view == null) return;
            ClearChildren(_view.navBar);
            _navButtons.Clear();
            _navMenus.Clear();

            var prefab = GetNavPrefab();
            if (prefab == null) return;

            var cat = Cat;
            // 스킬 탭 제거 — 스킬은 종합(스탯) 탭 하단에 표시된다.
            var navItems = new (SubMenu menu, string label, Sprite icon)[]
            {
                (SubMenu.Character, "종합", cat != null ? cat.iconUser : null),
                (SubMenu.Equipment, "장비", cat != null ? cat.iconSword : null),
                (SubMenu.JobChange, "전직", cat != null ? cat.iconStar : null),
            };

            foreach (var (menu, label, icon) in navItems)
            {
                var m = menu;
                var go = Object.Instantiate(prefab, _view.navBar, false);
                var tab = go.GetComponent<NavTabButtonView>();
                if (tab == null) continue;

                tab.SetLabel(label);
                tab.SetIcon(icon);
                tab.Button.onClick.AddListener(() =>
                {
                    _activeSubMenu = m;
                    Refresh();
                    UpdateNavStyles();
                });
                _navButtons.Add(tab);
                _navMenus.Add(m);
            }
            UpdateNavStyles();
        }

        private static void UpdateNavStyles()
        {
            for (int i = 0; i < _navButtons.Count; i++)
                _navButtons[i].SetSelected(_navMenus[i] == _activeSubMenu, UguiTheme.AccentBlue);
        }

        // ── 콘텐츠 라우터 ──

        private static void Refresh()
        {
            if (_view == null || _view.content == null) return;

            // 이전 실시간 갱신 해제 (콘텐츠는 각 Build 가 InstantiateContent 에서 교체)
            UnsubscribeCharTick();
            _charSheet = null;
            _charPortraitInner = null;

            switch (_activeSubMenu)
            {
                case SubMenu.Character: BuildCharacterView(); break;
                case SubMenu.Equipment: BuildEquipmentView(); break;
                case SubMenu.Skill: BuildSkillView(); break;
                case SubMenu.JobChange: BuildJobChangeView(); break;
            }
        }

        // ══════════════════════════════════════
        //  캐릭터 정보 (왕국군 캐릭터창) + 200ms 실시간 갱신
        // ══════════════════════════════════════

        private static void BuildCharacterView()
        {
            var player = GetCurrentPlayer();
            if (player == null)
            {
                ShowMessageInContent("플레이어 정보를 불러올 수 없습니다.");
                return;
            }

            var sheet = InstantiateContent<KACharacterSheetView>(Cat != null ? Cat.panelKACharacterSheet : null);
            if (sheet == null) return;
            _charSheet = sheet;

            var ps = player.playerStatus;
            _shownStatus = ps;
            _shownStatus.OnStatsChanged += MarkStatsDirty;
            _statsDirty = false;

            // 직업명 + 칩 값
            if (sheet.jobLabel != null) sheet.jobLabel.text = JobData.GetDisplayName(ps.JobName);
            if (sheet.atkValueLabel != null) sheet.atkValueLabel.text = NumberNotation.Format(ps.Atk);
            if (sheet.moveValueLabel != null) sheet.moveValueLabel.text = ps.MovSpeed.ToString();
            UpdateHpBar(player, sheet);

            // 상세 스탯 방정식 (탭 가능한 항 + 설명 팝업)
            _shownAttack = ps.AtkBreakdown(); _shownHealth = ps.MaxHPBreakdown();
            BuildStatEquation(sheet, sheet.atkEqRow, _shownAttack, atk: true);
            BuildStatEquation(sheet, sheet.hpEqRow, _shownHealth, atk: false);

            // 스탯 블록 = 버튼 → 상세 롤다운 토글
            _statDetailOpen = false;
            if (sheet.detailRoot != null) sheet.detailRoot.SetActive(false);
            if (sheet.expandArrow != null) sheet.expandArrow.localRotation = Quaternion.identity;
            if (sheet.statsButton != null)
            {
                sheet.statsButton.onClick.RemoveAllListeners();
                sheet.statsButton.onClick.AddListener(ToggleStatDetail);
            }

            // 스킬 (스탯 탭 하단)
            if (sheet.skillsRoot != null)
            {
                ClearChildren(sheet.skillsRoot);
                var infos = SkillSystem.GetJobSkillInfo(ps.JobName);
                if (infos != null)
                    foreach (var si in infos)
                        InstantiateSkillRow(sheet.skillsRoot, si.Name, si.Description, si.IsPassive);
            }

            // 장착 장비 라벨
            var equipped = player.PlayerEquipmentManager?.GetSlotEquipment(eEquipmentSlot.Weapon);
            if (sheet.equippedLabel != null)
            {
                if (equipped != null)
                {
                    sheet.equippedLabel.text = $"{equipped.baseData.DisplayName} +{equipped.enhancementLevel} (ATK +{NumberNotation.Format(equipped.GetFinalAtk())})";
                    sheet.equippedLabel.color = StatLineColor;
                }
                else
                {
                    sheet.equippedLabel.text = "없음";
                    sheet.equippedLabel.color = PlaceholderColor;
                }
            }

            // 초상화 (idle 스프라이트 기준 고정 스케일)
            _charPortraitInner = sheet.portraitInner;
            var portrait = _mgr.JobDB != null ? _mgr.JobDB.GetJob(ps.JobName)?.Portrait : null;
            if (portrait != null)
            {
                float idleH = portrait.rect.height;
                _portraitScale = (idleH > 0f) ? PORTRAIT_SIZE / idleH : 1f;
                ApplyPortraitSprite(portrait);
            }

            // 실시간 갱신 (200ms 간격으로 HP 바, 초상화 스프라이트 업데이트)
            SubscribeCharTick();
        }

        /// <summary>HP 바 채움 + 값 + 색(비율). 피격 시 낮아지고 붉어진다.</summary>
        private static void UpdateHpBar(Player player, KACharacterSheetView sheet)
        {
            if (player == null || sheet == null || player.playerStatus == null) return;
            long maxHp = player.playerStatus.MaxHP;
            float ratio = Mathf.Clamp01(player.HPRatio);
            long curHp = player.playerStatus.HP;
            if (sheet.hpFill != null)
            {
                ShieldHealthBar.Set(sheet.hpFill, ratio, player.IsDead ? 0 : player.ShieldHP, maxHp);
                // 초록(가득) → 노랑 → 빨강(위험)
                Color full = new Color(0.42f, 0.85f, 0.35f, 1f);
                Color low = new Color(0.88f, 0.25f, 0.2f, 1f);
                sheet.hpFill.color = Color.Lerp(low, full, Mathf.SmoothStep(0f, 1f, ratio));
            }
            if (sheet.hpValueLabel != null)
                sheet.hpValueLabel.text = $"{NumberNotation.Format(curHp)} / {NumberNotation.Format(maxHp)}" +
                    (player.ShieldHP > 0 ? $"  <color=#F5FAFF>+{NumberNotation.Format(player.ShieldHP)}</color>" : "");
        }

        /// <summary>스탯 블록 롤다운 토글.</summary>
        private static void ToggleStatDetail()
        {
            if (_charSheet == null || _charSheet.detailRoot == null) return;
            _statDetailOpen = !_statDetailOpen;
            _charSheet.detailRoot.SetActive(_statDetailOpen);
            if (_charSheet.expandArrow != null)
                _charSheet.expandArrow.localRotation = Quaternion.Euler(0f, 0f, _statDetailOpen ? 180f : 0f);
            // 스크롤 콘텐츠 재배치 (롤다운 펼침/접힘 반영)
            var contentRt = _view != null ? _view.content : null;
            if (contentRt != null) UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(contentRt);
        }

        /// <summary>방정식 컨테이너에 탭 가능한 항 + 연산자 라벨을 채운다: (base + equip + passive) × mult × (1+rate) = final.</summary>
        private static void BuildStatEquation(KACharacterSheetView sheet, RectTransform container, PlayerStatus.StatBreakdown b, bool atk)
        {
            if (container == null) return;
            ClearChildren(container);
            string kind = atk ? "공격력" : "체력";

            AddOperator(container, "(");
            AddTerm(sheet, container, NumberNotation.Format(b.Base), $"전직 기본 {kind}");
            if (b.Equip != 0) { AddOperator(container, "+"); AddTerm(sheet, container, NumberNotation.Format(b.Equip), $"장비 {kind}"); }
            if (b.Passive != 0) { AddOperator(container, "+"); AddTerm(sheet, container, NumberNotation.Format(b.Passive), $"패시브 {kind}"); }
            AddOperator(container, ")");
            if (b.HasBuff) { AddOperator(container, "×"); AddTerm(sheet, container, (1m+b.AuraRate+b.AccountRate+b.ReincarnationRate).ToString("0.###"), $"가산 보너스: 오라 {b.AuraRate:P1} + 계정 {b.AccountRate:P1} + 환생 {b.ReincarnationRate:P1}"); }
            if (b.HasEnhance) { AddOperator(container, "×"); AddTerm(sheet, container, b.GrowthMultiplier.ToString("0.###"), $"골드 강화: 레벨당 1.025배 누적"); }
            AddOperator(container, "=");
            AddTerm(sheet, container, NumberNotation.Format(b.Final), $"최종 {kind}");
        }

        private static void AddTerm(KACharacterSheetView sheet, RectTransform container, string text, string explanation)
        {
            var prefab = Cat != null ? Cat.itemStatTerm : null;
            if (prefab == null) return;
            var go = Object.Instantiate(prefab, container, false);
            var term = go.GetComponent<StatTermView>();
            if (term != null) term.Set(text, explanation, (expl, _) => ShowTermPopup(sheet, expl));
        }

        private static void AddOperator(RectTransform container, string op)
        {
            var go = new GameObject("Op", typeof(RectTransform));
            go.layer = 5;
            var rt = (RectTransform)go.transform;
            rt.SetParent(container, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            if (UIManager.Instance != null && UIManager.Instance.Catalog != null)
                t.font = UIManager.Instance.Catalog.defaultFont;
            t.text = op; t.fontSize = 26f; t.color = UguiTheme.TextSecondary;
            t.alignment = TextAlignmentOptions.Center; t.raycastTarget = false;
            t.fontStyle = FontStyles.Bold;
            var le = go.AddComponent<LayoutElement>();
            le.preferredWidth = op == "(" || op == ")" ? 14f : 26f; le.preferredHeight = 44f;
        }

        private static void ShowTermPopup(KACharacterSheetView sheet, string explanation)
        {
            if (sheet == null || sheet.termPopup == null) return;
            sheet.termPopup.SetActive(true);
            if (sheet.termPopupLabel != null) sheet.termPopupLabel.text = explanation;
        }

        private static void SubscribeCharTick()
        {
            if (_charTickSubscribed || UIManager.Instance == null) return;
            UIManager.Instance.FrameTick += OnCharTick;
            _charTickSubscribed = true;
            _charTickTimer = 0f;
        }

        private static void UnsubscribeCharTick()
        {
            if (_shownStatus != null) _shownStatus.OnStatsChanged -= MarkStatsDirty;
            _shownStatus = null;
            if (!_charTickSubscribed) return;
            if (UIManager.Instance != null)
                UIManager.Instance.FrameTick -= OnCharTick;
            _charTickSubscribed = false;
        }

        private static void OnCharTick()
        {
            _charTickTimer += Time.unscaledDeltaTime;
            if (_charTickTimer < CharTickInterval) return;
            _charTickTimer = 0f;

            var p = GetCurrentPlayer();
            if (p == null) return;

            // HP 바 실시간 갱신 (피격 시 채움/색 반영)
            if (_charSheet != null)
            {
                UpdateHpBar(p, _charSheet);
                if (_statsDirty)
                {
                    _statsDirty = false;
                    var ps = p.playerStatus;
                    _charSheet.atkValueLabel.text = NumberNotation.Format(ps.Atk);
                    _charSheet.moveValueLabel.text = ps.MovSpeed.ToString();
                    _charSheet.jobLabel.text = JobData.GetDisplayName(ps.JobName);
                    var attack = ps.AtkBreakdown(); var health = ps.MaxHPBreakdown();
                    if (!_shownAttack.Equals(attack)) { _shownAttack = attack; BuildStatEquation(_charSheet, _charSheet.atkEqRow, attack, true); }
                    if (!_shownHealth.Equals(health)) { _shownHealth = health; BuildStatEquation(_charSheet, _charSheet.hpEqRow, health, false); }
                }
            }

            // Job portraits are stable; promotion rebuilds this view through the army event.
        }

        private static void MarkStatsDirty() => _statsDirty = true;

        // ══════════════════════════════════════
        //  장비 (인벤토리 내 장비만 표시)
        // ══════════════════════════════════════

        private static void BuildEquipmentView()
        {
            var equip = InstantiateContent<KAEquipmentView>(Cat != null ? Cat.panelKAEquipment : null);
            if (equip == null) return;

            var player = GetCurrentPlayer();
            string jobName = player?.playerStatus?.JobName ?? "";
            var equipMgr = player?.PlayerEquipmentManager;
            EquipmentManager equipmentManager = EquipmentManager.Instance;
            var toolbar = equip.GetComponentInChildren<EquipmentToolbarView>(true);
            toolbar?.Bind(data => data.IsAllowedForJob(jobName), Refresh);

            var equipped = equipMgr?.GetSlotEquipment(eEquipmentSlot.Weapon);

            // ── 장착 중 카드 ──
            if (equip.equippedFrame != null) equip.equippedFrame.gameObject.SetActive(equipped != null);
            if (equip.equippedSlotLabel != null) equip.equippedSlotLabel.text = "무기";

            if (equipped != null)
            {
                if (equip.equippedIconWrap != null) equip.equippedIconWrap.gameObject.SetActive(true);
                SetIconSprite(equip.equippedIcon, equipped.baseData.icon);

                string enhStr0 = equipped.enhancementLevel > 0 ? $" +{equipped.enhancementLevel}" : "";
                if (equip.equippedNameLabel != null)
                {
                    equip.equippedNameLabel.text = $"{equipped.baseData.DisplayName}{enhStr0}";
                    equip.equippedNameLabel.color = new Color(1f, 1f, 1f, 0.85f);
                }
                if (equip.equippedStatLabel != null)
                {
                    equip.equippedStatLabel.gameObject.SetActive(true);
                    equip.equippedStatLabel.text = $"ATK +{NumberNotation.Format(equipped.GetFinalAtk())}  HP +{NumberNotation.Format(equipped.GetFinalMaxHP())}";
                }
                if (equip.unequipButton != null)
                {
                    equip.unequipButton.gameObject.SetActive(true);
                    var capturedEquipped = equipped;
                    var capturedMgr = equipMgr;
                    equip.unequipButton.onClick.RemoveAllListeners();
                    equip.unequipButton.onClick.AddListener(() =>
                    {
                        capturedMgr.Unequip(eEquipmentSlot.Weapon);
                        ShowToast($"{capturedEquipped.baseData.DisplayName} 해제");
                        Refresh();
                    });
                }
            }
            else
            {
                if (equip.equippedIconWrap != null) equip.equippedIconWrap.gameObject.SetActive(false);
                if (equip.equippedNameLabel != null)
                {
                    equip.equippedNameLabel.text = "비어있음";
                    equip.equippedNameLabel.color = new Color(1f, 1f, 1f, 0.30f);
                }
                if (equip.equippedStatLabel != null) equip.equippedStatLabel.gameObject.SetActive(false);
                if (equip.unequipButton != null) equip.unequipButton.gameObject.SetActive(false);
            }

            // ── 보유 장비 목록 ──
            ClearChildren(equip.inventoryGrid);

            if (equipmentManager?.Inventory == null || (equipmentManager.Inventory.Items.Count == 0 && KingdomIdle.Balance.LocalProgression.State.PendingEquipment.Count == 0 && KingdomIdle.Balance.LocalProgression.State.LegacyEquipment.Count == 0))
            {
                if (equip.emptyLabel != null) equip.emptyLabel.gameObject.SetActive(true);
                return;
            }
            if (equip.emptyLabel != null)
            {
                equip.emptyLabel.text = "필터에 맞는 장비가 없습니다.";
                equip.emptyLabel.gameObject.SetActive(toolbar != null && !toolbar.HasMatches);
            }

            BuildStoredEquipmentCells(equip.inventoryGrid, Refresh, toolbar != null ? toolbar.Accepts : null);
            // 1차: 장착가능(해당 전직) > 장착불가  2차: 등급 내림차순  3차: 강화레벨 내림차순
            var sortedItems = toolbar != null ? toolbar.Sort(equipmentManager.Inventory.Items) : equipmentManager.Inventory.Items;

            foreach (var item in sortedItems)
                BuildInventoryEquipCard(equip.inventoryGrid, item, jobName, equipped, equipMgr);
        }

        internal static void BuildStoredEquipmentCells(RectTransform grid, System.Action refresh, System.Func<int, bool> accepts = null)
        {
            var equipmentManager = EquipmentManager.Instance;
            if (equipmentManager == null) return;
            foreach (var stored in LocalProgression.State.LegacyEquipment)
            {
                if (accepts != null && !accepts(stored.Code)) continue;
                var data = equipmentManager.GetData(stored.Code); if (data == null) continue;
                int code = stored.Code, level = stored.Level;
                string enhancement = level > 0 ? $" +{level}" : "";
                InstantiateEquipCell(grid, data.icon, $"{data.DisplayName}{enhancement} ×{stored.Count}", UguiTheme.AccentGold,
                    "1개 받기", UguiTheme.RarityColor(data.rarity), false, false, "추가 보관", () => {
                        if (equipmentManager.ClaimLegacy(code, level)) refresh();
                        else UIManager.Instance?.ShowToast("가방이 가득 찼습니다. 위의 일괄 분해로 추가 보관 장비도 정리할 수 있습니다.");
                    });
            }
            foreach (var group in LocalProgression.State.PendingEquipment.GroupBy(x => (x.Code, x.Level)))
            {
                var item = group.First(); var data = equipmentManager.GetData(item.Code); if (data == null) continue;
                if (accepts != null && !accepts(item.Code)) continue;
                string id = item.Id;
                InstantiateEquipCell(grid, data.icon, $"{data.DisplayName} ×{group.Count()}", UguiTheme.AccentGold,
                    "1개 받기", UguiTheme.RarityColor(data.rarity), false, false, "추가 보관", () => {
                        if (equipmentManager.ClaimPending(id)) refresh();
                        else UIManager.Instance?.ShowToast("가방이 가득 찼습니다. 위의 일괄 분해로 추가 보관 장비도 정리할 수 있습니다.");
                    });
            }
        }

        private static void BuildInventoryEquipCard(
            RectTransform grid, EquipmentInstance item, string jobName,
            EquipmentInstance equipped, PlayerEquipmentManager equipMgr)
        {
            bool isAllowed = item.baseData.IsAllowedForJob(jobName);
            bool isEquipped = equipped != null && equipped == item;

            var capturedItem = item;
            var capturedMgr = equipMgr;
            bool capturedEquipped = isEquipped;
            bool capturedAllowed = isAllowed;
            System.Action onClick = () => ShowEquipmentActionPopup(capturedItem, capturedEquipped, capturedAllowed, capturedMgr);

            string enhStr = item.enhancementLevel > 0 ? $" +{item.enhancementLevel}" : "";
            string name = $"{item.baseData.DisplayName}{enhStr}";
            string sub = $"ATK +{NumberNotation.Format(item.GetFinalAtk())}  HP +{NumberNotation.Format(item.GetFinalMaxHP())}";
            var rarityColor = UguiTheme.RarityColor(item.baseData.rarity);

            InstantiateEquipCell(grid, item.baseData.icon, name, rarityColor, sub, rarityColor,
                isEquipped, !isAllowed, isEquipped ? "장착 중" : item.IsLocked ? "잠금" : null, onClick);
        }

        /// <summary>공용 장비 셀 생성 (왕국군/인벤토리). Item_EquipCell 프리팹 인스턴스화.</summary>
        internal static void InstantiateEquipCell(
            RectTransform grid, Sprite icon, string name, Color nameColor, string sub, Color rarityColor,
            bool equipped, bool dimmed, string state, System.Action onClick)
        {
            var cat = Cat;
            if (cat == null || cat.itemEquipCell == null) return;

            VirtualEquipmentGrid.Add(grid, cat.itemEquipCell, cell => {
                cell.Set(icon, name, nameColor, sub, rarityColor, equipped, dimmed, state);
                cell.OnClick(onClick);
            });
        }

        // ── 장비 액션 팝업 (장착/강화 선택) ──

        private static void ShowEquipmentActionPopup(
            EquipmentInstance item, bool isEquipped, bool isAllowed, PlayerEquipmentManager equipMgr)
        {
            if (_view == null) return;
            UnsubscribeCharTick();
            _charSheet = null;
            _charPortraitInner = null;

            var detail = InstantiateContent<KAEquipDetailView>(Cat != null ? Cat.panelKAEquipDetail : null);
            if (detail == null) return;

            // 뒤로가기
            if (detail.backButton != null)
            {
                detail.backButton.onClick.RemoveAllListeners();
                detail.backButton.onClick.AddListener(() => { _activeSubMenu = SubMenu.Equipment; Refresh(); });
            }

            // 장비 정보
            SetIconSprite(detail.icon, item.baseData.icon);

            string rarityStr = item.baseData.rarity switch
            {
                eEquipmentRarity.Normal => "일반",
                eEquipmentRarity.Rare => "레어",
                eEquipmentRarity.Epic => "에픽",
                _ => ""
            };
            string enhStr = item.enhancementLevel > 0 ? $" +{item.enhancementLevel}" : "";

            if (detail.nameLabel != null) detail.nameLabel.text = $"{item.baseData.DisplayName}{enhStr}";
            if (detail.rarityLabel != null) detail.rarityLabel.text = $"등급: {rarityStr}";
            if (detail.atkLabel != null) detail.atkLabel.text = $"공격력 보너스: +{NumberNotation.Format(item.GetFinalAtk())}";
            if (detail.hpLabel != null) detail.hpLabel.text = $"HP 보너스: +{NumberNotation.Format(item.GetFinalMaxHP())}";
            if (detail.enhanceLabel != null) detail.enhanceLabel.text = $"강화 레벨: {item.enhancementLevel} / {item.baseData.maxEnhancementLevel}";
            if (detail.equippedNowLabel != null) detail.equippedNowLabel.gameObject.SetActive(isEquipped);

            // ── 액션 버튼들 (Item_ActionButton) ──
            ClearChildren(detail.actionRow);

            // 장착 / 해제 버튼
            if (isEquipped)
            {
                var capturedItem = item;
                var capturedMgr = equipMgr;
                AddActionButton(detail.actionRow, "해제", UguiTheme.BtnCancel, true, () =>
                {
                    capturedMgr.Unequip(capturedItem.baseData.slot);
                    ShowToast($"{capturedItem.baseData.DisplayName} 해제");
                    Refresh();
                });
            }
            else if (isAllowed)
            {
                var capturedItem = item;
                var capturedMgr = equipMgr;
                AddActionButton(detail.actionRow, "장착", UguiTheme.BtnConfirm, true, () =>
                {
                    if (!EquipmentManager.Instance.TryEquip(capturedMgr.PlayerIndex,capturedItem)) { ShowToast("다른 병사가 장착 중이거나 직업에 맞지 않습니다."); return; }
                    ShowToast($"{capturedItem.baseData.DisplayName} 장착!");
                    Refresh();
                });
            }
            else
            {
                AddActionButton(detail.actionRow, "장착 불가 (직업 제한)", UguiTheme.DisabledGrey, false, null);
            }

            // 강화 버튼
            if (item.IsMaxLevel())
            {
                AddActionButton(detail.actionRow, "강화 MAX", UguiTheme.DisabledGrey, false, null);
            }
            else
            {
                var capturedItem = item;
                var capturedMgr = equipMgr;
                AddActionButton(detail.actionRow, "강화", UguiTheme.BtnSpend, true,
                    () => TryEnhanceEquipment(capturedItem, capturedMgr));
            }

            // 강화 정보 표시
            BuildEnhanceInfo(detail, item);
            AddActionButton(detail.actionRow,item.IsLocked?"잠금 해제":"분해 방지 잠금",UguiTheme.BtnCancel,true,()=> { EquipmentManager.Instance.SetLocked(item,!item.IsLocked); ShowEquipmentActionPopup(item,isEquipped,isAllowed,equipMgr); });
            AddActionButton(detail.actionRow,"분해 · 강화석 획득",UguiTheme.BtnSpend,!item.IsLocked&&!item.IsEquipped,
                () => EquipmentActionDialog.Dismantle(EquipmentEconomy.Preview(_ => true, item.instanceId), Refresh));
            // Keep the prefab's existing layout component; a separate child owns the vertical actions.
            var buttons = new System.Collections.Generic.List<Transform>();
            foreach (Transform child in detail.actionRow) if(child.gameObject.activeSelf) buttons.Add(child);
            var stack = new GameObject("EquipmentActions",typeof(RectTransform),typeof(UnityEngine.UI.VerticalLayoutGroup),typeof(UnityEngine.UI.LayoutElement));
            stack.transform.SetParent(detail.actionRow,false);
            var vertical=stack.GetComponent<UnityEngine.UI.VerticalLayoutGroup>();
            vertical.spacing=8;vertical.childControlWidth=true;vertical.childControlHeight=true;vertical.childForceExpandWidth=true;vertical.childForceExpandHeight=false;
            foreach(var button in buttons)
            {
                button.SetParent(stack.transform,false);
                var touch=button.GetComponent<LayoutElement>() ?? button.gameObject.AddComponent<LayoutElement>();touch.minHeight=128;touch.preferredHeight=128;
                var label=button.GetComponentInChildren<TMP_Text>();if(label!=null)label.fontSize=30;
            }
            var stackLayout=stack.GetComponent<UnityEngine.UI.LayoutElement>();stackLayout.minHeight=536;stackLayout.preferredHeight=536;stackLayout.flexibleWidth=1;
            var rowLayout=detail.actionRow.GetComponent<UnityEngine.UI.LayoutElement>() ?? detail.actionRow.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();rowLayout.minHeight=536;rowLayout.preferredHeight=536;

        }

        /// <summary>강화 시도. 재료 부족 시 부족 수량을 토스트로 안내.</summary>
        private static void TryEnhanceEquipment(EquipmentInstance item, PlayerEquipmentManager equipMgr)
        {
            if (item.IsMaxLevel())
            {
                ShowToast("이미 최대 강화 레벨입니다.");
                return;
            }

            long needed = EquipmentEconomy.EnhanceCost(item);
            long available = KingdomIdle.Balance.LocalProgression.Balance(eCurrency.EquipmentStone);

            if (available < needed)
            {
                long shortage = needed - available;
                ShowToast($"강화석 부족 (보유 {available} / 필요 {needed}, {shortage}개 부족)");
                return;
            }

            var result = EquipmentManager.Instance.TryEnhanceDetailed(item);
            if (result == EquipmentManager.EnhancementResult.Success)
            {
                ShowToast($"강화 성공! {item.baseData.DisplayName} +{item.enhancementLevel}");
            }
            else if (result == EquipmentManager.EnhancementResult.ChanceFailed)
            {
                ShowToast($"강화 실패. 다시 확인해 주세요.");
            }
            else ShowToast("강화하지 못했습니다. 강화석과 저장 상태를 확인해 주세요.");

            // 현재 화면이 액션 팝업이면 다시 표시
            ShowEquipmentActionPopup(item,
                equipMgr.GetSlotEquipment(item.baseData.slot) == item,
                item.baseData.IsAllowedForJob(GetCurrentPlayer()?.playerStatus?.JobName ?? ""),
                equipMgr);
        }

        /// <summary>강화 관련 정보 (필요 재료, 성공 확률 등)를 상세 뷰에 채운다.</summary>
        private static void BuildEnhanceInfo(KAEquipDetailView detail, EquipmentInstance item)
        {
            if (detail.enhanceInfoGroup == null) return;

            if (item.IsMaxLevel())
            {
                detail.enhanceInfoGroup.gameObject.SetActive(false);
                return;
            }
            detail.enhanceInfoGroup.gameObject.SetActive(true);

            long needed = EquipmentEconomy.EnhanceCost(item);
            long available = KingdomIdle.Balance.LocalProgression.Balance(eCurrency.EquipmentStone);

            float successRate = item.GetEnhanceSuccessRate() * 100f;

            if (detail.materialLabel != null)
            {
                detail.materialLabel.text = $"필요 강화석: {needed}개 (보유: {NumberNotation.Format(available)}개)";
                detail.materialLabel.color = available < needed ? FragLockedColor : StatLineColor;
            }
            if (detail.successRateLabel != null)
                detail.successRateLabel.text = $"성공 확률: {successRate:F0}%";

            // 강화 후 예상 스탯
            int nextAtk = item.GetAttackAtLevel(item.enhancementLevel + 1);
            int nextHP = item.GetFinalMaxHP();
            if (detail.expectedLabel != null)
                detail.expectedLabel.text = $"강화 시 예상: ATK +{NumberNotation.Format(item.GetFinalAtk())} → +{NumberNotation.Format(nextAtk)}  HP +{NumberNotation.Format(item.GetFinalMaxHP())} → +{NumberNotation.Format(nextHP)}";
        }

        // ══════════════════════════════════════
        //  왕국군 스킬
        // ══════════════════════════════════════

        private static void BuildSkillView()
        {
            var skill = InstantiateContent<KASkillView>(Cat != null ? Cat.panelKASkill : null);
            if (skill == null) return;

            var player = GetCurrentPlayer();
            if (player == null || player.playerStatus == null)
            {
                ShowSkillPlaceholder(skill, "플레이어 정보 없음");
                return;
            }

            var changeJob = player.GetComponent<ChangeJob>();
            if (changeJob == null)
            {
                ShowSkillPlaceholder(skill, "스킬 정보 없음");
                return;
            }

            string jobName = player.playerStatus?.JobName ?? "";
            var skillInfos = SkillSystem.GetJobSkillInfo(jobName);

            if (skillInfos == null || skillInfos.Length == 0)
            {
                ShowSkillPlaceholder(skill, "직업 스킬이 없습니다.");
                return;
            }

            if (skill.placeholder != null) skill.placeholder.gameObject.SetActive(false);
            ClearChildren(skill.skillList);
            foreach (var si in skillInfos)
                InstantiateSkillRow(skill.skillList, si.Name, si.Description, si.IsPassive);
        }

        private static void ShowSkillPlaceholder(KASkillView skill, string msg)
        {
            ClearChildren(skill.skillList);
            if (skill.placeholder == null) return;
            skill.placeholder.gameObject.SetActive(true);
            skill.placeholder.text = msg;
        }

        /// <summary>공용 스킬 행 생성. 왕국군 스킬/전직 상세 공용. Item_SkillRow 프리팹 인스턴스화.</summary>
        internal static void InstantiateSkillRow(RectTransform parent, string name, string detail, bool isPassive)
        {
            var cat = Cat;
            if (cat == null || cat.itemSkillRow == null) return;

            var go = Object.Instantiate(cat.itemSkillRow, parent, false);
            var view = go.GetComponent<SkillRowView>();
            if (view == null) { Object.Destroy(go); return; }
            view.Set(name, detail, isPassive);
        }

        // ══════════════════════════════════════
        //  전직 메뉴 (왕국군전직메뉴)
        // ══════════════════════════════════════

        private static void BuildJobChangeView()
        {
            var jc = InstantiateContent<KAJobChangeView>(Cat != null ? Cat.panelKAJobChange : null);
            if (jc == null) return;

            var jobDB = _mgr.JobDB;
            if (jobDB == null || jobDB.Count == 0)
            {
                if (jc.placeholder != null) jc.placeholder.gameObject.SetActive(true);
                if (jc.contentGroup != null) jc.contentGroup.gameObject.SetActive(false);
                return;
            }
            if (jc.placeholder != null) jc.placeholder.gameObject.SetActive(false);
            if (jc.contentGroup != null) jc.contentGroup.gameObject.SetActive(true);

            // 통합 전직 파편 보유량 배너 — 어떤 직업이든 파편 40개로 전직 가능.
            int ownedFrags = _mgr.GetFragments();
            int fragCost = 40;

            if (jc.bannerValue != null)
            {
                jc.bannerValue.text = $"{NumberNotation.Format(ownedFrags)}";
                jc.bannerValue.color = ownedFrags >= fragCost ? UguiTheme.SuccessGreenBright : UguiTheme.AccentGoldStrong;
            }
            if (jc.bannerHint != null) jc.bannerHint.text = "1차 40 · 2차 120 · 해금 후 무료";

            var player = GetCurrentPlayer();
            string currentJob = player?.playerStatus?.JobName ?? "";

            ClearChildren(jc.basicGrid);
            ClearChildren(jc.eliteGrid);

            // A column is one branch. Keep the unavailable branch visible without exposing retired jobs.
            foreach (string name in new[] { "Knight", "Mage", "Archer" })
            {
                var basic = jobDB.GetJob(name); var elite = jobDB.GetJob("Elite_" + name);
                if (basic != null) BuildJobCard(jc.basicGrid, basic, player, currentJob, false);
                if (elite != null) BuildJobCard(jc.eliteGrid, elite, player, currentJob, true);
            }
        }

        /// <summary>
        /// 전직 카드를 생성한다 (Item_JobCard). 상태에 따라 시각적으로 구분:
        /// - 현재 직업: "현재" 배지 + 강조 테두리
        /// - 보유 직업: "보유" 배지 + 무료 재전직 안내
        /// - 잠김 직업: 어두운 색 + 잠금 표시
        /// </summary>
        private static void BuildJobCard(RectTransform grid, JobData job, Player player, string currentJob, bool isElite)
        {
            if (!JobData.IsAvailable(job.jobName))
            {
                if (Cat == null || Cat.itemJobCard == null) return;
                var placeholder = Object.Instantiate(Cat.itemJobCard, grid, false).GetComponent<JobCardView>();
                placeholder?.SetComingSoon(_mgr.JobDB.GetJob("Spearman"), isElite);
                return;
            }
            // 상태 판정
            bool isCurrent = currentJob == job.jobName;
            bool isUnlocked = player != null && _mgr.IsAlreadyUnlocked(player, job.jobName);
            string prereq = KingdomArmyManager.GetPrerequisiteJob(job.jobName);
            bool prereqMet = prereq == null || (player != null && _mgr.HasCompletedPromotion(player, prereq));

            // 통합 전직 파편 — 모든 직업이 동일한 파편 풀을 공유한다.
            int owned = _mgr.GetFragments();
            int cost = _mgr.GetFragmentCost(job.jobName);
            bool fragReady = owned >= cost;

            // 카드 배경 색 (상태 변형)
            Color bg = isCurrent ? UguiTheme.RusticSurface : UguiTheme.RusticSurfaceDark;

            // 상태 테두리 색
            Color? frameColor = null;
            if (isCurrent) frameColor = UguiTheme.BronzeLight;
            else if (isUnlocked) frameColor = UguiTheme.Bronze;
            else if (isElite) frameColor = new Color(.42f,.37f,.48f,.7f);

            // 배지
            string badgeText; Color badgeColor;
            if (isCurrent) { badgeText = "현재"; badgeColor = UguiTheme.AccentGoldStrong; }
            else if (isUnlocked) { badgeText = "보유"; badgeColor = new Color(120f / 255f, 180f / 255f, 1f, 1f); }
            else if (!prereqMet) { badgeText = "잠김"; badgeColor = UguiTheme.WarnRed; }
            else if (fragReady) { badgeText = "전직가능"; badgeColor = UguiTheme.SuccessGreenBright; }
            else { badgeText = isElite ? "2차" : "1차"; badgeColor = new Color(1f, 1f, 1f, 0.55f); }

            string statText = job.jobName switch { "Knight" => "묵직한 근접 공격", "Elite_Knight" => "광역 베기 · 보호막", "Mage" => "원거리 마법", "Elite_Mage" => "파동 · 밀쳐내기", _ => "" };
            string fragText; Color fragColor;
            if (isUnlocked) { fragText = "무료 재전직"; fragColor = UguiTheme.SuccessGreenBright; }
            else { fragText = $"전직 파편 {NumberNotation.Format(owned)}/{NumberNotation.Format(cost)}"; fragColor = fragReady ? UguiTheme.SuccessGreenBright : UguiTheme.AccentGoldStrong; }
            string prereqText = !prereqMet ? $"{JobData.GetDisplayName(prereq)} 전직 필요" : null;

            var capturedJob = job;
            var cat = Cat;
            if (cat == null || cat.itemJobCard == null) return;

            var go = Object.Instantiate(cat.itemJobCard, grid, false);
            var view = go.GetComponent<JobCardView>();
            if (view == null) { Object.Destroy(go); return; }

            view.Set(job, bg, frameColor, badgeText, badgeColor, statText, fragText, fragColor, prereqText);
            view.OnClick(() => ShowJobDetail(capturedJob));
        }

        // ══════════════════════════════════════
        //  전직 상세 팝업 (왕국군전직상세메뉴)
        // ══════════════════════════════════════

        private static void ShowJobDetail(JobData job)
        {
            if (_view == null || job == null || !JobData.IsAvailable(job.jobName)) return;
            UnsubscribeCharTick();
            _charSheet = null;
            _charPortraitInner = null;

            var detail = InstantiateContent<KAJobDetailView>(Cat != null ? Cat.panelKAJobDetail : null);
            if (detail == null) return;

            var player = GetCurrentPlayer();
            var ps = player?.playerStatus;
            bool isCurrent = ps != null && ps.JobName == job.jobName;
            bool isUnlocked = player != null && _mgr.IsAlreadyUnlocked(player, job.jobName);
            string prereq = KingdomArmyManager.GetPrerequisiteJob(job.jobName);
            bool prereqMet = prereq == null || _mgr.HasCompletedPromotion(player, prereq);

            // 뒤로가기 버튼
            if (detail.backButton != null)
            {
                detail.backButton.onClick.RemoveAllListeners();
                detail.backButton.onClick.AddListener(() => { _activeSubMenu = SubMenu.JobChange; Refresh(); });
            }

            // ── 직업 헤더 (이미지 + 이름 + 상태 배지) ──
            SetIconSprite(detail.image, job.Portrait);
            if (detail.jobNameLabel != null) detail.jobNameLabel.text = job.DisplayName;

            // 상태 배지
            string stateText;
            Color stateColor;
            if (isCurrent) { stateText = "현재"; stateColor = UguiTheme.AccentGoldStrong; }
            else if (isUnlocked) { stateText = "보유"; stateColor = new Color(120f / 255f, 180f / 255f, 1f, 1f); }
            else { stateText = prereq != null ? "2차" : "1차"; stateColor = new Color(1f, 1f, 1f, 0.55f); }
            if (detail.stateBadge != null) { detail.stateBadge.text = stateText; detail.stateBadge.color = stateColor; }

            // 직업 설명 — JobData에 description 필드가 없으므로 간단한 분류 텍스트로 대체
            string roleText = job.jobName.Contains("Knight") ? "근접 탱커 / 근거리 딜러"
                              : job.jobName.Contains("Archer") ? "원거리 물리 딜러"
                              : job.jobName.Contains("Mage") ? "원거리 마법 딜러"
                              : "근접 전사";
            if (detail.roleLabel != null) detail.roleLabel.text = roleText;

            // ── 스탯 비교 (현재 직업 vs 신규 직업) ──
            BuildStatCompareTable(detail.compareTable, player, job);

            // ── 직업 스킬 ──
            var jobSkills = SkillSystem.GetJobSkillInfo(job.jobName);
            if (jobSkills != null && jobSkills.Length > 0)
            {
                if (detail.skillGroup != null) detail.skillGroup.gameObject.SetActive(true);
                ClearChildren(detail.skillList);
                foreach (var si in jobSkills)
                    InstantiateSkillRow(detail.skillList, si.Name, si.Description, si.IsPassive);
            }
            else
            {
                if (detail.skillGroup != null) detail.skillGroup.gameObject.SetActive(false);
            }

            // ── 전직 비용 / 조건 ──
            // 통합 전직 파편 — 어떤 직업이든 동일한 파편 풀에서 40개 소모.
            int owned = _mgr.GetFragments();
            int cost = _mgr.GetFragmentCost(job.jobName);

            if (isUnlocked)
            {
                if (detail.freeLabel != null) detail.freeLabel.gameObject.SetActive(true);
                if (detail.fragCondRow != null) detail.fragCondRow.gameObject.SetActive(false);
                if (detail.prereqCondRow != null) detail.prereqCondRow.gameObject.SetActive(false);
            }
            else
            {
                if (detail.freeLabel != null) detail.freeLabel.gameObject.SetActive(false);

                if (detail.fragCondRow != null) detail.fragCondRow.gameObject.SetActive(true);
                if (detail.fragCondValue != null)
                {
                    detail.fragCondValue.text = $"{NumberNotation.Format(owned)} / {NumberNotation.Format(cost)}";
                    detail.fragCondValue.color = owned >= cost ? UguiTheme.SuccessGreenBright : UguiTheme.TextPrimary;
                }

                // 선행 조건 — 2차 전직의 순서 보장.
                if (prereq != null)
                {
                    if (detail.prereqCondRow != null) detail.prereqCondRow.gameObject.SetActive(true);
                    if (detail.prereqCondValue != null)
                    {
                        detail.prereqCondValue.text = prereqMet ? $"{JobData.GetDisplayName(prereq)} 전직 완료" : $"{JobData.GetDisplayName(prereq)} 전직 필요";
                        detail.prereqCondValue.color = prereqMet ? UguiTheme.SuccessGreenBright : FragLockedColor;
                    }
                }
                else
                {
                    if (detail.prereqCondRow != null) detail.prereqCondRow.gameObject.SetActive(false);
                }
            }

            // ── 전직하기 버튼 (Item_ActionButton) ──
            bool canChange = player != null && _mgr.CanChangeJob(job.jobName, player);

            string btnText;
            Color btnColor;
            bool btnEnabled;
            if (isCurrent)
            {
                btnText = "현재 직업";
                btnColor = UguiTheme.DisabledGrey;
                btnEnabled = false;
            }
            else if (isUnlocked)
            {
                btnText = "재전직 (무료)";
                btnColor = UguiTheme.BtnConfirm;
                btnEnabled = true;
            }
            else if (!prereqMet)
            {
                btnText = $"{JobData.GetDisplayName(prereq)} 전직 필요";
                btnColor = UguiTheme.DisabledGrey;
                btnEnabled = false;
            }
            else if (canChange)
            {
                btnText = $"전직하기 (파편 {NumberNotation.Format(cost)}개)";
                btnColor = UguiTheme.BtnSpend;
                btnEnabled = true;
            }
            else
            {
                btnText = $"전직 파편 부족 ({NumberNotation.Format(owned)}/{NumberNotation.Format(cost)})";
                btnColor = UguiTheme.DisabledGrey;
                btnEnabled = false;
            }

            var capturedPlayer = player;
            var capturedJob = job;
            ClearChildren(detail.changeRow);
            AddActionButton(detail.changeRow, btnText, btnColor, btnEnabled,
                () => OnJobChangeClicked(capturedPlayer, capturedJob));
        }

        /// <summary>
        /// 현재 직업의 스탯 vs 신규 직업의 스탯을 비교하는 표를 만든다 (Item_StatCompareRow).
        /// 변화량을 색으로 강조 (▲상승=초록, ▼하락=빨강).
        /// </summary>
        private static void BuildStatCompareTable(RectTransform table, Player player, JobData job)
        {
            if(table==null || player==null) return;
            ClearChildren(table);
            var current=player.playerStatus;var state=LocalProgression.State;
            decimal attackAura=0,healthAura=0;
            foreach(var member in UserManager.Instance.GetPlayers().Take(3))
            {
                string name=member==player?job.jobName:state.Jobs.TryGetValue(member.PlayerIndex,out var selected)?selected:member.playerStatus.JobName;
                if(name=="Knight" || name=="Elite_Knight")healthAura+=.1m;
                if(name=="Archer" || name=="Elite_Archer")attackAura+=.1m;
                if(name=="Mage" || name=="Elite_Mage"){attackAura+=.05m;healthAura+=.05m;}
            }
            var weapon=player.PlayerEquipmentManager?.GetSlotEquipment(eEquipmentSlot.Weapon);
            bool retain=weapon!=null && weapon.baseData.IsAllowedForJob(job.jobName);
            long attack=BalanceMath.Stat(job.atk,retain?weapon.GetFinalAtk():0,0,state.AttackLevel,attackAura,state.AccountLevel,state.ReincarnationLevel);
            long health=BalanceMath.Stat(job.maxHP,retain?weapon.GetFinalMaxHP():0,0,state.HealthLevel,healthAura,state.AccountLevel,state.ReincarnationLevel);
            AddCompareRow(table,"스탯","현재","적용 후","변화",UguiTheme.TextSecondary,UguiTheme.TextSecondary,isHead:true);
            AddStatCompareRow(table,"HP",current.MaxHP,health,true);
            AddStatCompareRow(table,"공격력",current.Atk,attack,true);
            AddStatCompareRow(table,"이동속도",current.MovSpeed,job.movSpeed,true);
    }

        private static void AddStatCompareRow(RectTransform table, string name, decimal curVal, decimal newVal, bool higherIsBetter, string suffix = "")
        {
            decimal diff = newVal - curVal;
            string diffText;
            Color diffColor;
            if (System.Math.Abs(diff) < 0.001m)
            {
                diffText = "-";
                diffColor = new Color(1f, 1f, 1f, 0.60f);
            }
            else
            {
                bool isUp = diff > 0m;
                bool isGood = isUp == higherIsBetter;
                string arrow = isUp ? "+" : "-";
                diffText = $"{arrow}{NumberNotation.Format(System.Math.Abs(diff))}{suffix}";
                diffColor = isGood ? UguiTheme.SuccessGreenBright : UguiTheme.WarnRed;
            }

            AddCompareRow(table, name, $"{NumberNotation.Format(curVal)}{suffix}", $"{NumberNotation.Format(newVal)}{suffix}", diffText,
                new Color(1f, 1f, 1f, 0.85f), diffColor, isHead: false);
        }

        private static void AddCompareRow(RectTransform table, string c0, string c1, string c2, string c3,
            Color normalColor, Color diffColor, bool isHead)
        {
            var cat = Cat;
            if (cat == null || cat.itemStatCompareRow == null) return;

            var go = Object.Instantiate(cat.itemStatCompareRow, table, false);
            var view = go.GetComponent<StatCompareRowView>();
            if (view == null) { Object.Destroy(go); return; }
            view.Set(c0, c1, c2, c3, normalColor, diffColor, isHead);
        }

        private static void OnJobChangeClicked(Player player, JobData job)
        {
            if (player == null || _mgr == null) return;

            _mgr.TryChangeJob(player, job.jobName,
                onSuccess: () =>
                {
                    ShowToast($"다음 일반 웨이브부터 {job.DisplayName} 적용");
                    BuildMemberTabs();
                    ShowJobDetail(job);
                },
                onError: msg => ShowToast(string.IsNullOrEmpty(msg) ? "전직에 실패했습니다." : msg));
        }

        // ── 유틸 ──

        /// <summary>
        /// 초상화 내부 요소에 스프라이트를 적용한다.
        /// idle 때 산출한 _portraitScale을 그대로 사용하므로
        /// 공격 모션처럼 폭이 넓은 프레임이 와도 캐릭터 크기가 일정하다.
        /// 컨테이너의 RectMask2D가 넘치는 부분을 클리핑한다.
        /// </summary>
        private static void ApplyPortraitSprite(Sprite sprite)
        {
            if (_charPortraitInner == null || sprite == null) return;

            float w = sprite.rect.width * _portraitScale;
            float h = sprite.rect.height * _portraitScale;

            _charPortraitInner.rectTransform.sizeDelta = new Vector2(w, h);
            _charPortraitInner.rectTransform.anchoredPosition = Vector2.zero;   // 중앙 정렬
            _charPortraitInner.sprite = sprite;
            _charPortraitInner.enabled = true;
        }

        private static Player GetCurrentPlayer()
        {
            if (_players == null || _activeMemberIndex >= _players.Count) return null;
            return _players[_activeMemberIndex];
        }

        private static GameObject GetNavPrefab()
        {
            return Cat != null ? Cat.itemNavTabButton : null;
        }

        /// <summary>콘텐츠를 비우고 지정 프리팹을 스크롤 Content 에 인스턴스화한 뒤 View 를 반환.</summary>
        private static T InstantiateContent<T>(GameObject prefab) where T : Component
        {
            if (_view == null || _view.content == null) return null;

            ClearChildren(_view.content);

            if (prefab == null)
            {
                Debug.LogWarning($"[KingdomArmyPanel] 카탈로그의 {typeof(T).Name} 프리팹이 없습니다.");
                return null;
            }

            var go = Object.Instantiate(prefab, _view.content, false);
            if (_contentPage != typeof(T))
            {
                _view.scroll?.StopMovement();
                var position = _view.content.anchoredPosition;
                _view.content.anchoredPosition = new Vector2(position.x, 0);
                _contentPage = typeof(T);
            }
            var comp = go.GetComponent<T>();
            if (comp == null)
            {
                Debug.LogError($"[KingdomArmyPanel] {typeof(T).Name} 컴포넌트가 없습니다.");
                Object.Destroy(go);
                return null;
            }
            return comp;
        }

        /// <summary>단독 안내 메시지 화면(Panel_KAMessage)을 콘텐츠에 표시.</summary>
        private static void ShowMessageInContent(string message)
        {
            var msg = InstantiateContent<KAMessageView>(Cat != null ? Cat.panelKAMessage : null);
            if (msg != null) msg.Set(message);
        }

        /// <summary>액션 버튼(Item_ActionButton) 하나를 부모 행에 추가.</summary>
        private static void AddActionButton(RectTransform parent, string text, Color bg, bool interactable, System.Action onClick)
        {
            var cat = Cat;
            if (cat == null || cat.itemActionButton == null || parent == null) return;

            var go = Object.Instantiate(cat.itemActionButton, parent, false);
            var view = go.GetComponent<ActionButtonView>();
            if (view == null) { Object.Destroy(go); return; }

            view.Set(text, bg, interactable);
            if (onClick != null) view.OnClick(onClick);
        }

        /// <summary>아이콘 Image 에 스프라이트 적용 (스프라이트 없으면 원본 배경 유지).</summary>
        private static void SetIconSprite(Image img, Sprite sprite)
        {
            if (img == null || sprite == null) return;
            img.sprite = sprite;
            img.color = Color.white;
            img.type = Image.Type.Simple;
            img.preserveAspect = true;
            img.enabled = true;
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

        private static void ShowToast(string msg)
        {
            var uiMgr = UIManager.Instance;
            if (uiMgr != null) uiMgr.ShowToast(msg);
        }
    }
}
