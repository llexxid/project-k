using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace KingdomIdle.UGUI.Editor
{
    /// <summary>
    /// 런타임 코드생성 UI → 프리팹 전환용 생성기.
    /// 1차: 마탑 스킬 장착 팝업(Panel_MageTowerEquip) + 슬롯/셀 아이템 프리팹.
    /// </summary>
    internal static class PopupGens
    {
        // ── 좌측 장착 슬롯 셀 ──
        internal static GameObject GenerateMageEquipSlot()
        {
            var border = F.Box(null, "Item_MageEquipSlot", new Color(1f, 1f, 1f, 0.20f), rounded: true, raycast: true);
            F.Preferred(border, width: 100f, height: 100f);
            var view = border.gameObject.AddComponent<MageEquipSlotView>();
            view.borderImage = border;

            var btn = border.gameObject.AddComponent<Button>();
            btn.targetGraphic = border;
            btn.transition = Selectable.Transition.ColorTint;
            btn.colors = UguiTheme.MakeColorBlock();
            border.gameObject.AddComponent<PlayClickSfxOnClick>();
            view.button = btn;

            var bg = F.Box(border.transform, "Bg", UguiTheme.SurfaceLight, rounded: true);
            F.Stretch(bg.rectTransform);
            bg.rectTransform.offsetMin = new Vector2(2f, 2f);
            bg.rectTransform.offsetMax = new Vector2(-2f, -2f);

            var icon = F.IconImage(bg.transform, "Icon", null, 92f, 92f);
            F.AnchorCenter(icon.rectTransform, 92f, 92f);
            view.icon = icon;

            var lbl = F.Text(bg.transform, "Label", "-", 18f, new Color(1f, 1f, 1f, 0.3f), TextAlignmentOptions.Center);
            F.Stretch(lbl.rectTransform);
            view.label = lbl;

            return PrefabGenUtil.SavePrefab(border.gameObject, $"{PrefabGenUtil.PrefabRoot}/Items/Item_MageEquipSlot.prefab");
        }

        // ── 우측 보유 스킬 그리드 셀 ──
        internal static GameObject GenerateMageSkillCell()
        {
            var frame = F.Box(null, "Item_MageSkillCell", Color.clear, rounded: true, raycast: true);
            F.Preferred(frame, width: 110f, height: 130f);
            var view = frame.gameObject.AddComponent<MageSkillCellView>();
            view.frameImage = frame;
            view.canvasGroup = frame.gameObject.AddComponent<CanvasGroup>();

            var bg = F.Box(frame.transform, "Bg", UguiTheme.SurfaceLight, rounded: true);
            F.Stretch(bg.rectTransform);
            bg.rectTransform.offsetMin = new Vector2(2f, 2f);
            bg.rectTransform.offsetMax = new Vector2(-2f, -2f);
            F.VLayout(bg.gameObject, 4f, new RectOffset(6, 6, 6, 6), TextAnchor.MiddleCenter);
            view.background = bg;

            var btn = frame.gameObject.AddComponent<Button>();
            btn.targetGraphic = bg;
            btn.transition = Selectable.Transition.ColorTint;
            btn.colors = UguiTheme.MakeColorBlock();
            frame.gameObject.AddComponent<PlayClickSfxOnClick>();
            view.button = btn;

            var icon = F.IconImage(bg.transform, "Icon", null, 60f, 60f);
            F.Preferred(icon, width: 60f, height: 60f);
            view.icon = icon;

            var nameLbl = F.Text(bg.transform, "Name", "", 18f, new Color(1f, 1f, 1f, 0.85f), TextAlignmentOptions.Center);
            F.Preferred(nameLbl, height: 24f);
            view.nameLabel = nameLbl;

            var dmg = F.Text(bg.transform, "Dmg", "", 16f, new Color(1f, 1f, 1f, 0.6f), TextAlignmentOptions.Center);
            F.Preferred(dmg, height: 22f);
            view.dmgLabel = dmg;

            return PrefabGenUtil.SavePrefab(frame.gameObject, $"{PrefabGenUtil.PrefabRoot}/Items/Item_MageSkillCell.prefab");
        }

        // ── 팝업 셸 ──
        internal static GameObject GenerateMageTowerEquipPopup()
        {
            var root = F.Root("Panel_MageTowerEquip");
            var view = root.gameObject.AddComponent<MageTowerEquipPopupView>();
            view.pulse = root.gameObject.AddComponent<UIPulseGroup>();

            var dim = F.Box(root, "Dim", UguiTheme.DimMedium, rounded: false, raycast: true);
            F.Stretch(dim.rectTransform);
            var dimBtn = dim.gameObject.AddComponent<Button>();
            dimBtn.targetGraphic = dim;
            dimBtn.transition = Selectable.Transition.None;
            view.backdropButton = dimBtn;

            var panel = F.PixelPanel(root, "Panel", F.Catalog != null ? F.Catalog.kitWindow : null,
                F.FrameGold, 24f, raycast: true, baseColor: F.PanelBaseDarker);
            F.AnchorCenter(panel.rectTransform, 900f, 680f);
            F.VLayout(panel.gameObject, 14f, new RectOffset(30, 30, 24, 28));
            view.panelBox = panel.rectTransform;

            // 타이틀바
            var titleBar = F.PixelPanel(panel.transform, "TitleBar", F.Catalog != null ? F.Catalog.kitTitleBar : null,
                new Color(0.20f, 0.22f, 0.30f, 1f), 14f, frameOnly: false);
            F.HLayout(titleBar.gameObject, 8f, new RectOffset(18, 10, 6, 6), TextAnchor.MiddleLeft);
            F.Preferred(titleBar, height: 76f);
            var title = F.Text(titleBar.transform, "Title", "마탑 스킬 장착", 34f, UguiTheme.TextPrimary,
                TextAlignmentOptions.Left, bold: true);
            F.Flexible(title, flexWidth: 1f);
            view.titleLabel = title;

            var closeImg = F.CircleBox(titleBar.transform, "BtnClose", new Color(0.55f, 0.2f, 0.2f, 1f), raycast: true);
            F.Preferred(closeImg, width: 72f, height: 72f);
            var closeBtn = closeImg.gameObject.AddComponent<Button>();
            closeBtn.targetGraphic = closeImg;
            closeBtn.transition = Selectable.Transition.ColorTint;
            closeBtn.colors = UguiTheme.MakeColorBlock();
            closeImg.gameObject.AddComponent<PlayClickSfxOnClick>();
            view.closeButton = closeBtn;
            if (F.Catalog != null && F.Catalog.iconX != null)
            {
                var xIcon = F.IconImage(closeImg.transform, "Icon", F.Catalog.iconX, 34f, 34f);
                F.AnchorCenter(xIcon.rectTransform, 34f, 34f);
            }

            // 본문
            var body = F.Container(panel.transform, "Body");
            F.HLayout(body.gameObject, 18f, null, TextAnchor.UpperLeft);
            F.Flexible(body, flexHeight: 1f);

            // 좌: 장착 슬롯 열
            var slotsCol = F.Container(body, "SlotsCol");
            F.VLayout(slotsCol.gameObject, 10f, null, TextAnchor.UpperCenter, expandWidth: false);
            F.Preferred(slotsCol, width: 130f);
            F.Text(slotsCol, "Header", "장착 슬롯", 22f, new Color(1f, 1f, 1f, 0.75f), TextAlignmentOptions.Center);
            var slots = F.Container(slotsCol, "Slots");
            F.VLayout(slots.gameObject, 10f, null, TextAnchor.UpperCenter, expandWidth: false);
            view.slotsContainer = slots;

            // 우: 보유 스킬 스크롤 그리드
            var invCol = F.Container(body, "InvCol");
            F.VLayout(invCol.gameObject, 10f);
            F.Flexible(invCol, flexWidth: 1f, flexHeight: 1f);
            F.Text(invCol, "Header", "보유 스킬", 22f, new Color(1f, 1f, 1f, 0.75f));
            var scroll = F.VScroll(invCol, "InvScroll", out var content, spacing: 12f, padding: new RectOffset(4, 4, 4, 4));
            F.Flexible(scroll, flexHeight: 1f);
            view.invScroll = scroll;

            var grid = F.Container(content, "InvGrid");
            var gridLg = grid.gameObject.AddComponent<GridLayoutGroup>();
            gridLg.cellSize = new Vector2(110f, 130f);
            gridLg.spacing = new Vector2(12f, 12f);
            gridLg.childAlignment = TextAnchor.UpperLeft;
            gridLg.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gridLg.constraintCount = 5;
            view.invGrid = grid;

            return PrefabGenUtil.SavePrefab(root.gameObject, $"{PrefabGenUtil.PrefabRoot}/Popups/Panel_MageTowerEquip.prefab");
        }
    }
}
