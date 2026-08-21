using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace KingdomIdle.UGUI.Editor
{
    /// <summary>
    /// 프로필 팝업(더미) 생성기 — 상용 아이들 게임 공통 구성 + 게임 고유 요소.
    /// 러스틱 테마 통일. 서버 미연동(플레이스홀더 값). 프리팹: Popup_Profile.prefab.
    /// </summary>
    internal static class ProfilePopupPrefabGens
    {
        private static readonly Color CardBg = new Color(0.13f, 0.10f, 0.075f, 1f);

        internal static GameObject GenerateProfilePopup()
        {
            var root = F.Root("Popup_Profile");
            var view = root.gameObject.AddComponent<ProfilePopupView>();

            // 딤 배경 (탭하면 닫기)
            var dim = F.Box(root, "Dim", UguiTheme.DimHeavy, rounded: false, raycast: true);
            F.Stretch(dim.rectTransform);
            var dimBtn = dim.gameObject.AddComponent<Button>();
            dimBtn.targetGraphic = dim; dimBtn.transition = Selectable.Transition.None;
            view.backdrop = dimBtn;

            // 패널 (러스틱 프레임 + 그라디언트)
            var panel = F.PixelPanel(root, "Panel",
                F.Catalog != null ? F.Catalog.kitWindow : null, F.FrameGold, 24f, raycast: true,
                baseColor: F.PanelBaseDarker);
            F.AnchorCenter(panel.rectTransform, 920f, 0f);
            var fitter = panel.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            F.VLayout(panel.gameObject, 16f, new RectOffset(38, 38, 26, 34));
            view.panel = panel.rectTransform;
            F.CornerBrackets(panel.transform);

            // 헤더 배너
            F.HeaderBanner(panel.transform, "프로필");

            // 닫기 (우상단 원형)
            view.closeButton = MakeCloseButton(panel.transform);

            // ── 아이덴티티 행 (아바타 + 이름/레벨/XP) ──
            var idRow = F.Container(panel.transform, "IdentityRow");
            F.HLayout(idRow.gameObject, 20f, new RectOffset(4, 4, 6, 6), TextAnchor.MiddleLeft);
            F.Preferred(idRow.gameObject.AddComponent<LayoutElement>(), height: 200f);

            // 아바타 (청동 링 + 소켓 + 초상화) + 레벨 배지
            var avatarRing = F.CircleBox(idRow, "AvatarRing", UguiTheme.Bronze, raycast: false);
            F.Preferred(avatarRing, width: 180f, height: 180f);
            var avatarSocket = F.CircleBox(avatarRing.transform, "Socket", new Color(0.16f, 0.13f, 0.10f, 1f), raycast: false);
            F.AnchorCenter(avatarSocket.rectTransform, 158f, 158f);
            var avatarImg = F.IconImage(avatarSocket.transform, "Avatar", UguiGenAssets.IconUser, 120f, 120f);
            F.AnchorCenter(avatarImg.rectTransform, 120f, 120f);
            avatarImg.enabled = true;
            view.avatar = avatarImg;
            // 레벨 훈장 배지
            var lvBadge = F.Container(avatarRing.transform, "LevelBadge");
            var lvImg = lvBadge.gameObject.AddComponent<Image>();
            lvImg.sprite = UguiGenAssets.BadgeCrimped; lvImg.color = new Color(0.95f, 0.72f, 0.24f, 1f); lvImg.preserveAspect = true; lvImg.raycastTarget = false;
            lvBadge.anchorMin = new Vector2(0.5f, 0f); lvBadge.anchorMax = new Vector2(0.5f, 0f); lvBadge.pivot = new Vector2(0.5f, 0.5f);
            lvBadge.anchoredPosition = new Vector2(0f, 6f); lvBadge.sizeDelta = new Vector2(72f, 72f);
            view.levelLabel = F.Text(lvBadge, "Lv", "1", 32f, new Color(0.16f, 0.11f, 0.02f, 1f), TextAlignmentOptions.Center, bold: true);
            F.Stretch(view.levelLabel.rectTransform);

            // 정보 열
            var infoCol = F.Container(idRow, "InfoCol");
            F.VLayout(infoCol.gameObject, 10f, null, TextAnchor.UpperLeft);
            F.Flexible(infoCol, flexWidth: 1f);

            // 이름 + 편집 연필
            var nameRow = F.Container(infoCol, "NameRow");
            F.HLayout(nameRow.gameObject, 10f, null, TextAnchor.MiddleLeft);
            F.Preferred(nameRow.gameObject.AddComponent<LayoutElement>(), height: 48f);
            view.nameLabel = F.Text(nameRow, "Name", "닉네임", 38f, UguiTheme.Parchment, TextAlignmentOptions.Left, bold: true);
            F.Preferred(view.nameLabel, width: 320f, height: 48f);
            var editImg = F.Box(nameRow, "BtnEdit", UguiTheme.RusticSurface, rounded: true, raycast: true);
            F.Preferred(editImg, width: 52f, height: 52f);
            view.editNameButton = F.ButtonOn(editImg);
            var editIcon = F.IconImage(editImg.transform, "Icon", UguiGenAssets.LL("edit"), 30f, 30f);
            F.AnchorCenter(editIcon.rectTransform, 30f, 30f);

            // ID
            view.idLabel = F.Text(infoCol, "IdLabel", "ID: 00000000", 22f, UguiTheme.TextTertiary, TextAlignmentOptions.Left);
            F.Preferred(view.idLabel, height: 28f);

            // XP 바
            var xpWrap = F.Container(infoCol, "XpWrap");
            F.HLayout(xpWrap.gameObject, 10f, null, TextAnchor.MiddleLeft);
            F.Preferred(xpWrap.gameObject.AddComponent<LayoutElement>(), height: 40f);
            var xpFill = F.HFillBar(xpWrap, "XpBar", F.TrackDark, new Color(0.95f, 0.72f, 0.24f, 1f), out var xpTrack);
            F.Flexible(xpTrack, flexWidth: 1f); F.Preferred(xpTrack, height: 28f);
            xpFill.fillAmount = 0.45f;
            view.xpFill = xpFill;
            view.xpLabel = F.Text(xpTrack.transform, "XpLabel", "45 / 100", 20f, UguiTheme.TextPrimary, TextAlignmentOptions.Center, bold: true);
            F.Stretch(view.xpLabel.rectTransform);

            F.DecoDivider(panel.transform);

            // ── 요약 알약 (전투력 / 트로피) ──
            var pillRow = F.Container(panel.transform, "SummaryPills");
            F.HLayout(pillRow.gameObject, 14f, null, TextAnchor.MiddleCenter, expandWidth: true);
            F.Preferred(pillRow.gameObject.AddComponent<LayoutElement>(), height: 84f);
            view.powerLabel = MakeSummaryPill(pillRow, UguiGenAssets.IconPower, "전투력", "12,480");
            view.powerButton = MakeSummaryPillButton(view.powerLabel);
            view.trophyLabel = MakeSummaryPill(pillRow, UguiGenAssets.IconTrophy, "트로피", "3,600");

            // 길드 행
            var guildRow = MakeInfoRow(panel.transform, "길드", out var guildVal);
            view.guildLabel = guildVal; guildVal.text = "길드 없음";

            // ── 시즌 리그 카드 ──
            F.DecoDivider(panel.transform);
            var league = F.Box(panel.transform, "LeagueCard", CardBg, rounded: true);
            F.HLayout(league.gameObject, 18f, new RectOffset(20, 20, 14, 14), TextAnchor.MiddleLeft);
            F.Preferred(league.gameObject.AddComponent<LayoutElement>(), height: 140f);
            var leagueFrame = F.Frame(league.transform, "Frame", UguiTheme.Bronze);
            leagueFrame.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            var emblem = F.IconImage(league.transform, "Emblem", UguiGenAssets.IconCrown, 96f, 96f);
            F.Preferred(emblem, width: 96f, height: 96f);
            view.leagueEmblem = emblem;
            var leagueCol = F.Container(league.transform, "LeagueCol");
            F.VLayout(leagueCol.gameObject, 4f, null, TextAnchor.MiddleLeft);
            F.Flexible(leagueCol, flexWidth: 1f);
            F.Text(leagueCol, "Caption", "현재 시즌", 20f, UguiTheme.TextTertiary, TextAlignmentOptions.Left);
            view.leagueLabel = F.Text(leagueCol, "League", "브론즈 리그", 30f, UguiTheme.AccentGold, TextAlignmentOptions.Left, bold: true);
            view.leagueTrophyLabel = F.Text(leagueCol, "Trophy", "트로피 3,600", 24f, UguiTheme.TextSecondary, TextAlignmentOptions.Left);

            // ── 스탯 그리드 2×3 ──
            F.DecoDivider(panel.transform);
            var grid = F.Container(panel.transform, "StatsGrid");
            var gl = grid.gameObject.AddComponent<GridLayoutGroup>();
            gl.cellSize = new Vector2(272f, 96f); gl.spacing = new Vector2(12f, 12f);
            gl.constraint = GridLayoutGroup.Constraint.FixedColumnCount; gl.constraintCount = 3;
            gl.childAlignment = TextAnchor.MiddleCenter;
            F.Preferred(grid.gameObject.AddComponent<LayoutElement>(), height: 204f);

            string[] labels = { "스테이지 클리어", "랭킹", "승리", "퍼펙트 승", "몬스터 처치", "최고 리그" };
            string[] samples = { "1-1", "—", "0", "0", "0", "브론즈" };
            view.statValues = new TMP_Text[labels.Length];
            for (int i = 0; i < labels.Length; i++)
                view.statValues[i] = MakeStatCell(grid.transform, labels[i], samples[i]);

            // ── 게임 고유 요소 (왕국 레벨 / 보유 전직) ──
            F.DecoDivider(panel.transform);
            var uniqueRow = F.Container(panel.transform, "UniqueRow");
            F.HLayout(uniqueRow.gameObject, 14f, null, TextAnchor.MiddleCenter, expandWidth: true);
            F.Preferred(uniqueRow.gameObject.AddComponent<LayoutElement>(), height: 84f);
            view.kingdomLevelLabel = MakeSummaryPill(uniqueRow, UguiGenAssets.IconStageMap, "왕국 레벨", "Lv. 1");
            view.totalJobsLabel = MakeSummaryPill(uniqueRow, UguiGenAssets.IconHelmet, "보유 전직", "1 / 7");

            root.gameObject.SetActive(false);
            return PrefabGenUtil.SavePrefab(root.gameObject, $"{PrefabGenUtil.PrefabRoot}/Popups/Popup_Profile.prefab");
        }

        private static Button MakeCloseButton(Transform parent)
        {
            var closeImg = F.CircleBox(parent, "BtnClose", new Color(0.62f, 0.24f, 0.24f, 1f), raycast: true);
            var rt = closeImg.rectTransform;
            rt.anchorMin = new Vector2(1f, 1f); rt.anchorMax = new Vector2(1f, 1f); rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-10f, -10f); rt.sizeDelta = new Vector2(70f, 70f);
            closeImg.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            var btn = closeImg.gameObject.AddComponent<Button>();
            btn.targetGraphic = closeImg; btn.transition = Selectable.Transition.ColorTint; btn.colors = UguiTheme.MakeColorBlock();
            closeImg.gameObject.AddComponent<PlayClickSfxOnClick>();
            if (F.Catalog != null && F.Catalog.iconX != null)
            {
                var x = F.IconImage(closeImg.transform, "Icon", F.Catalog.iconX, 34f, 34f);
                F.AnchorCenter(x.rectTransform, 34f, 34f);
            }
            return btn;
        }

        private static TMP_Text MakeSummaryPill(Transform parent, Sprite icon, string caption, string value)
        {
            var pill = F.Box(parent, "Pill", CardBg, rounded: true);
            F.HLayout(pill.gameObject, 10f, new RectOffset(16, 18, 0, 0), TextAnchor.MiddleLeft);
            F.Flexible(pill, flexWidth: 1f);
            var frame = F.Frame(pill.transform, "Frame", new Color(UguiTheme.Bronze.r, UguiTheme.Bronze.g, UguiTheme.Bronze.b, 0.6f));
            frame.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            var ic = F.IconImage(pill.transform, "Icon", icon, 48f, 48f);
            F.Preferred(ic, width: 48f, height: 48f);
            var col = F.Container(pill.transform, "Col");
            F.VLayout(col.gameObject, 0f, null, TextAnchor.MiddleLeft);
            F.Flexible(col, flexWidth: 1f);
            F.Text(col, "Caption", caption, 18f, UguiTheme.TextTertiary, TextAlignmentOptions.Left);
            var val = F.Text(col, "Value", value, 28f, UguiTheme.TextPrimary, TextAlignmentOptions.Left, bold: true);
            return val;
        }

        /// <summary>전투력 Pill 전체를 클릭 가능한 영역으로 만든다.</summary>
        private static Button MakeSummaryPillButton(TMP_Text valueLabel)
        {
            var pillImage = valueLabel.transform.parent.parent.GetComponent<Image>();
            pillImage.raycastTarget = true;

            var button = pillImage.gameObject.AddComponent<Button>();
            button.targetGraphic = pillImage;
            button.transition = Selectable.Transition.ColorTint;
            button.colors = UguiTheme.MakeColorBlock();
            pillImage.gameObject.AddComponent<PlayClickSfxOnClick>();
            return button;
        }

        private static TMP_Text MakeInfoRow(Transform parent, string label, out TMP_Text value)
        {
            var row = F.Container(parent, "InfoRow");
            F.HLayout(row.gameObject, 10f, new RectOffset(6, 6, 0, 0), TextAnchor.MiddleLeft);
            F.Preferred(row.gameObject.AddComponent<LayoutElement>(), height: 44f);
            F.Text(row, "Label", label, 24f, UguiTheme.TextTertiary, TextAlignmentOptions.Left, bold: true);
            value = F.Text(row, "Value", "-", 24f, UguiTheme.TextPrimary, TextAlignmentOptions.Right);
            F.Flexible(value, flexWidth: 1f);
            return value;
        }

        private static TMP_Text MakeStatCell(Transform parent, string label, string value)
        {
            var cell = F.Box(parent, "StatCell", CardBg, rounded: true);
            F.VLayout(cell.gameObject, 2f, new RectOffset(8, 8, 8, 8), TextAnchor.MiddleCenter);
            var frame = F.Frame(cell.transform, "Frame", new Color(UguiTheme.Bronze.r, UguiTheme.Bronze.g, UguiTheme.Bronze.b, 0.45f));
            frame.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            F.Text(cell.transform, "Label", label, 18f, UguiTheme.TextTertiary, TextAlignmentOptions.Center);
            var val = F.Text(cell.transform, "Value", value, 28f, UguiTheme.TextPrimary, TextAlignmentOptions.Center, bold: true);
            return val;
        }
    }
}
