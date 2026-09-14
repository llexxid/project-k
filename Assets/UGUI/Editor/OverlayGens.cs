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
        internal static GameObject GenerateSettings() => SettingsRevisionBuilder.Generate();

        // ═══ 궁극기(신성 스킬) 컷인 오버레이 ═══
        // 암전 → 일러스트 슬라이드 인 → 등급 리본 + 이름 플레이트 → 섬광 아웃.
        // 값은 DivineCutInController가 런타임에 채운다(여기서는 뼈대와 초기 상태만 만든다).
        internal static GameObject GenerateDivineCutIn()
        {
            var root = F.Root("Overlay_DivineCutIn");
            var view = root.gameObject.AddComponent<DivineCutInView>();

            // 암전 — 거의 투명하게 시작(완전 0은 컬링될 수 있어 InvisibleCatcher와 같은 0.004 사용).
            // 컷인 동안 입력을 막아 연타를 차단한다.
            var scrim = F.Box(root, "Scrim", new Color(0f, 0f, 0f, 0.004f), rounded: false, raycast: true);
            F.Stretch(scrim.rectTransform);
            view.scrim = scrim;

            // 일러스트 홀더 — 오른쪽에서 밀려 들어온다
            var holder = F.Container(root, "IllustHolder");
            F.AnchorCenter(holder, UguiTheme.DivineCutInIllustWidth, UguiTheme.DivineCutInIllustHeight,
                0f, UguiTheme.DivineCutInIllustY);
            var holderGroup = holder.gameObject.AddComponent<CanvasGroup>();
            holderGroup.alpha = 0f;
            holderGroup.blocksRaycasts = false;
            view.illustHolder = holder;
            view.illustGroup = holderGroup;

            // 아트 미도입 상태에서도 흰 박스가 나오지 않도록 sprite null이면 Image를 끈 채로 저장한다
            var illust = F.IconImage(holder, "Illust", null,
                UguiTheme.DivineCutInIllustWidth, UguiTheme.DivineCutInIllustHeight);
            F.Stretch(illust.rectTransform);
            view.illust = illust;

            // 이름 플레이트 — 등급 리본 + 카드명 + 스킬명
            var plate = F.PixelPanel(root, "Plate",
                F.Catalog != null ? F.Catalog.kitWindow : null, F.FrameGold, 20f,
                raycast: false, baseColor: F.PanelBaseDarker);
            F.AnchorCenter(plate.rectTransform, UguiTheme.DivineCutInPlateWidth, UguiTheme.DivineCutInPlateHeight,
                0f, UguiTheme.DivineCutInPlateY);
            F.VLayout(plate.gameObject, 8f, new RectOffset(28, 28, 18, 22), TextAnchor.MiddleCenter,
                expandWidth: false);
            var plateGroup = plate.gameObject.AddComponent<CanvasGroup>();
            plateGroup.alpha = 0f;
            plateGroup.blocksRaycasts = false;
            view.plate = plate.rectTransform;
            view.plateGroup = plateGroup;
            F.CornerBrackets(plate.transform);

            // 등급 리본 — 컨트롤러가 등급색으로 칠하므로 라벨은 어두운 잉크색으로 대비를 준다
            var ribbon = F.Box(plate.transform, "GradeRibbon", UguiTheme.Bronze, rounded: true);
            F.Preferred(ribbon, width: 200f, height: 46f);
            view.gradeRibbon = ribbon;
            var gradeLbl = F.Text(ribbon.transform, "Label", "영웅", UguiTheme.FontCutInGrade,
                UguiTheme.RusticPanelDeep, TextAlignmentOptions.Center, bold: true);
            F.Stretch(gradeLbl.rectTransform);
            view.gradeLabel = gradeLbl;

            var nameLbl = F.Text(plate.transform, "Name", "", UguiTheme.FontCutInName, UguiTheme.Parchment,
                TextAlignmentOptions.Center);
            F.Preferred(nameLbl, height: 44f);
            view.nameLabel = nameLbl;

            var skillLbl = F.Text(plate.transform, "SkillName", "", UguiTheme.FontCutInSkill,
                UguiTheme.AccentGoldStrong, TextAlignmentOptions.Center, bold: true);
            F.Preferred(skillLbl, height: 80f);
            view.skillLabel = skillLbl;

            // 마무리 섬광 — 알파 0에서 시작해 컨트롤러가 올렸다 내린다 (맨 위 형제)
            var flash = F.Box(root, "Flash", new Color(1f, 0.96f, 0.86f, 0f), rounded: false);
            F.Stretch(flash.rectTransform);
            flash.raycastTarget = false;
            view.flash = flash;

            return PrefabGenUtil.SavePrefab(root.gameObject,
                $"{PrefabGenUtil.PrefabRoot}/Overlays/Overlay_DivineCutIn.prefab");
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
