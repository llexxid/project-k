using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using KingdomIdle.KingdomArmy;

namespace KingdomIdle.UGUI
{
    /// <summary>
    /// 왕국군 패널 컨트롤러 (UITKKingdomArmyPanelController 이식).
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

        private static KingdomArmyPanelView _view;
        private static readonly List<NavTabButtonView> _memberTabButtons = new();
        private static readonly List<NavTabButtonView> _navButtons = new();

        // 캐시된 플레이어 목록
        private static List<Player> _players;
        private static KingdomArmyManager _mgr;

        // 종합 탭 실시간 갱신용 캐시 (기존 IVisualElementScheduledItem → FrameTick 타이머)
        private static bool _charTickSubscribed;
        private static float _charTickTimer;
        private const float CharTickInterval = 0.2f;
        private static TMP_Text _lblHp;
        private static Image _charPortraitInner;

        /// <summary>초기 idle 스프라이트 기준 1px당 표시 크기 (고정 스케일)</summary>
        private static float _portraitScale;
        private const float PORTRAIT_SIZE = 120f;

        // ── 진입점 ──

        public static void Populate(KingdomArmyPanelView view)
        {
            if (view == null) return;

            _view = view;
            if (_view.memberTabs == null || _view.content == null || _view.navBar == null) return;

            view.OnClosed = () =>
            {
                UnsubscribeCharTick();
                if (_view == view)
                {
                    _view = null;
                    _memberTabButtons.Clear();
                    _navButtons.Clear();
                    _lblHp = null;
                    _charPortraitInner = null;
                }
            };

            _mgr = KingdomArmyManager.Instance;
            if (_mgr == null)
            {
                AddPlaceholder("KingdomArmyManager를 씬에 배치해주세요.");
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
            if (_view == null) return;
            UguiRuntimeFactory.Clear(_view.memberTabs);
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
                        label = $"왕국군{i + 1} ({job})";
                }

                var go = Object.Instantiate(prefab, _view.memberTabs, false);
                var tab = go.GetComponent<NavTabButtonView>();
                if (tab == null) continue;

                tab.SetLabel(label);
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
            UguiRuntimeFactory.Clear(_view.navBar);
            _navButtons.Clear();

            var prefab = GetNavPrefab();
            if (prefab == null) return;

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
                var go = Object.Instantiate(prefab, _view.navBar, false);
                var tab = go.GetComponent<NavTabButtonView>();
                if (tab == null) continue;

                tab.SetLabel(label);
                tab.Button.onClick.AddListener(() =>
                {
                    _activeSubMenu = m;
                    Refresh();
                    UpdateNavStyles();
                });
                _navButtons.Add(tab);
            }
            UpdateNavStyles();
        }

        private static void UpdateNavStyles()
        {
            for (int i = 0; i < _navButtons.Count; i++)
                _navButtons[i].SetSelected(i == (int)_activeSubMenu, UguiTheme.AccentBlue);
        }

        // ── 콘텐츠 라우터 ──

        private static void Refresh()
        {
            if (_view == null || _view.content == null) return;

            // 이전 실시간 갱신 해제
            UnsubscribeCharTick();
            _lblHp = null;
            _charPortraitInner = null;

            UguiRuntimeFactory.Clear(_view.content);

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
                AddPlaceholder("플레이어 정보를 불러올 수 없습니다.");
                return;
            }

            var ps = player.playerStatus;
            var content = _view.content;

            // 초상화 + 직업명 (.ka-char-header)
            var header = UguiRuntimeFactory.Box(content, "CharHeader", new Color(1f, 1f, 1f, 0.05f));
            UguiRuntimeFactory.HorizontalLayout(header.gameObject, 16f, new RectOffset(12, 12, 12, 12), TextAnchor.UpperLeft);

            // 초상화 컨테이너 (120×120, overflow hidden 대응 → RectMask2D)
            var portrait = UguiRuntimeFactory.Box(header.transform, "Portrait", UguiTheme.SurfaceLight);
            var portraitLe = UguiRuntimeFactory.Preferred(portrait, width: PORTRAIT_SIZE, height: PORTRAIT_SIZE);
            portraitLe.minWidth = PORTRAIT_SIZE;
            portrait.gameObject.AddComponent<RectMask2D>();

            _charPortraitInner = UguiRuntimeFactory.Box(portrait.transform, "Inner", Color.white, rounded: false);
            _charPortraitInner.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            _charPortraitInner.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            _charPortraitInner.rectTransform.pivot = new Vector2(0.5f, 0.5f);

            // idle 스프라이트 기준으로 고정 스케일 산출
            var sr = player.GetComponent<SpriteRenderer>();
            if (sr != null && sr.sprite != null)
            {
                float idleH = sr.sprite.rect.height;
                _portraitScale = (idleH > 0f) ? PORTRAIT_SIZE / idleH : 1f;
                ApplyPortraitSprite(sr.sprite);
            }

            var infoCol = UguiRuntimeFactory.Container(header.transform, "Info");
            UguiRuntimeFactory.VerticalLayout(infoCol.gameObject, 6f);
            UguiRuntimeFactory.Flexible(infoCol, 1f);

            AddStatLine(infoCol, $"직업: {ps.JobName}");
            _lblHp = AddStatLine(infoCol, $"HP: {ps.HP} / {ps.MaxHP}");
            AddStatLine(infoCol, $"공격력: {ps.Atk}");
            AddStatLine(infoCol, $"이동속도: {ps.MovSpeed}");

            // 장착 장비 표시
            AddSectionTitle("장착 장비");
            var equipped = player.PlayerEquipmentManager?.GetSlotEquipment(eEquipmentSlot.Weapon);
            if (equipped != null)
                AddStatLine(content, $"{equipped.baseData.equipmentName} +{equipped.enhancementLevel} (ATK +{equipped.GetFinalAtk()})");
            else
                AddPlaceholder("없음");

            // 실시간 갱신 (200ms 간격으로 HP, 초상화 스프라이트 업데이트)
            SubscribeCharTick();
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
        }

        // ══════════════════════════════════════
        //  장비 (인벤토리 내 장비만 표시)
        // ══════════════════════════════════════

        private static void BuildEquipmentView()
        {
            var player = GetCurrentPlayer();
            string jobName = player?.playerStatus?.JobName ?? "";
            var equipMgr = player?.PlayerEquipmentManager;
            EquipmentManager equipmentManager = EquipmentManager.Instance;
            var content = _view.content;

            AddSectionTitle("장비");

            // ── 현재 장착 슬롯 표시 ──
            AddSubsectionTitle("장착 중");
            var equippedRow = MakeEquipGrid(content);

            var equipped = equipMgr?.GetSlotEquipment(eEquipmentSlot.Weapon);
            var equippedCard = UguiRuntimeFactory.Box(equippedRow, "EquippedCard", UguiTheme.SurfaceFaint);
            UguiRuntimeFactory.VerticalLayout(equippedCard.gameObject, 4f, new RectOffset(6, 6, 8, 8), TextAnchor.UpperCenter);
            if (equipped != null)
            {
                AddEquippedFrame(equippedCard);
                AddCardLabel(equippedCard.transform, "무기", 20f, new Color(1f, 1f, 1f, 0.85f));
                AddCardIcon(equippedCard.transform, equipped.baseData.icon);
                string enhStr0 = equipped.enhancementLevel > 0 ? $" +{equipped.enhancementLevel}" : "";
                AddCardLabel(equippedCard.transform, $"{equipped.baseData.equipmentName}{enhStr0}", 20f, new Color(1f, 1f, 1f, 0.85f));
                AddCardLabel(equippedCard.transform, $"ATK +{equipped.GetFinalAtk()}  HP +{equipped.GetFinalMaxHP()}", 18f, new Color(1f, 1f, 1f, 0.45f));

                // 장착 해제 버튼 (.ka-small-btn)
                var capturedEquipped = equipped;
                var capturedMgr = equipMgr;
                var unequipBtn = UguiRuntimeFactory.TextButton(equippedCard.transform, "해제", 20f,
                    new Color(60f / 255f, 130f / 255f, 230f / 255f, 0.60f), () =>
                    {
                        capturedMgr.Unequip(eEquipmentSlot.Weapon);
                        ShowToast($"{capturedEquipped.baseData.equipmentName} 해제");
                        Refresh();
                    }, out _);
                UguiRuntimeFactory.Preferred((RectTransform)unequipBtn.transform, height: 44f);
            }
            else
            {
                AddCardLabel(equippedCard.transform, "무기", 20f, new Color(1f, 1f, 1f, 0.85f));
                AddCardLabel(equippedCard.transform, "비어있음", 18f, new Color(1f, 1f, 1f, 0.30f));
            }

            // ── 인벤토리 내 장비 목록 ──
            AddSubsectionTitle("보유 장비");

            if (equipmentManager?.Inventory == null || equipmentManager.Inventory.Items.Count == 0)
            {
                AddPlaceholder("보유한 장비가 없습니다.");
                return;
            }

            var grid = MakeEquipGrid(content);

            // 1차: 장착가능(해당 전직) > 장착불가  2차: 등급 내림차순  3차: 강화레벨 내림차순
            var sortedItems = equipmentManager.Inventory.Items
                .OrderByDescending(i => i.baseData.IsAllowedForJob(jobName) ? 1 : 0)
                .ThenByDescending(i => i.baseData.rarity)
                .ThenByDescending(i => i.enhancementLevel)
                .ToList();

            foreach (var item in sortedItems)
            {
                BuildInventoryEquipCard(grid, item, jobName, equipped, equipMgr);
            }
        }

        private static void BuildInventoryEquipCard(
            RectTransform grid, EquipmentInstance item, string jobName,
            EquipmentInstance equipped, PlayerEquipmentManager equipMgr)
        {
            bool isAllowed = item.baseData.IsAllowedForJob(jobName);
            bool isEquipped = equipped != null && equipped == item;

            var card = UguiRuntimeFactory.Box(grid, "EquipCard", UguiTheme.SurfaceFaint, raycastTarget: true);
            UguiRuntimeFactory.VerticalLayout(card.gameObject, 4f, new RectOffset(6, 6, 8, 8), TextAnchor.UpperCenter);

            var capturedItem = item;
            var capturedMgr = equipMgr;
            bool capturedEquipped = isEquipped;
            bool capturedAllowed = isAllowed;

            var btn = card.gameObject.AddComponent<Button>();
            btn.targetGraphic = card;
            btn.colors = UguiTheme.MakeColorBlock();
            card.gameObject.AddComponent<PlayClickSfxOnClick>();
            btn.onClick.AddListener(() => ShowEquipmentActionPopup(capturedItem, capturedEquipped, capturedAllowed, capturedMgr));

            // 전직에 맞지 않는 장비는 어둡게 (.ka-equip-dimmed)
            if (!isAllowed)
            {
                var group = card.gameObject.AddComponent<CanvasGroup>();
                group.alpha = 0.35f;
            }

            // 장착된 장비는 테두리 색 변경 (.ka-equip-equipped)
            if (isEquipped)
                AddEquippedFrame(card);

            // 등급 표시
            string rarityStr = item.baseData.rarity switch
            {
                eEquipmentRarity.Normal => "[일반]",
                eEquipmentRarity.Rare => "[레어]",
                eEquipmentRarity.Epic => "[에픽]",
                _ => ""
            };

            // 아이콘
            AddCardIcon(card.transform, item.baseData.icon);

            // 이름 + 강화
            string enhStr = item.enhancementLevel > 0 ? $" +{item.enhancementLevel}" : "";
            AddCardLabel(card.transform, $"{rarityStr} {item.baseData.equipmentName}{enhStr}", 20f,
                UguiTheme.RarityColor(item.baseData.rarity));

            // 스탯
            AddCardLabel(card.transform, $"ATK +{item.GetFinalAtk()}  HP +{item.GetFinalMaxHP()}", 18f, new Color(1f, 1f, 1f, 0.45f));

            if (isEquipped)
                AddCardLabel(card.transform, "장착 중", 18f, UguiTheme.SuccessGreenBright);
        }

        // ── 장비 액션 팝업 (장착/강화 선택) ──

        private static void ShowEquipmentActionPopup(
            EquipmentInstance item, bool isEquipped, bool isAllowed, PlayerEquipmentManager equipMgr)
        {
            if (_view == null) return;
            UnsubscribeCharTick();
            UguiRuntimeFactory.Clear(_view.content);
            var content = _view.content;

            // 뒤로가기
            var backBtn = UguiRuntimeFactory.TextButton(content, "← 장비 목록", 22f, UguiTheme.SurfaceLight,
                () => { _activeSubMenu = SubMenu.Equipment; Refresh(); }, out _);
            UguiRuntimeFactory.Preferred((RectTransform)backBtn.transform, height: 44f);

            AddSectionTitle("장비 상세");

            // 장비 정보 (.ka-job-detail-header)
            var infoBox = UguiRuntimeFactory.Box(content, "InfoBox", new Color(1f, 1f, 1f, 0.05f));
            UguiRuntimeFactory.HorizontalLayout(infoBox.gameObject, 16f, new RectOffset(14, 14, 14, 14), TextAnchor.UpperLeft);

            var iconBg = UguiRuntimeFactory.Box(infoBox.transform, "Icon", UguiTheme.SurfaceLight);
            var iconLe = UguiRuntimeFactory.Preferred(iconBg, width: 120f, height: 120f);
            iconLe.minWidth = 120f;
            if (item.baseData.icon != null)
            {
                iconBg.sprite = item.baseData.icon;
                iconBg.color = Color.white;
                iconBg.preserveAspect = true;
            }

            var infoCol = UguiRuntimeFactory.Container(infoBox.transform, "InfoCol");
            UguiRuntimeFactory.VerticalLayout(infoCol.gameObject, 6f);
            UguiRuntimeFactory.Flexible(infoCol, 1f);

            string rarityStr = item.baseData.rarity switch
            {
                eEquipmentRarity.Normal => "일반",
                eEquipmentRarity.Rare => "레어",
                eEquipmentRarity.Epic => "에픽",
                _ => ""
            };

            string enhStr = item.enhancementLevel > 0 ? $" +{item.enhancementLevel}" : "";
            var nameLbl = UguiRuntimeFactory.Label(infoCol, $"{item.baseData.equipmentName}{enhStr}", 30f,
                UguiTheme.TextPrimary, TextAlignmentOptions.Left, bold: true);
            UguiRuntimeFactory.Preferred(nameLbl, height: 40f);
            AddStatLine(infoCol, $"등급: {rarityStr}");
            AddStatLine(infoCol, $"공격력 보너스: +{item.GetFinalAtk()}");
            AddStatLine(infoCol, $"HP 보너스: +{item.GetFinalMaxHP()}");
            AddStatLine(infoCol, $"강화 레벨: {item.enhancementLevel} / {item.baseData.maxEnhancementLevel}");

            if (isEquipped)
            {
                var lbl = UguiRuntimeFactory.Label(infoCol, "현재 장착 중", 24f, UguiTheme.SuccessGreenBright);
                UguiRuntimeFactory.Preferred(lbl, height: 32f);
            }

            // ── 액션 버튼들 (.ka-equip-action-row) ──
            var btnRow = UguiRuntimeFactory.Container(content, "ActionRow");
            UguiRuntimeFactory.HorizontalLayout(btnRow.gameObject, 12f, null, TextAnchor.MiddleCenter, expandWidth: true);
            UguiRuntimeFactory.Preferred(btnRow.gameObject.AddComponent<LayoutElement>(), height: 64f);

            // 장착 / 해제 버튼
            if (isEquipped)
            {
                var unequipBtn = UguiRuntimeFactory.TextButton(btnRow, "해제", 28f, UguiTheme.AccentBlue, () =>
                {
                    equipMgr.Unequip(item.baseData.slot);
                    ShowToast($"{item.baseData.equipmentName} 해제");
                    Refresh();
                }, out _);
                UguiRuntimeFactory.Flexible((RectTransform)unequipBtn.transform, 1f);
            }
            else if (isAllowed)
            {
                var equipBtn = UguiRuntimeFactory.TextButton(btnRow, "장착", 28f, UguiTheme.AccentBlue, () =>
                {
                    equipMgr.Equip(item);
                    ShowToast($"{item.baseData.equipmentName} 장착!");
                    Refresh();
                }, out _);
                UguiRuntimeFactory.Flexible((RectTransform)equipBtn.transform, 1f);
            }
            else
            {
                var disabledBtn = UguiRuntimeFactory.TextButton(btnRow, "장착 불가 (직업 제한)", 24f,
                    UguiTheme.DisabledGrey, null, out _);
                disabledBtn.interactable = false;
                UguiRuntimeFactory.Flexible((RectTransform)disabledBtn.transform, 1f);
            }

            // 강화 버튼
            BuildEnhanceButton(btnRow, item, equipMgr);

            // 강화 정보 표시
            BuildEnhanceInfo(item);
        }

        /// <summary>강화 버튼을 생성한다. 장비 상세에서 사용.</summary>
        private static void BuildEnhanceButton(RectTransform parent, EquipmentInstance item, PlayerEquipmentManager equipMgr)
        {
            if (item.IsMaxLevel())
            {
                var maxBtn = UguiRuntimeFactory.TextButton(parent, "강화 MAX", 28f, UguiTheme.DisabledGrey, null, out _);
                maxBtn.interactable = false;
                UguiRuntimeFactory.Flexible((RectTransform)maxBtn.transform, 1f);
                return;
            }

            var capturedItem = item;
            var capturedMgr = equipMgr;
            var enhBtn = UguiRuntimeFactory.TextButton(parent, "강화", 28f, UguiTheme.EnhanceOrange,
                () => TryEnhanceEquipment(capturedItem, capturedMgr), out _);
            UguiRuntimeFactory.Flexible((RectTransform)enhBtn.transform, 1f);
        }

        /// <summary>강화 시도. 재료 부족 시 부족 수량을 토스트로 안내.</summary>
        private static void TryEnhanceEquipment(EquipmentInstance item, PlayerEquipmentManager equipMgr)
        {
            if (item.IsMaxLevel())
            {
                ShowToast("이미 최대 강화 레벨입니다.");
                return;
            }

            int needed = item.GetMaterialCount();
            int available = 0;
            if (EquipmentManager.Instance.Inventory != null)
            {
                foreach (var inv in EquipmentManager.Instance.Inventory.Items)
                {
                    if (inv != item && inv.baseData == item.baseData && !inv.IsEquipped)
                        available++;
                }
            }

            if (available < needed)
            {
                int shortage = needed - available;
                ShowToast($"동일 장비 부족! (보유: {available}/{needed}개, {shortage}개 부족)");
                return;
            }

            bool success = EquipmentManager.Instance.TryEnhance(item);
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
                equipMgr.GetSlotEquipment(item.baseData.slot) == item,
                item.baseData.IsAllowedForJob(GetCurrentPlayer()?.playerStatus?.JobName ?? ""),
                equipMgr);
        }

        /// <summary>강화 관련 정보 (필요 재료, 성공 확률 등)</summary>
        private static void BuildEnhanceInfo(EquipmentInstance item)
        {
            if (_view == null || item.IsMaxLevel()) return;
            var content = _view.content;

            AddSubsectionTitle("강화 정보");

            int needed = item.GetMaterialCount();
            int available = 0;
            if (EquipmentManager.Instance?.Inventory != null)
            {
                foreach (var inv in EquipmentManager.Instance.Inventory.Items)
                {
                    if (inv != item && inv.baseData == item.baseData)
                        available++;
                }
            }

            float successRate = item.GetEnhanceSuccessRate() * 100f;

            var matColor = available < needed ? FragLockedColor : StatLineColor;
            var matLbl = UguiRuntimeFactory.Label(content,
                $"필요 재료: {item.baseData.equipmentName} x{needed} (보유: {available}개)", 24f, matColor, wrap: true);
            UguiRuntimeFactory.Preferred(matLbl, height: 34f);

            AddStatLine(content, $"성공 확률: {successRate:F0}%");

            // 강화 후 예상 스탯
            int nextAtk = item.baseData.bonusAtk + (int)(item.baseData.bonusAtk * item.baseData.atkGrowthPerLevel * (item.enhancementLevel + 1));
            int nextHP = item.baseData.bonusMaxHP + (int)(item.baseData.bonusMaxHP * item.baseData.hpGrowthPerLevel * (item.enhancementLevel + 1));
            AddStatLine(content, $"강화 시 예상: ATK +{item.GetFinalAtk()} → +{nextAtk}  HP +{item.GetFinalMaxHP()} → +{nextHP}");
        }

        // ══════════════════════════════════════
        //  왕국군 스킬
        // ══════════════════════════════════════

        private static void BuildSkillView()
        {
            var content = _view.content;
            AddSectionTitle("스킬");

            var player = GetCurrentPlayer();
            if (player == null || player.playerStatus == null)
            {
                AddPlaceholder("플레이어 정보 없음");
                return;
            }

            // 현재 직업의 스킬 표시
            var changeJob = player.GetComponent<ChangeJob>();
            if (changeJob == null)
            {
                AddPlaceholder("스킬 정보 없음");
                return;
            }

            // 현재 직업의 스킬 목록 표시
            string jobName = player.playerStatus?.JobName ?? "";
            var skillInfos = SkillSystem.GetJobSkillInfo(jobName);

            if (skillInfos == null || skillInfos.Length == 0)
            {
                AddPlaceholder("직업 스킬이 없습니다.");
                return;
            }

            foreach (var si in skillInfos)
            {
                // .ka-skill-row: bg white@4% radius8 padding10/8
                var row = UguiRuntimeFactory.Box(content, "SkillRow", new Color(1f, 1f, 1f, 0.04f));
                UguiRuntimeFactory.VerticalLayout(row.gameObject, 4f, new RectOffset(10, 10, 8, 8));

                var nameLbl = UguiRuntimeFactory.Label(row.transform, si.Name, 24f, UguiTheme.TextPrimary,
                    TextAlignmentOptions.Left, bold: true);
                UguiRuntimeFactory.Preferred(nameLbl, height: 32f);

                string typeTag = si.IsPassive ? "[패시브]" : "[액티브]";
                var descLbl = UguiRuntimeFactory.Label(row.transform, $"{typeTag}  {si.Description}", 20f,
                    new Color(1f, 1f, 1f, 0.55f), TextAlignmentOptions.Left, wrap: true);
                UguiRuntimeFactory.Preferred(descLbl, height: 30f);
            }
        }

        // ══════════════════════════════════════
        //  전직 메뉴 (왕국군전직메뉴)
        // ══════════════════════════════════════

        private static void BuildJobChangeView()
        {
            var content = _view.content;
            AddSectionTitle("전직");

            var jobDB = _mgr.JobDB;
            if (jobDB == null || jobDB.Count == 0)
            {
                AddPlaceholder("직업 데이터가 없습니다.");
                return;
            }

            // 통합 전직 파편 보유량 배너 — 어떤 직업이든 파편 40개로 전직 가능.
            int ownedFrags = _mgr.GetFragments();
            int fragCost = _mgr.GetFragmentCost();

            // .ka-frag-banner: 갈색 bg + 골드 테두리
            var fragBanner = UguiRuntimeFactory.Box(content, "FragBanner", new Color(60f / 255f, 45f / 255f, 20f / 255f, 0.55f));
            UguiRuntimeFactory.HorizontalLayout(fragBanner.gameObject, 12f, new RectOffset(16, 16, 10, 10), TextAnchor.MiddleLeft);
            UguiRuntimeFactory.Preferred(fragBanner, height: 64f);
            var bannerFrame = UguiRuntimeFactory.Box(fragBanner.transform, "Frame", new Color(1f, 220f / 255f, 100f / 255f, 0.60f));
            bannerFrame.fillCenter = false;
            UguiRuntimeFactory.Stretch(bannerFrame.rectTransform);
            bannerFrame.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;

            var bannerName = UguiRuntimeFactory.Label(fragBanner.transform, "전직 파편", 24f, new Color(1f, 1f, 1f, 0.85f), bold: true);
            UguiRuntimeFactory.Preferred(bannerName, width: 130f, height: 40f);

            var bannerVal = UguiRuntimeFactory.Label(fragBanner.transform, $"{ownedFrags:N0}", 34f,
                ownedFrags >= fragCost ? UguiTheme.SuccessGreenBright : UguiTheme.AccentGoldStrong,
                TextAlignmentOptions.Left, bold: true);
            UguiRuntimeFactory.Preferred(bannerVal, width: 140f, height: 44f);

            var bannerHint = UguiRuntimeFactory.Label(fragBanner.transform, $"(전직당 {fragCost}개 소모)", 20f, new Color(1f, 1f, 1f, 0.55f));
            UguiRuntimeFactory.Flexible(bannerHint, 1f);

            var player = GetCurrentPlayer();
            string currentJob = player?.playerStatus?.JobName ?? "";

            // 1차 전직 / 2차 전직 그룹 분리
            AddJobSectionTitle("1차 전직");
            var basicGrid = MakeJobGrid(content);

            var eliteJobs = new List<JobData>();
            for (int i = 0; i < jobDB.Count; i++)
            {
                var job = jobDB.GetJob(i);
                if (job == null) continue;

                // 창병(Spearman)은 전직 목록에서 제외
                if (job.jobName == "Spearman") continue;

                bool isElite = KingdomArmyManager.GetPrerequisiteJob(job.jobName) != null;
                if (isElite)
                {
                    eliteJobs.Add(job);
                    continue;
                }
                BuildJobCard(basicGrid, job, player, currentJob, isElite: false);
            }

            AddJobSectionTitle("2차 전직 (정예)");
            var eliteGrid = MakeJobGrid(content);
            foreach (var job in eliteJobs)
                BuildJobCard(eliteGrid, job, player, currentJob, isElite: true);
        }

        /// <summary>
        /// 전직 카드를 생성한다. 상태에 따라 시각적으로 구분:
        /// - 현재 직업: "현재" 배지 + 강조 테두리
        /// - 보유 직업: "보유" 배지 + 무료 재전직 안내
        /// - 잠김 직업: 어두운 색 + 잠금 표시
        /// </summary>
        private static void BuildJobCard(RectTransform grid, JobData job, Player player, string currentJob, bool isElite)
        {
            // 상태 판정
            bool isCurrent = currentJob == job.jobName;
            bool isUnlocked = player != null && _mgr.IsAlreadyUnlocked(player, job.jobName);
            string prereq = KingdomArmyManager.GetPrerequisiteJob(job.jobName);
            bool prereqMet = prereq == null || (player != null && _mgr.HasCompletedPromotion(player, prereq));

            // 통합 전직 파편 — 모든 직업이 동일한 파편 풀을 공유한다.
            int owned = _mgr.GetFragments();
            int cost = _mgr.GetFragmentCost();
            bool fragReady = owned >= cost;

            // 카드 배경 색 (상태 변형)
            Color bg = new Color(1f, 1f, 1f, 0.07f);
            if (isCurrent) bg = new Color(1f, 230f / 255f, 100f / 255f, 0.12f);
            else if (isElite) bg = new Color(160f / 255f, 100f / 255f, 200f / 255f, 0.10f);
            else if (!prereqMet) bg = new Color(0.4f, 0.4f, 0.4f, 0.45f);

            var card = UguiRuntimeFactory.Box(grid, "JobCard", bg, raycastTarget: true);
            UguiRuntimeFactory.VerticalLayout(card.gameObject, 6f, new RectOffset(8, 8, 12, 8), TextAnchor.UpperCenter);

            var capturedJob = job;
            var btn = card.gameObject.AddComponent<Button>();
            btn.targetGraphic = card;
            btn.colors = UguiTheme.MakeColorBlock();
            card.gameObject.AddComponent<PlayClickSfxOnClick>();
            btn.onClick.AddListener(() => ShowJobDetail(capturedJob));

            // 상태 테두리
            if (isCurrent) AddCardFrame(card, new Color(1f, 230f / 255f, 100f / 255f, 1f));
            else if (isUnlocked) AddCardFrame(card, new Color(140f / 255f, 190f / 255f, 1f, 1f));
            else if (isElite) AddCardFrame(card, new Color(180f / 255f, 100f / 255f, 220f / 255f, 0.70f));

            // 상단 배지 (.ka-job-card-badge — absolute top-right)
            string badgeText;
            Color badgeColor;
            if (isCurrent) { badgeText = "현재"; badgeColor = UguiTheme.AccentGoldStrong; }
            else if (isUnlocked) { badgeText = "보유"; badgeColor = new Color(120f / 255f, 180f / 255f, 1f, 1f); }
            else if (!prereqMet) { badgeText = "잠김"; badgeColor = UguiTheme.WarnRed; }
            else if (fragReady) { badgeText = "전직가능"; badgeColor = UguiTheme.SuccessGreenBright; }
            else { badgeText = isElite ? "2차" : "1차"; badgeColor = new Color(1f, 1f, 1f, 0.55f); }

            var badge = UguiRuntimeFactory.Label(card.transform, badgeText, 16f, badgeColor, TextAlignmentOptions.Right, bold: true);
            var badgeRt = badge.rectTransform;
            badgeRt.anchorMin = new Vector2(1f, 1f);
            badgeRt.anchorMax = new Vector2(1f, 1f);
            badgeRt.pivot = new Vector2(1f, 1f);
            badgeRt.anchoredPosition = new Vector2(-6f, -6f);
            badgeRt.sizeDelta = new Vector2(90f, 22f);
            badge.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;

            // 전직 이미지 (80×80)
            var imgWrap = UguiRuntimeFactory.Container(card.transform, "ImgWrap");
            UguiRuntimeFactory.Preferred(imgWrap.gameObject.AddComponent<LayoutElement>(), height: 84f);
            if (job.jobSprite != null)
            {
                var img = UguiRuntimeFactory.Icon(imgWrap, job.jobSprite, 80f, 80f);
                img.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
                img.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                img.rectTransform.anchoredPosition = Vector2.zero;
            }

            // 직업명
            AddCardLabel(card.transform, job.jobName, 24f, UguiTheme.TextPrimary);

            // 핵심 스탯 한 줄 (HP / ATK)
            AddCardLabel(card.transform, $"HP {job.maxHP}  ·  ATK {job.atk}", 18f, new Color(1f, 1f, 1f, 0.60f));

            // 파편 현황 / 무료 재전직 안내 — 통합 전직 파편을 기준으로 진행도 표시.
            if (isUnlocked)
            {
                AddCardLabel(card.transform, "무료 재전직", 20f, UguiTheme.SuccessGreenBright);
            }
            else
            {
                AddCardLabel(card.transform, $"전직 파편 {owned}/{cost}", 20f,
                    fragReady ? UguiTheme.SuccessGreenBright : UguiTheme.AccentGoldStrong);
            }

            // 선행 조건 미충족 표시
            if (!prereqMet)
                AddCardLabel(card.transform, $"{prereq} 전직 필요", 18f, FragLockedColor);
        }

        // ══════════════════════════════════════
        //  전직 상세 팝업 (왕국군전직상세메뉴)
        // ══════════════════════════════════════

        private static void ShowJobDetail(JobData job)
        {
            if (_view == null) return;
            UnsubscribeCharTick();
            UguiRuntimeFactory.Clear(_view.content);
            var content = _view.content;

            var player = GetCurrentPlayer();
            var ps = player?.playerStatus;
            bool isCurrent = ps != null && ps.JobName == job.jobName;
            bool isUnlocked = player != null && _mgr.IsAlreadyUnlocked(player, job.jobName);
            string prereq = KingdomArmyManager.GetPrerequisiteJob(job.jobName);
            bool prereqMet = prereq == null || _mgr.HasCompletedPromotion(player, prereq);

            // 뒤로가기 버튼
            var backBtn = UguiRuntimeFactory.TextButton(content, "← 전직 목록", 22f, UguiTheme.SurfaceLight,
                () => { _activeSubMenu = SubMenu.JobChange; Refresh(); }, out _);
            UguiRuntimeFactory.Preferred((RectTransform)backBtn.transform, height: 44f);

            AddSectionTitle("전직 상세");

            // ── 직업 헤더 (이미지 + 이름 + 상태 배지) ──
            var header = UguiRuntimeFactory.Box(content, "JobHeader", new Color(1f, 1f, 1f, 0.05f));
            UguiRuntimeFactory.HorizontalLayout(header.gameObject, 16f, new RectOffset(14, 14, 14, 14), TextAnchor.UpperLeft);

            var imgBg = UguiRuntimeFactory.Box(header.transform, "Img", UguiTheme.SurfaceLight);
            var imgLe = UguiRuntimeFactory.Preferred(imgBg, width: 120f, height: 120f);
            imgLe.minWidth = 120f;
            if (job.jobSprite != null)
            {
                imgBg.sprite = job.jobSprite;
                imgBg.color = Color.white;
                imgBg.preserveAspect = true;
            }

            var nameCol = UguiRuntimeFactory.Container(header.transform, "NameCol");
            UguiRuntimeFactory.VerticalLayout(nameCol.gameObject, 6f);
            UguiRuntimeFactory.Flexible(nameCol, 1f);

            var nameRow = UguiRuntimeFactory.Container(nameCol, "NameRow");
            UguiRuntimeFactory.HorizontalLayout(nameRow.gameObject, 12f, null, TextAnchor.MiddleLeft);
            UguiRuntimeFactory.Preferred(nameRow.gameObject.AddComponent<LayoutElement>(), height: 42f);

            var jobNameLbl = UguiRuntimeFactory.Label(nameRow, job.jobName, 30f, UguiTheme.TextPrimary,
                TextAlignmentOptions.Left, bold: true);
            UguiRuntimeFactory.Preferred(jobNameLbl, height: 42f);

            // 상태 배지
            string stateText;
            Color stateColor;
            if (isCurrent) { stateText = "현재"; stateColor = UguiTheme.AccentGoldStrong; }
            else if (isUnlocked) { stateText = "보유"; stateColor = new Color(120f / 255f, 180f / 255f, 1f, 1f); }
            else { stateText = prereq != null ? "2차" : "1차"; stateColor = new Color(1f, 1f, 1f, 0.55f); }
            var stateLbl = UguiRuntimeFactory.Label(nameRow, stateText, 18f, stateColor, TextAlignmentOptions.Left, bold: true);
            UguiRuntimeFactory.Preferred(stateLbl, width: 90f, height: 30f);

            // 직업 설명 — JobData에 description 필드가 없으므로 간단한 분류 텍스트로 대체
            string roleText = job.jobName.Contains("Knight") ? "근접 탱커 / 근거리 딜러"
                              : job.jobName.Contains("Archer") ? "원거리 물리 딜러"
                              : job.jobName.Contains("Mage") ? "원거리 마법 딜러"
                              : "근접 전사";
            var roleLbl = UguiRuntimeFactory.Label(nameCol, roleText, 22f, new Color(1f, 1f, 1f, 0.65f));
            UguiRuntimeFactory.Preferred(roleLbl, height: 30f);

            // ── 스탯 비교 (현재 직업 vs 신규 직업) ──
            AddSubsectionTitle("스탯 비교");
            BuildStatCompareTable(content, ps, job);

            // ── 직업 스킬 ──
            var jobSkills = SkillSystem.GetJobSkillInfo(job.jobName);
            if (jobSkills != null && jobSkills.Length > 0)
            {
                AddSubsectionTitle("직업 스킬");

                foreach (var si in jobSkills)
                {
                    // .ka-job-skill-row: 타입 배지 + 이름/설명
                    var skillRow = UguiRuntimeFactory.Box(content, "JobSkillRow", new Color(1f, 1f, 1f, 0.04f));
                    UguiRuntimeFactory.HorizontalLayout(skillRow.gameObject, 12f, new RectOffset(10, 10, 8, 8), TextAnchor.UpperLeft);

                    // 타입 배지 (70×30: 액티브=블루, 패시브=퍼플)
                    var badgeBg = UguiRuntimeFactory.Box(skillRow.transform, "TypeBadge",
                        si.IsPassive
                            ? new Color(160f / 255f, 100f / 255f, 200f / 255f, 0.80f)
                            : new Color(80f / 255f, 140f / 255f, 220f / 255f, 0.80f));
                    var badgeLe = UguiRuntimeFactory.Preferred(badgeBg, width: 82f, height: 30f);
                    badgeLe.minWidth = 82f;
                    var badgeLbl = UguiRuntimeFactory.Label(badgeBg.transform, si.IsPassive ? "패시브" : "액티브",
                        16f, UguiTheme.TextPrimary, TextAlignmentOptions.Center, bold: true);
                    UguiRuntimeFactory.Stretch(badgeLbl.rectTransform);

                    var skillCol = UguiRuntimeFactory.Container(skillRow.transform, "Info");
                    UguiRuntimeFactory.VerticalLayout(skillCol.gameObject, 4f);
                    UguiRuntimeFactory.Flexible(skillCol, 1f);

                    var sn = UguiRuntimeFactory.Label(skillCol, si.Name, 22f, UguiTheme.TextPrimary,
                        TextAlignmentOptions.Left, bold: true);
                    UguiRuntimeFactory.Preferred(sn, height: 30f);
                    var sd = UguiRuntimeFactory.Label(skillCol, si.Description, 19f, new Color(1f, 1f, 1f, 0.55f),
                        TextAlignmentOptions.Left, wrap: true);
                    UguiRuntimeFactory.Preferred(sd, height: 28f);
                }
            }

            // ── 전직 비용 / 조건 ──
            AddSubsectionTitle("전직 조건");

            // 통합 전직 파편 — 어떤 직업이든 동일한 파편 풀에서 40개 소모.
            int owned = _mgr.GetFragments();
            int cost = _mgr.GetFragmentCost();

            var condBox = UguiRuntimeFactory.Box(content, "CondBox", new Color(0f, 0f, 0f, 0.25f));
            UguiRuntimeFactory.VerticalLayout(condBox.gameObject, 6f, new RectOffset(12, 12, 12, 12));

            if (isUnlocked)
            {
                var freeLbl = UguiRuntimeFactory.Label(condBox.transform, "✓ 이미 해금된 직업 — 무료 재전직 가능",
                    22f, UguiTheme.SuccessGreenBright);
                UguiRuntimeFactory.Preferred(freeLbl, height: 32f);
            }
            else
            {
                AddCondRow(condBox.transform, "전직 파편", $"{owned} / {cost}",
                    owned >= cost ? UguiTheme.SuccessGreenBright : UguiTheme.TextPrimary);

                // 선행 조건 — 2차 전직의 순서 보장.
                if (prereq != null)
                {
                    AddCondRow(condBox.transform, "선행 조건",
                        prereqMet ? $"✓ {prereq} 전직 완료" : $"✕ {prereq} 전직 필요",
                        prereqMet ? UguiTheme.SuccessGreenBright : FragLockedColor);
                }
            }

            // ── 전직하기 버튼 (.ka-change-btn: h72 / 32px bold) ──
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
                btnColor = UguiTheme.AccentBlue;
                btnEnabled = true;
            }
            else if (!prereqMet)
            {
                btnText = $"{prereq} 전직 필요";
                btnColor = UguiTheme.DisabledGrey;
                btnEnabled = false;
            }
            else if (canChange)
            {
                btnText = $"전직하기 (파편 {cost}개)";
                btnColor = new Color(60f / 255f, 180f / 255f, 80f / 255f, 0.70f);
                btnEnabled = true;
            }
            else
            {
                btnText = $"전직 파편 부족 ({owned}/{cost})";
                btnColor = UguiTheme.DisabledGrey;
                btnEnabled = false;
            }

            var capturedPlayer = player;
            var capturedJob = job;
            var changeBtn = UguiRuntimeFactory.TextButton(content, btnText, 32f, btnColor,
                () => OnJobChangeClicked(capturedPlayer, capturedJob), out _);
            changeBtn.interactable = btnEnabled;
            UguiRuntimeFactory.Preferred((RectTransform)changeBtn.transform, height: 72f);
        }

        /// <summary>
        /// 현재 직업의 스탯 vs 신규 직업의 스탯을 비교하는 표를 만든다.
        /// 변화량을 색으로 강조 (▲상승=초록, ▼하락=빨강).
        /// </summary>
        private static void BuildStatCompareTable(RectTransform content, PlayerStatus current, JobData job)
        {
            var table = UguiRuntimeFactory.Box(content, "StatCompareTable", new Color(0f, 0f, 0f, 0.20f));
            UguiRuntimeFactory.VerticalLayout(table.gameObject, 2f, new RectOffset(10, 10, 8, 8));

            // 헤더
            AddCompareRow(table.transform, "스탯", "현재", "신규", "변화",
                UguiTheme.TextSecondary, UguiTheme.TextSecondary, isHead: true);

            AddStatCompareRow(table.transform, "HP", current?.MaxHP ?? 0, job.maxHP, higherIsBetter: true);
            AddStatCompareRow(table.transform, "공격력", current?.Atk ?? 0, job.atk, higherIsBetter: true);
            AddStatCompareRow(table.transform, "이동속도", current?.MovSpeed ?? 0f, job.movSpeed, higherIsBetter: true);
        }

        private static void AddStatCompareRow(Transform table, string name, float curVal, float newVal, bool higherIsBetter, string suffix = "")
        {
            float diff = newVal - curVal;
            string diffText;
            Color diffColor;
            if (Mathf.Abs(diff) < 0.001f)
            {
                diffText = "—";
                diffColor = new Color(1f, 1f, 1f, 0.60f);
            }
            else
            {
                bool isUp = diff > 0f;
                bool isGood = isUp == higherIsBetter;
                string arrow = isUp ? "▲" : "▼";
                diffText = $"{arrow} {Mathf.Abs(diff):0.##}{suffix}";
                diffColor = isGood ? UguiTheme.SuccessGreenBright : UguiTheme.WarnRed;
            }

            AddCompareRow(table, name, $"{curVal:0.##}{suffix}", $"{newVal:0.##}{suffix}", diffText,
                new Color(1f, 1f, 1f, 0.85f), diffColor, isHead: false);
        }

        private static void AddCompareRow(Transform table, string c0, string c1, string c2, string c3,
            Color normalColor, Color diffColor, bool isHead)
        {
            var row = UguiRuntimeFactory.Container(table, "Row");
            UguiRuntimeFactory.HorizontalLayout(row.gameObject, 4f, null, TextAnchor.MiddleLeft);
            UguiRuntimeFactory.Preferred(row.gameObject.AddComponent<LayoutElement>(), height: 36f);

            var cell0 = UguiRuntimeFactory.Label(row, c0, 20f, normalColor, TextAlignmentOptions.Left, bold: isHead);
            UguiRuntimeFactory.Flexible(cell0, 1.4f);
            var cell1 = UguiRuntimeFactory.Label(row, c1, 20f, normalColor, TextAlignmentOptions.Center, bold: isHead);
            UguiRuntimeFactory.Flexible(cell1, 1f);
            var cell2 = UguiRuntimeFactory.Label(row, c2, 20f, normalColor, TextAlignmentOptions.Center, bold: isHead);
            UguiRuntimeFactory.Flexible(cell2, 1f);
            var cell3 = UguiRuntimeFactory.Label(row, c3, 20f, diffColor, TextAlignmentOptions.Center, bold: true);
            UguiRuntimeFactory.Flexible(cell3, 1f);
        }

        private static void AddCondRow(Transform parent, string name, string value, Color valueColor)
        {
            var row = UguiRuntimeFactory.Container(parent, "CondRow");
            UguiRuntimeFactory.HorizontalLayout(row.gameObject, 10f, null, TextAnchor.MiddleLeft);
            UguiRuntimeFactory.Preferred(row.gameObject.AddComponent<LayoutElement>(), height: 34f);

            var nameLbl = UguiRuntimeFactory.Label(row, name, 22f, new Color(1f, 1f, 1f, 0.70f));
            UguiRuntimeFactory.Preferred(nameLbl, width: 130f, height: 30f);

            var valLbl = UguiRuntimeFactory.Label(row, value, 22f, valueColor, TextAlignmentOptions.Left, bold: true);
            UguiRuntimeFactory.Flexible(valLbl, 1f);
        }

        private static void OnJobChangeClicked(Player player, JobData job)
        {
            if (player == null || _mgr == null) return;

            _mgr.TryChangeJob(player, job.jobName,
                onSuccess: () =>
                {
                    ShowToast($"{job.jobName}(으)로 전직 완료!");
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
        }

        private static Player GetCurrentPlayer()
        {
            if (_players == null || _activeMemberIndex >= _players.Count) return null;
            return _players[_activeMemberIndex];
        }

        private static GameObject GetNavPrefab()
        {
            return UIManager.Instance != null && UIManager.Instance.Catalog != null
                ? UIManager.Instance.Catalog.itemNavTabButton
                : null;
        }

        private static RectTransform MakeEquipGrid(RectTransform content)
        {
            var grid = UguiRuntimeFactory.Container(content, "EquipGrid");
            var layout = UguiRuntimeFactory.GridLayout(grid.gameObject, new Vector2(160f, 210f), new Vector2(10f, 10f));
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = 6;
            return grid;
        }

        private static RectTransform MakeJobGrid(RectTransform content)
        {
            var grid = UguiRuntimeFactory.Container(content, "JobGrid");
            var layout = UguiRuntimeFactory.GridLayout(grid.gameObject, new Vector2(190f, 260f), new Vector2(12f, 12f));
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = 5;
            return grid;
        }

        private static void AddEquippedFrame(Image card)
        {
            var frame = UguiRuntimeFactory.Box(card.transform, "EquippedFrame", new Color(80f / 255f, 200f / 255f, 120f / 255f, 1f));
            frame.fillCenter = false;
            UguiRuntimeFactory.Stretch(frame.rectTransform);
            frame.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        }

        private static void AddCardFrame(Image card, Color color)
        {
            var frame = UguiRuntimeFactory.Box(card.transform, "StateFrame", color);
            frame.fillCenter = false;
            UguiRuntimeFactory.Stretch(frame.rectTransform);
            frame.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        }

        private static void AddCardIcon(Transform parent, Sprite sprite)
        {
            var wrap = UguiRuntimeFactory.Container(parent, "IconWrap");
            UguiRuntimeFactory.Preferred(wrap.gameObject.AddComponent<LayoutElement>(), height: 64f);
            var icon = UguiRuntimeFactory.Box(wrap, "Icon", UguiTheme.SurfaceLight);
            UguiRuntimeFactory.SetSize(icon.rectTransform, 60f, 60f);
            icon.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            icon.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            icon.rectTransform.anchoredPosition = Vector2.zero;
            if (sprite != null)
            {
                icon.sprite = sprite;
                icon.color = Color.white;
                icon.preserveAspect = true;
            }
        }

        private static void AddCardLabel(Transform parent, string text, float size, Color color)
        {
            var lbl = UguiRuntimeFactory.Label(parent, text, size, color, TextAlignmentOptions.Center);
            UguiRuntimeFactory.Preferred(lbl, height: size + 8f);
        }

        private static TMP_Text AddStatLine(RectTransform parent, string text)
        {
            var lbl = UguiRuntimeFactory.Label(parent, text, 24f, StatLineColor);
            UguiRuntimeFactory.Preferred(lbl, height: 32f);
            return lbl;
        }

        private static void AddSectionTitle(string text)
        {
            var lbl = UguiRuntimeFactory.Label(_view.content, text, 28f, UguiTheme.TextPrimary, TextAlignmentOptions.Left, bold: true);
            UguiRuntimeFactory.Preferred(lbl, height: 40f);
        }

        private static void AddSubsectionTitle(string text)
        {
            var lbl = UguiRuntimeFactory.Label(_view.content, text, 24f, new Color(1f, 1f, 1f, 0.90f), TextAlignmentOptions.Left, bold: true);
            UguiRuntimeFactory.Preferred(lbl, height: 34f);
        }

        /// <summary>.ka-job-section-title: 골드 + 좌측 4px 보더.</summary>
        private static void AddJobSectionTitle(string text)
        {
            var row = UguiRuntimeFactory.Container(_view.content, "JobSectionTitle");
            UguiRuntimeFactory.HorizontalLayout(row.gameObject, 10f, null, TextAnchor.MiddleLeft);
            UguiRuntimeFactory.Preferred(row.gameObject.AddComponent<LayoutElement>(), height: 36f);

            var bar = UguiRuntimeFactory.Box(row, "Bar", UguiTheme.AccentGoldStrong, rounded: false);
            UguiRuntimeFactory.Preferred(bar, width: 4f, height: 26f);

            var lbl = UguiRuntimeFactory.Label(row, text, 24f, UguiTheme.AccentGold, TextAlignmentOptions.Left, bold: true);
            UguiRuntimeFactory.Flexible(lbl, 1f);
        }

        private static void AddPlaceholder(string msg)
        {
            var lbl = UguiRuntimeFactory.Label(_view.content, msg, 24f, PlaceholderColor, TextAlignmentOptions.Center);
            UguiRuntimeFactory.Preferred(lbl, height: 60f);
        }

        private static void ShowToast(string msg)
        {
            var uiMgr = UIManager.Instance;
            if (uiMgr != null) uiMgr.ShowToast(msg);
        }
    }
}
