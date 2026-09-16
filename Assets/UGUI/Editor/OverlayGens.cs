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
            popup.rectTransform.anchorMin = new Vector2(.5f, .08f);
            popup.rectTransform.anchorMax = new Vector2(.5f, .92f);
            popup.rectTransform.sizeDelta = new Vector2(980f, 0f);
            popup.gameObject.AddComponent<ModalSizeFitter>();
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
            F.Preferred(btnRow.gameObject.AddComponent<LayoutElement>(), height: 144f);
            view.buttonRow = btnRow;

            // Cost stays visible on the second line; all actions have a mobile touch target.
            view.btnDone = F.TextButton(btnRow, "BtnDone", "완료", 28f, UguiTheme.RusticSurface, out _);
            F.Flexible((RectTransform)view.btnDone.transform, flexWidth: 1f);

            view.btnRePull1 = F.TextButton(btnRow, "BtnRePull1", "1회 다시 뽑기", 26f, UguiTheme.RusticSurface, out var rePull1Lbl);
            F.Flexible((RectTransform)view.btnRePull1.transform, flexWidth: 1f);
            view.btnRePull1Label = rePull1Lbl;

            view.btnRePullN = F.TextButton(btnRow, "BtnRePullN", "10회 다시 뽑기", 26f, UguiTheme.RusticSurface, out var rePullNLbl);
            F.Flexible((RectTransform)view.btnRePullN.transform, flexWidth: 1f);
            view.btnRePullNLabel = rePullNLbl;
            foreach (var button in new[] {view.btnDone, view.btnRePull1, view.btnRePullN})
            {
                F.Preferred((RectTransform)button.transform, height: 144f);
                var label = button.GetComponentInChildren<TMP_Text>();
                label.textWrappingMode = TextWrappingModes.Normal;
                label.overflowMode = TextOverflowModes.Overflow;
                label.color = UguiTheme.Parchment;
                if (button == view.btnDone) continue;
                var background = button.GetComponent<Image>();
                background.sprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UGUI/Art/Gacha/GachaBronzeButton.png");
                background.type = Image.Type.Sliced; background.color = Color.white;
                foreach (Transform child in button.transform) if (child.GetComponent<TMP_Text>() == null) child.gameObject.SetActive(false);
                ItemGens.AddGachaFlare(button.transform);
            }

            return PrefabGenUtil.SavePrefab(root.gameObject, $"{PrefabGenUtil.PrefabRoot}/Popups/Popup_GachaResult.prefab");
        }
    }
}
