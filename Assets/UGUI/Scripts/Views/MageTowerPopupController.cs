using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using KingdomIdle.MageTower;

namespace KingdomIdle.UGUI
{
    // 마탑 스킬 장착 팝업 (UITKMageTowerPopupController 이식 — 탭 기반 장착, 모바일 친화)
    // - 장착된 슬롯 탭 → 장착해제
    // - 빈 슬롯 탭 → 선택 모드 진입, 장착 가능한 스킬만 펄스 애니메이션
    // - 펄스 중인 스킬 탭 → 해당 슬롯에 장착
    public static class MageTowerPopupController
    {
        // ── USS 토큰 (.mt-equip-* / .mt-inv-*) ──
        private static readonly Color PanelBg = new Color(35f / 255f, 30f / 255f, 45f / 255f, 0.95f);
        private static readonly Color CloseBtnBg = new Color(1f, 1f, 1f, 0.10f);
        private static readonly Color ColLabelColor = new Color(1f, 1f, 1f, 0.75f);
        private static readonly Color SlotBorderNormal = new Color(1f, 1f, 1f, 0.20f);
        private static readonly Color SlotBorderActive = new Color(100f / 255f, 210f / 255f, 130f / 255f, 0.7f);   // .mt-equip-slot-active
        private static readonly Color EquippedBorder = new Color(100f / 255f, 210f / 255f, 130f / 255f, 0.6f);     // .mt-inv-item-equipped
        private static readonly Color EmptySlotText = new Color(1f, 1f, 1f, 0.3f);                                 // .mt-equip-slot-empty-label
        private static readonly Color InvNameColor = new Color(1f, 1f, 1f, 0.85f);                                 // .mt-inv-item-name
        private static readonly Color InvDmgColor = new Color(1f, 1f, 1f, 0.6f);                                   // .mt-inv-item-dmg

        // .mt-equip-panel: width 92% max 900 / height 70% max 680 → 1080x1920 기준 900x680 고정
        private const float PanelW = 900f;
        private const float PanelH = 680f;
        private const int PanelPad = 18;
        private const float PanelGap = 14f;
        private const float BodyGap = 18f;
        private const float SlotsColW = 130f;
        private const float SlotSize = 100f;
        private const float SlotBorderPx = 2f;
        private const float SlotIconSize = 92f;
        private const float InvCellW = 110f;
        private const float InvCellH = 130f;
        private const float InvGap = 12f;
        // 그리드 가용폭 = 900 - 패딩36 - 슬롯열130 - 간격18 - 콘텐츠패딩8 = 708 → 110*5 + 12*4 = 598 ≤ 708
        private const int InvColumns = 5;
        private const float LockedAlpha = 0.35f;   // .mt-inv-item-locked

        private static GameObject _overlayGo;
        private static RectTransform _grid;
        private static PulseDriver _pulse;

        private static int _selectedSlot;
        private static bool _pickingMode;

        // 펄스 (선택 모드 동안 장착 가능 아이템 alpha 토글 — USS transition opacity 대응)
        private static readonly List<CanvasGroup> _equippableItems = new List<CanvasGroup>();

        private static readonly Image[] _equipSlotBorders = new Image[MageTowerManager.SlotCount];
        private static readonly Image[] _equipSlotIcons = new Image[MageTowerManager.SlotCount];
        private static readonly TMP_Text[] _equipSlotLabels = new TMP_Text[MageTowerManager.SlotCount];

        public static bool IsOpen => _overlayGo != null;

        public static void Show(int focusSlot = 0)
        {
            _selectedSlot = Mathf.Clamp(focusSlot, 0, MageTowerManager.SlotCount - 1);
            _pickingMode = false;
            EnsureBuilt();
            if (_overlayGo == null) return;
            Refresh();
            _overlayGo.transform.SetAsLastSibling();   // BringToFront 대응
        }

        public static void Hide()
        {
            if (_overlayGo == null) return;
            ExitPickingMode();

            Object.Destroy(_overlayGo);
            _overlayGo = null;
            _grid = null;
            _pulse = null;
            _equippableItems.Clear();
            for (int i = 0; i < MageTowerManager.SlotCount; i++)
            {
                _equipSlotBorders[i] = null;
                _equipSlotIcons[i] = null;
                _equipSlotLabels[i] = null;
            }

            if (MageTowerHudController.Instance != null)
                MageTowerHudController.Instance.RefreshSlots();
        }

        private static void EnsureBuilt()
        {
            if (_overlayGo != null) return;

            var mgr = UIManager.Instance;
            if (mgr == null || mgr.LayerOverlays == null) return;

            // 오버레이 (풀스크린 딤) — 바탕 클릭 → 닫기 (패널 클릭은 안 닫힘: 패널이 레이캐스트 차단)
            var overlay = UguiRuntimeFactory.Box(mgr.LayerOverlays, "MageTowerEquipOverlay",
                UguiTheme.DimMedium, rounded: false, raycastTarget: true);
            UguiRuntimeFactory.Stretch(overlay.rectTransform);
            _overlayGo = overlay.gameObject;

            var dimBtn = overlay.gameObject.AddComponent<Button>();
            dimBtn.targetGraphic = overlay;
            dimBtn.transition = Selectable.Transition.None;
            dimBtn.onClick.AddListener(Hide);

            _pulse = _overlayGo.AddComponent<PulseDriver>();

            // 패널 (중앙 900x680)
            var panel = UguiRuntimeFactory.Box(overlay.transform, "Panel", PanelBg, rounded: true, raycastTarget: true);
            var panelRt = panel.rectTransform;
            panelRt.anchorMin = new Vector2(0.5f, 0.5f);
            panelRt.anchorMax = new Vector2(0.5f, 0.5f);
            panelRt.pivot = new Vector2(0.5f, 0.5f);
            UguiRuntimeFactory.SetSize(panelRt, PanelW, PanelH);
            UguiRuntimeFactory.VerticalLayout(panel.gameObject, PanelGap,
                new RectOffset(PanelPad, PanelPad, PanelPad, PanelPad));

            // titlebar
            var titleBar = UguiRuntimeFactory.Container(panel.transform, "TitleBar");
            UguiRuntimeFactory.HorizontalLayout(titleBar.gameObject, 0f, null, TextAnchor.MiddleLeft);

            var title = UguiRuntimeFactory.Label(titleBar, "마탑 스킬 장착", 34f, UguiTheme.TextPrimary, bold: true);
            UguiRuntimeFactory.Flexible(title, 1f);

            var closeBtn = UguiRuntimeFactory.TextButton(titleBar, "X", 30f, CloseBtnBg, Hide,
                out _, bold: false, textColor: new Color(1f, 1f, 1f, 0.9f));
            MakeCircle(closeBtn.image);
            UguiRuntimeFactory.Preferred(closeBtn, 72f, 72f);

            // body
            var body = UguiRuntimeFactory.Container(panel.transform, "Body");
            UguiRuntimeFactory.HorizontalLayout(body.gameObject, BodyGap, null, TextAnchor.UpperLeft);
            UguiRuntimeFactory.Flexible(body, 0f, 1f);

            // left: equip slots
            var slotsCol = UguiRuntimeFactory.Container(body, "SlotsCol");
            UguiRuntimeFactory.VerticalLayout(slotsCol.gameObject, 10f, null, TextAnchor.UpperCenter, expandWidth: false);
            UguiRuntimeFactory.Preferred(slotsCol, SlotsColW);
            UguiRuntimeFactory.Flexible(slotsCol, 0f, 1f);

            UguiRuntimeFactory.Label(slotsCol, "장착 슬롯", 22f, ColLabelColor, TextAlignmentOptions.Center);

            for (int i = 0; i < MageTowerManager.SlotCount; i++)
            {
                int idx = i;

                // 바깥 = 2px 테두리(선택 모드 활성 시 초록), 안쪽 = 배경
                var border = UguiRuntimeFactory.Box(slotsCol, $"Slot_{i}", SlotBorderNormal, rounded: true, raycastTarget: true);
                UguiRuntimeFactory.Preferred(border, SlotSize, SlotSize);

                var slotBtn = border.gameObject.AddComponent<Button>();
                slotBtn.targetGraphic = border;
                slotBtn.transition = Selectable.Transition.ColorTint;
                slotBtn.colors = UguiTheme.MakeColorBlock();
                slotBtn.onClick.AddListener(() => OnEquipSlotClicked(idx));
                border.gameObject.AddComponent<PlayClickSfxOnClick>();

                var bg = UguiRuntimeFactory.Box(border.transform, "Bg", UguiTheme.SurfaceLight, rounded: true);
                UguiRuntimeFactory.Stretch(bg.rectTransform);
                bg.rectTransform.offsetMin = new Vector2(SlotBorderPx, SlotBorderPx);
                bg.rectTransform.offsetMax = new Vector2(-SlotBorderPx, -SlotBorderPx);

                var icon = UguiRuntimeFactory.Icon(bg.transform, null, SlotIconSize, SlotIconSize);
                icon.gameObject.SetActive(false);

                var lbl = UguiRuntimeFactory.Label(bg.transform, "-", 18f, EmptySlotText, TextAlignmentOptions.Center);
                UguiRuntimeFactory.Stretch(lbl.rectTransform);

                _equipSlotBorders[i] = border;
                _equipSlotIcons[i] = icon;
                _equipSlotLabels[i] = lbl;
            }

            // right: inventory
            var invCol = UguiRuntimeFactory.Container(body, "InvCol");
            UguiRuntimeFactory.VerticalLayout(invCol.gameObject, 10f);
            UguiRuntimeFactory.Flexible(invCol, 1f, 1f);

            UguiRuntimeFactory.Label(invCol, "보유 스킬", 22f, ColLabelColor);

            var scroll = UguiRuntimeFactory.VerticalScroll(invCol, "InvScroll", out var content,
                InvGap, new RectOffset(4, 4, 4, 4));
            UguiRuntimeFactory.Flexible(scroll, 1f, 1f);

            // 그리드: 스크롤 Content(세로 레이아웃 + SizeFitter) 안에 GridLayoutGroup 자식 컨테이너.
            // FixedColumnCount 라 preferredHeight 가 행 수에 맞게 자동 계산된다.
            _grid = UguiRuntimeFactory.Container(content, "InvGrid");
            var gridLg = UguiRuntimeFactory.GridLayout(_grid.gameObject,
                new Vector2(InvCellW, InvCellH), new Vector2(InvGap, InvGap));
            gridLg.childAlignment = TextAnchor.UpperLeft;   // USS flex-wrap 좌측 정렬 대응
            gridLg.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gridLg.constraintCount = InvColumns;
        }

        private static void Refresh()
        {
            var mgr = MageTowerManager.Instance;
            if (mgr == null) return;

            // equip slots
            for (int i = 0; i < MageTowerManager.SlotCount; i++)
            {
                int skillId = mgr.GetEquippedSkillId(i);
                var so = skillId >= 0 ? mgr.GetSkillById(skillId) : null;

                bool active = _pickingMode && i == _selectedSlot;   // .mt-equip-slot-active
                _equipSlotBorders[i].color = active ? SlotBorderActive : SlotBorderNormal;

                if (so != null)
                {
                    if (so.icon != null)
                    {
                        _equipSlotIcons[i].sprite = so.icon;
                        _equipSlotIcons[i].gameObject.SetActive(true);
                        _equipSlotLabels[i].text = "";
                    }
                    else
                    {
                        _equipSlotIcons[i].gameObject.SetActive(false);
                        _equipSlotLabels[i].text = so.nameKor;
                        _equipSlotLabels[i].color = UguiTheme.TextPrimary;   // empty-label 스타일 해제
                    }
                }
                else
                {
                    _equipSlotIcons[i].gameObject.SetActive(false);
                    _equipSlotLabels[i].text = "-";
                    _equipSlotLabels[i].color = EmptySlotText;
                }
            }

            // inventory
            RebuildInventory(mgr);
            UpdatePulseState();
        }

        private static void RebuildInventory(MageTowerManager mgr)
        {
            if (_grid == null) return;
            _equippableItems.Clear();

            // Destroy 는 프레임 말에 실행되므로, 잔여 자식이 그리드 레이아웃에 끼지 않게 먼저 비활성화
            for (int i = _grid.childCount - 1; i >= 0; i--)
                _grid.GetChild(i).gameObject.SetActive(false);
            UguiRuntimeFactory.Clear(_grid);

            var skills = mgr.GetAllSkills();
            for (int i = 0; i < skills.Count; i++)
            {
                var skill = skills[i];
                if (skill == null) continue;
                BuildInvItem(skill, mgr);
            }
        }

        private static void BuildInvItem(MageTowerSkillSO skill, MageTowerManager mgr)
        {
            int id = skill.id;
            bool owned = mgr.IsOwned(id);
            bool equipped = owned && mgr.IsEquipped(id);
            bool equippable = owned && !equipped;

            // 바깥 = 테두리(장착중이면 초록, 아니면 투명 — 클릭 타겟 겸용), 안쪽 = 배경
            var frame = UguiRuntimeFactory.Box(_grid, $"Item_{id}",
                equipped ? EquippedBorder : Color.clear, rounded: true, raycastTarget: owned);

            var bg = UguiRuntimeFactory.Box(frame.transform, "Bg", UguiTheme.SurfaceLight, rounded: true);
            UguiRuntimeFactory.Stretch(bg.rectTransform);
            bg.rectTransform.offsetMin = new Vector2(SlotBorderPx, SlotBorderPx);
            bg.rectTransform.offsetMax = new Vector2(-SlotBorderPx, -SlotBorderPx);
            UguiRuntimeFactory.VerticalLayout(bg.gameObject, 4f, new RectOffset(6, 6, 6, 6), TextAnchor.MiddleCenter);

            var icon = UguiRuntimeFactory.Icon(bg.transform, skill.icon, 60f, 60f);
            UguiRuntimeFactory.Preferred(icon, 60f, 60f);

            UguiRuntimeFactory.Label(bg.transform, skill.nameKor, 18f, InvNameColor, TextAlignmentOptions.Center);

            if (owned)
            {
                float dmg = mgr.GetEffectiveDamage(id);
                UguiRuntimeFactory.Label(bg.transform, $"DMG {dmg:F0}", 16f, InvDmgColor, TextAlignmentOptions.Center);

                var btn = frame.gameObject.AddComponent<Button>();
                btn.targetGraphic = bg;
                btn.transition = Selectable.Transition.ColorTint;
                btn.colors = UguiTheme.MakeColorBlock();
                btn.onClick.AddListener(() => OnInvItemTapped(id, equippable));
                frame.gameObject.AddComponent<PlayClickSfxOnClick>();
            }
            else
            {
                // .mt-inv-item-locked — opacity 0.35, 입력 차단
                var lockedCg = frame.gameObject.AddComponent<CanvasGroup>();
                lockedCg.alpha = LockedAlpha;
                lockedCg.interactable = false;
                lockedCg.blocksRaycasts = false;
            }

            if (equippable)
            {
                var cg = frame.gameObject.GetComponent<CanvasGroup>();
                if (cg == null) cg = frame.gameObject.AddComponent<CanvasGroup>();
                _equippableItems.Add(cg);
            }
        }

        private static void OnInvItemTapped(int skillId, bool equippable)
        {
            var mgr = MageTowerManager.Instance;
            if (mgr == null) return;

            if (_pickingMode)
            {
                if (!equippable) return; // 장착 불가능한 스킬은 무시
                mgr.Equip(_selectedSlot, skillId);
                ExitPickingMode();
                Refresh();
            }
            else
            {
                // 일반 모드 — 상세 팝업 열기
                MageTowerDetailPopupController.Show(skillId);
            }
        }

        private static void OnEquipSlotClicked(int slotIndex)
        {
            var mgr = MageTowerManager.Instance;
            if (mgr == null) return;

            int skillId = mgr.GetEquippedSkillId(slotIndex);
            if (skillId >= 0)
            {
                // 장착된 슬롯 탭 → 장착해제 (선택 모드였으면 종료)
                mgr.Unequip(slotIndex);
                ExitPickingMode();
                Refresh();
            }
            else
            {
                // 빈 슬롯 탭 → 선택 모드 진입 (다른 슬롯을 이미 고르던 상태면 대상만 변경)
                _selectedSlot = slotIndex;
                _pickingMode = true;
                Refresh();
            }
        }

        private static void ExitPickingMode()
        {
            _pickingMode = false;
            StopPulse();
        }

        // ─── 펄스 애니메이션 ───
        // 선택 모드에서 장착 가능 아이템들의 alpha 를 주기적으로 토글.
        // (USS transition(opacity 0.7s ease-in-out) → CanvasGroup alpha 보간으로 대응)

        private static void UpdatePulseState()
        {
            if (_pickingMode && _equippableItems.Count > 0)
                StartPulse();
            else
                StopPulse();
        }

        private static void StartPulse()
        {
            if (_pulse == null) return;
            _pulse.Begin(_equippableItems);
        }

        private static void StopPulse()
        {
            if (_pulse != null)
                _pulse.Stop();
        }

        private static void MakeCircle(Image img)
        {
            if (img == null) return;
            var catalog = UIManager.Instance != null ? UIManager.Instance.Catalog : null;
            if (catalog == null || catalog.circle == null) return;
            img.sprite = catalog.circle;
            img.type = Image.Type.Simple;
            img.preserveAspect = true;
        }

        /// <summary>
        /// UITK IVisualElementScheduledItem 대응 — 오버레이 GO에 붙어 함께 파괴된다.
        /// PulseIntervalMs(700ms)마다 목표 alpha 를 토글하고 0.7s 에 걸쳐 부드럽게 보간.
        /// </summary>
        private sealed class PulseDriver : MonoBehaviour
        {
            private const float IntervalSec = 0.7f;   // PulseIntervalMs = 700
            private const float FadeSec = 0.7f;       // .mt-inv-item transition-duration
            private const float DimAlpha = 0.35f;     // .mt-inv-item-pulse-dim

            private readonly List<CanvasGroup> _targets = new List<CanvasGroup>();
            private bool _running;
            private bool _dim;
            private float _timer;

            public void Begin(List<CanvasGroup> targets)
            {
                Stop();
                _targets.AddRange(targets);
                _running = true;
                _dim = false;
                _timer = 0f;
            }

            public void Stop()
            {
                _running = false;
                for (int i = 0; i < _targets.Count; i++)
                {
                    if (_targets[i] != null)
                        _targets[i].alpha = 1f;
                }
                _targets.Clear();
            }

            private void Update()
            {
                if (!_running) return;

                _timer += Time.unscaledDeltaTime;
                if (_timer >= IntervalSec)
                {
                    _timer -= IntervalSec;
                    _dim = !_dim;
                }

                float target = _dim ? DimAlpha : 1f;
                float maxDelta = (1f - DimAlpha) / FadeSec * Time.unscaledDeltaTime;
                for (int i = 0; i < _targets.Count; i++)
                {
                    var cg = _targets[i];
                    if (cg == null) continue;
                    cg.alpha = Mathf.MoveTowards(cg.alpha, target, maxDelta);
                }
            }
        }
    }
}
