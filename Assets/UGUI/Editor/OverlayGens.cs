using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace KingdomIdle.UGUI.Editor
{
    /// <summary>로딩 / 토스트 / 설정 모달 / 가챠 결과 팝업 프리팹 생성기.</summary>
    internal static class OverlayGens
    {
        // ═══ 로딩 오버레이 (Overlay_Loading.uxml 대응) ═══
        internal static GameObject GenerateLoading()
        {
            var root = F.Root("Overlay_Loading");
            var view = root.gameObject.AddComponent<LoadingOverlayView>();

            var dim = F.Box(root, "Dim", UguiTheme.DimHeavy, rounded: false, raycast: true);
            F.Stretch(dim.rectTransform);

            var box = F.PixelPanel(root, "Box",
                F.Catalog != null ? F.Catalog.kitWindow : null, F.FrameGold, 24f, baseColor: F.PanelBaseDarker);
            F.AnchorCenter(box.rectTransform, 620f, 220f);
            F.VLayout(box.gameObject, 18f, new RectOffset(40, 40, 34, 34), TextAnchor.MiddleCenter);

            var lbl = F.Text(box.transform, "LblLoading", "Loading...", 30f, UguiTheme.TextPrimary,
                TextAlignmentOptions.Center, bold: true);
            F.Preferred(lbl, height: 44f);
            view.lblLoading = lbl;

            var slider = F.SimpleSlider(box.transform, "PbLoading", new Color(1f, 1f, 1f, 0.12f),
                UguiTheme.TimerAmber, interactable: false);
            F.Preferred((RectTransform)slider.transform, height: 24f);
            view.progressBar = slider;

            return PrefabGenUtil.SavePrefab(root.gameObject, $"{PrefabGenUtil.PrefabRoot}/Overlays/Overlay_Loading.prefab");
        }

        // ═══ 토스트 (레이캐스트 비대상 — 입력을 막지 않음) ═══
        internal static GameObject GenerateToast()
        {
            var root = F.Root("Overlay_Toast");
            var view = root.gameObject.AddComponent<ToastView>();

            var box = F.Box(root, "Box", UguiTheme.ToastBg, rounded: true);
            box.raycastTarget = false;
            F.AnchorCenter(box.rectTransform, 0f, 0f);
            F.VLayout(box.gameObject, 0f, new RectOffset(22, 22, 16, 16), TextAnchor.MiddleCenter);
            var fitter = box.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var lbl = F.Text(box.transform, "Label", "", 24f, UguiTheme.TextPrimary, TextAlignmentOptions.Center);
            view.label = lbl;

            return PrefabGenUtil.SavePrefab(root.gameObject, $"{PrefabGenUtil.PrefabRoot}/Overlays/Overlay_Toast.prefab");
        }

        // ═══ 설정 모달 (.settings-*) ═══
        internal static GameObject GenerateSettings()
        {
            var root = F.Root("Overlay_Settings");
            var view = root.gameObject.AddComponent<SettingsModalView>();

            // 바깥 딤 = 탭 시 닫기
            var dim = F.Box(root, "Dim", UguiTheme.DimMedium, rounded: false, raycast: true);
            F.Stretch(dim.rectTransform);
            var dimBtn = dim.gameObject.AddComponent<Button>();
            dimBtn.targetGraphic = dim;
            dimBtn.transition = Selectable.Transition.None;
            view.outsideCatcher = dimBtn;

            // 닫기 힌트 (bottom 38)
            var hint = F.Text(root, "HintClose", "터치해서 닫기", 22f, new Color(1f, 1f, 1f, 0.65f), TextAlignmentOptions.Center);
            var hintRt = hint.rectTransform;
            hintRt.anchorMin = new Vector2(0f, 0f);
            hintRt.anchorMax = new Vector2(1f, 0f);
            hintRt.pivot = new Vector2(0.5f, 0f);
            hintRt.anchoredPosition = new Vector2(0f, 38f);
            hintRt.sizeDelta = new Vector2(0f, 30f);

            // 패널 — 어두운 배경 + 픽셀 윈도우 프레임
            var panel = F.PixelPanel(root, "Panel",
                F.Catalog != null ? F.Catalog.kitWindow : null, F.FrameGold, 24f, raycast: true,
                baseColor: F.PanelBaseDarker);
            F.AnchorCenter(panel.rectTransform, 860f, 0f);
            F.VLayout(panel.gameObject, 10f, new RectOffset(30, 30, 26, 28));
            var panelFitter = panel.gameObject.AddComponent<ContentSizeFitter>();
            panelFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            view.panel = panel.rectTransform;
            F.CornerBrackets(panel.transform);

            // 헤더(데모 스타일): LL 리본 배너에 얹은 중앙 제목
            F.HeaderBanner(panel.transform, "환경설정");

            // 정보 행: 서버 + 버전
            var infoRow = F.Container(panel.transform, "InfoRow");
            F.HLayout(infoRow.gameObject, 8f, null, TextAnchor.MiddleLeft);
            F.Preferred(infoRow.gameObject.AddComponent<LayoutElement>(), height: 44f);

            var lblServer = F.Text(infoRow, "LblServer", "현재 서버: null", 22f, new Color(1f, 1f, 1f, 0.65f));
            F.Flexible(lblServer, flexWidth: 1f);
            view.lblServer = lblServer;

            var lblVersion = F.Text(infoRow, "LblVersion", "Version 0.0.1", 22f, new Color(1f, 1f, 1f, 0.65f),
                TextAlignmentOptions.Right);
            F.Flexible(lblVersion, flexWidth: 1f);
            view.lblVersion = lblVersion;

            // Google Play 연동 칩
            view.btnGoogleChip = F.TextButton(panel.transform, "BtnGoogleChip", "Google Play 연동됨", 24f,
                UguiTheme.SurfaceMid, out _, bold: false);
            F.Preferred((RectTransform)view.btnGoogleChip.transform, height: 60f);

            // 토글 그리드 (2열 × 3행)
            var grid = F.Container(panel.transform, "ToggleGrid");
            F.HLayout(grid.gameObject, 10f, null, TextAnchor.UpperLeft, expandWidth: true);

            var colL = F.Container(grid, "ColL");
            F.VLayout(colL.gameObject, 8f);
            F.Flexible(colL.gameObject.AddComponent<LayoutElement>(), flexWidth: 1f);

            var colR = F.Container(grid, "ColR");
            F.VLayout(colR.gameObject, 8f);
            F.Flexible(colR.gameObject.AddComponent<LayoutElement>(), flexWidth: 1f);

            view.tglPowerSave = MakeToggleRow(colL, "절전 모드");
            view.tglDamageText = MakeToggleRow(colL, "데미지 문구 출력");
            view.tglPush = MakeToggleRow(colL, "푸시 동의");

            view.tglHideItem = MakeToggleRow(colR, "아이템 획득 숨기기");
            view.tglScreenShake = MakeToggleRow(colR, "화면 흔들림 켜기");
            view.tglNightPush = MakeToggleRow(colR, "[야간] 푸시 동의");

            // 음량 행
            var volRow = F.Box(panel.transform, "VolumeRow", UguiTheme.SurfaceFaint, rounded: true);
            F.Preferred(volRow, height: 64f);
            F.HLayout(volRow.gameObject, 12f, new RectOffset(14, 14, 10, 10), TextAnchor.MiddleLeft);

            var volLbl = F.Text(volRow.transform, "Label", "전체 음량", 22f, UguiTheme.TextPrimary);
            F.Preferred(volLbl, width: 120f, height: 32f);

            var muteBg = F.Box(volRow.transform, "BtnMute", UguiTheme.SurfaceMid, rounded: true, raycast: true);
            F.Preferred(muteBg, width: 100f, height: 44f);
            view.btnMute = F.ButtonOn(muteBg);
            view.btnMuteBg = muteBg;
            var muteLbl = F.Text(muteBg.transform, "Label", "음소거", 20f, UguiTheme.TextPrimary, TextAlignmentOptions.Center);
            F.Stretch(muteLbl.rectTransform);

            var slider = F.SimpleSlider(volRow.transform, "SldVolume", new Color(1f, 1f, 1f, 0.12f),
                UguiTheme.TimerAmber, interactable: true);
            F.Flexible((RectTransform)slider.transform, flexWidth: 1f);
            F.Preferred((RectTransform)slider.transform, height: 40f);
            view.sldVolume = slider;

            // 문의
            var inquiry = F.Text(panel.transform, "Inquiry", "문의: ", 20f, new Color(1f, 1f, 1f, 0.55f));
            F.Preferred(inquiry, height: 30f);

            // 회원 탈퇴
            view.btnWithdraw = F.TextButton(panel.transform, "BtnWithdraw", "회원 탈퇴 & 계정 삭제", 22f,
                new Color(120f / 255f, 30f / 255f, 30f / 255f, 0.35f), out var withdrawLbl, bold: false);
            withdrawLbl.color = new Color(1f, 130f / 255f, 130f / 255f, 1f);
            F.Preferred((RectTransform)view.btnWithdraw.transform, height: 52f);

            // 하단 버튼 행
            var bottomRow = F.Container(panel.transform, "BottomRow");
            F.HLayout(bottomRow.gameObject, 12f, null, TextAnchor.MiddleCenter, expandWidth: true);
            F.Preferred(bottomRow.gameObject.AddComponent<LayoutElement>(), height: 64f);

            view.btnSave = F.TextButton(bottomRow, "BtnSave", "저장하기", 24f, UguiTheme.SurfaceMid, out _);
            F.Flexible((RectTransform)view.btnSave.transform, flexWidth: 1f);

            view.btnSaveClose = F.TextButton(bottomRow, "BtnSaveClose", "저장 후 닫기", 24f, UguiTheme.BtnConfirm, out _);
            F.Flexible((RectTransform)view.btnSaveClose.transform, flexWidth: 1f);

            return PrefabGenUtil.SavePrefab(root.gameObject, $"{PrefabGenUtil.PrefabRoot}/Overlays/Overlay_Settings.prefab");
        }

        /// <summary>설정 토글 행 (라벨 + 우측 토글, bg white@6%).</summary>
        private static Toggle MakeToggleRow(RectTransform parent, string label)
        {
            var row = F.Box(parent, $"Row_{label}", UguiTheme.SurfaceFaint, rounded: true);
            F.Preferred(row, height: 60f);
            F.HLayout(row.gameObject, 10f, new RectOffset(14, 14, 12, 12), TextAnchor.MiddleLeft);

            var lbl = F.Text(row.transform, "Label", label, 22f, UguiTheme.TextPrimary);
            F.Flexible(lbl, flexWidth: 1f);

            var toggle = F.SimpleToggle(row.transform, "Toggle", 34f);
            F.Preferred((RectTransform)toggle.transform, width: 54f, height: 34f);
            return toggle;
        }

        // ═══ 가챠 결과 팝업 (.gacha-result-*) ═══
        internal static GameObject GenerateGachaResult()
        {
            var root = F.Root("Popup_GachaResult");
            var view = root.gameObject.AddComponent<GachaResultPopupView>();

            // 딤 (외부 클릭 차단 — 원본과 동일하게 바깥 탭으로 닫지 않음)
            var dim = F.Box(root, "Dim", UguiTheme.DimHeavy, rounded: false, raycast: true);
            F.Stretch(dim.rectTransform);

            // 팝업 본체 — 어두운 배경 패널 (가챠 특별감은 금색 타이틀/보상 카드로 표현)
            var popup = F.PixelPanel(root, "Popup",
                F.Catalog != null ? F.Catalog.kitWindow : null,
                F.FrameGold, 24f, raycast: true, baseColor: F.PanelBaseDarker);
            F.AnchorCenter(popup.rectTransform, 700f, 1150f);
            F.VLayout(popup.gameObject, 14f, new RectOffset(28, 28, 26, 26));
            view.box = popup.rectTransform;
            F.CornerBrackets(popup.transform);

            // 제목 — LL 리본 배너 (가챠 특별감: 금색 제목)
            var title = F.HeaderBanner(popup.transform, "뽑기 결과");
            title.color = UguiTheme.AccentGoldStrong;
            view.title = title;

            // 결과 그리드 스크롤
            var scroll = F.VScroll(popup.transform, "Scroll", out var scrollContent, spacing: 8f);
            F.Flexible(scroll.gameObject.AddComponent<LayoutElement>(), flexHeight: 1f);
            view.scroll = scroll;

            var grid = F.Container(scrollContent, "Grid");
            var gridLayout = grid.gameObject.AddComponent<GridLayoutGroup>();
            gridLayout.cellSize = new Vector2(126f, 160f);
            gridLayout.spacing = new Vector2(10f, 10f);
            gridLayout.childAlignment = TextAnchor.UpperCenter;
            gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gridLayout.constraintCount = 5;
            view.grid = grid;

            // 하단 버튼 행
            var btnRow = F.Container(popup.transform, "BtnRow");
            F.HLayout(btnRow.gameObject, 10f, null, TextAnchor.MiddleCenter, expandWidth: true);
            F.Preferred(btnRow.gameObject.AddComponent<LayoutElement>(), height: 56f);
            view.buttonRow = btnRow;

            // 완료=닫기(다크 우드), 다시 뽑기=재화 소모(스펜드 크림슨)
            view.btnDone = F.TextButton(btnRow, "BtnDone", "완료", 22f, UguiTheme.BtnCancel, out _);
            F.Flexible((RectTransform)view.btnDone.transform, flexWidth: 1f);

            view.btnRePull1 = F.TextButton(btnRow, "BtnRePull1", "다시 뽑기 x1", 22f, UguiTheme.BtnSpend, out var rePull1Lbl);
            F.Flexible((RectTransform)view.btnRePull1.transform, flexWidth: 1f);
            view.btnRePull1Label = rePull1Lbl;

            view.btnRePullN = F.TextButton(btnRow, "BtnRePullN", "다시 뽑기 xN", 22f, UguiTheme.BtnSpend, out var rePullNLbl);
            F.Flexible((RectTransform)view.btnRePullN.transform, flexWidth: 1f);
            view.btnRePullNLabel = rePullNLbl;

            return PrefabGenUtil.SavePrefab(root.gameObject, $"{PrefabGenUtil.PrefabRoot}/Popups/Popup_GachaResult.prefab");
        }
    }
}
