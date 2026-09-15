using System;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace KingdomIdle.UGUI.Editor
{
    public static class SettingsRevisionBuilder
    {
        [MenuItem("KingdomIdle/UGUI/Rebuild settings")]
        public static void Rebuild()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
            F.Init(); F.Catalog = AssetDatabase.LoadAssetAtPath<UIViewCatalog>(PrefabGenUtil.CatalogPath);
            Generate();
            ItemGens.GenerateCurrencyLine();
            var title = PrefabUtility.LoadPrefabContents(TitleLobbyBuilder.PrefabPath);
            try { WireTitleButton(title.GetComponent<TitleScreenView>()); PrefabUtility.SaveAsPrefabAsset(title, TitleLobbyBuilder.PrefabPath); }
            finally { PrefabUtility.UnloadPrefabContents(title); }
            AssetDatabase.SaveAssets();
        }

        internal static void WireTitleButton(TitleScreenView view)
        {
            var parent = view.presentation.languageButton.transform.parent;
            var button = parent.Find("Settings")?.GetComponent<Button>();
            if (button == null) button = F.TextButton(parent, "Settings", "설정", 30, UguiTheme.RusticSurfaceDark, out _);
            TitleLobbyBuilder.StyleButton(button, true);
            var label = button.GetComponentInChildren<TMP_Text>();
            label.fontStyle = FontStyles.Normal; label.fontSharedMaterial = F.Font.material;
            var rt = (RectTransform)button.transform;
            rt.anchorMin = rt.anchorMax = Vector2.one; rt.pivot = Vector2.one;
            rt.sizeDelta = new Vector2(144, 144); rt.anchoredPosition = new Vector2(-24, -24);
            view.btnSettings = button;
        }

        internal static GameObject Generate()
        {
            var root = F.Root("Overlay_Settings"); F.Stretch(root);
            var view = root.gameObject.AddComponent<SettingsModalView>();
            var dim = F.Box(root, "Dim", UguiTheme.DimMedium, false, true); F.Stretch(dim.rectTransform);
            view.outsideCatcher = dim.gameObject.AddComponent<Button>();
            view.outsideCatcher.targetGraphic = dim; view.outsideCatcher.transition = Selectable.Transition.None;
            var panel = F.PixelPanel(root, "Panel", F.Catalog.kitWindow, F.FrameGold, 24, raycast: true, baseColor: UguiTheme.RusticPanelDeep);
            F.AnchorCenter(panel.rectTransform, 960, 1640); view.panel = panel.rectTransform;
            F.VLayout(panel.gameObject, 12, new RectOffset(28, 28, 20, 24));
            F.CornerBrackets(panel.transform);

            var header = F.Container(panel.transform, "Header"); F.HLayout(header.gameObject, 10, null, TextAnchor.MiddleLeft);
            F.Preferred(header, height: 112);
            var title = F.Text(header, "Title", "환경설정", 40, UguiTheme.Parchment, bold: true);
            F.Flexible(title, flexWidth: 1);
            view.btnClose = F.TextButton(header, "BtnClose", "닫기", 28, UguiTheme.RusticSurface, out _);
            F.Preferred((RectTransform)view.btnClose.transform, width: 140, height: 112);
            view.lblServer = Text(panel.transform, "LblServer", "변경 즉시 적용 · 자동 저장", 26, 42);

            view.scroll = F.VScroll(panel.transform, "SettingsScroll", out var content, 12, new RectOffset(2, 12, 4, 12));
            F.Flexible((RectTransform)view.scroll.transform, flexHeight: 1);
            F.Preferred((RectTransform)view.scroll.transform, height: 300);
            var scrollTrack = F.Box(view.scroll.transform, "ScrollTrack", UguiTheme.RusticSurfaceDark);
            var trackRect = scrollTrack.rectTransform;
            trackRect.anchorMin = new Vector2(1, 0); trackRect.anchorMax = Vector2.one;
            trackRect.pivot = new Vector2(1, .5f); trackRect.sizeDelta = new Vector2(5, 0); trackRect.anchoredPosition = Vector2.zero;
            var scrollThumb = F.Box(scrollTrack.transform, "Thumb", UguiTheme.Bronze);
            F.Stretch(scrollThumb.rectTransform);
            var scrollbar = scrollTrack.gameObject.AddComponent<Scrollbar>();
            scrollbar.handleRect = scrollThumb.rectTransform; scrollbar.direction = Scrollbar.Direction.BottomToTop;
            scrollbar.interactable = false; scrollbar.transition = Selectable.Transition.None;
            view.scroll.verticalScrollbar = scrollbar; view.scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            Section(content, "숫자 표시");
            var formats = F.Container(content, "NumberFormats"); F.HLayout(formats.gameObject, 10, null, TextAnchor.MiddleCenter, expandWidth: true);
            F.Preferred(formats, height: 132);
            view.numberButtons = new Button[3];
            string[] names = { "K / M / B\n선택됨", "만 / 억 / 조\n선택", "지수 e\n선택" };
            for (int i = 0; i < 3; i++)
            {
                var button = F.TextButton(formats, "NumberStyle" + i, names[i], 28, i == 0 ? UguiTheme.Bronze : UguiTheme.RusticSurface, out var label);
                F.Flexible((RectTransform)button.transform, flexWidth: 1); F.Preferred((RectTransform)button.transform, height: 132);
                label.textWrappingMode = TextWrappingModes.Normal; view.numberButtons[i] = button;
            }
            view.numberPreview = Text(content, "NumberPreview", "표시 예시   12.34K  ·  1.23B\n재화 · 피해 · 능력치에 적용", 27, 86);
            view.numberHint = Text(content, "NumberHint", "K → M → B → T → Qa → Qi\n정확한 재화는 상단 재화를 눌러 확인", 25, 84);

            Section(content, "소리");
            view.btnMute = F.TextButton(content, "BtnMute", "음소거 끔", 30, UguiTheme.RusticSurface, out _);
            view.btnMuteBg = view.btnMute.GetComponent<Image>(); F.Preferred((RectTransform)view.btnMute.transform, height: 116);
            view.sldVolume = Volume(content, "SldVolume", "전체 음량", out view.lblVolume);
            view.sldMusic = Volume(content, "SldMusic", "배경음", out view.lblMusic);
            view.sldEffects = Volume(content, "SldEffects", "효과음", out view.lblEffects);
            Section(content, "전투 표시");
            view.tglDamageText = Toggle(content, "DamageText", "피해 숫자 표시", "전투 중 발생한 피해량 표시");
            view.tglScreenShake = Toggle(content, "ScreenShake", "화면 흔들림", "강한 스킬의 카메라 흔들림");
            view.tglHideItem = Toggle(content, "HideItem", "사냥 장비 알림 숨기기", "장비는 알림과 관계없이 획득");
            Section(content, "기기와 성능");
            view.tglPowerSave = Toggle(content, "PowerSave", "절전 모드", "화면을 초당 30프레임으로 표시");
            view.tglLowSpec = Toggle(content, "LowSpec", "저사양 모드", "장식과 피해 숫자의 움직임 간소화");
            view.tglKeepAwake = Toggle(content, "KeepAwake", "화면 켜짐 유지", "플레이 중 화면 자동 꺼짐 방지");
            Section(content, "저장 정보");
            view.btnGoogleChip = F.TextButton(content, "BtnGoogleChip", "진행 데이터는 현재 기기에 저장됩니다", 26, UguiTheme.RusticSurfaceDark, out _);
            view.btnGoogleChip.interactable = false; F.Preferred((RectTransform)view.btnGoogleChip.transform, height: 76);
            view.lblVersion = Text(content, "LblVersion", "v" + Application.version, 24, 42);
            view.btnSaveClose = F.TextButton(panel.transform, "BtnSaveClose", "완료", 36, UguiTheme.BtnConfirm, out _);
            F.Preferred((RectTransform)view.btnSaveClose.transform, height: 132);
            var result = PrefabGenUtil.SavePrefab(root.gameObject, PrefabGenUtil.PrefabRoot + "/Overlays/Overlay_Settings.prefab");
            return result;
        }
        static TMP_Text Text(Transform parent, string name, string value, float size, float height)
        {
            var text = F.Text(parent, name, value, size, UguiTheme.Parchment);
            text.textWrappingMode = TextWrappingModes.Normal; text.overflowMode = TextOverflowModes.Overflow;
            F.Preferred(text, height: height); return text;
        }
        static void Section(Transform parent, string title)
        {
            var text = Text(parent, "Section_" + title, title, 30, 66); text.color = UguiTheme.AccentGold;
            text.fontStyle = FontStyles.Bold;
        }
        static Slider Volume(Transform parent, string name, string title, out TMP_Text percent)
        {
            var row = F.Box(parent, name + "Row", UguiTheme.RusticSurfaceDark, true);
            F.HLayout(row.gameObject, 16, new RectOffset(16, 20, 0, 0), TextAnchor.MiddleLeft);
            F.Preferred(row, height: 128);
            var label = Text(row.transform, "Label", title, 28, 60); F.Preferred(label, width: 158);
            var slider = F.SimpleSlider(row.transform, name, UguiTheme.RusticSurface, UguiTheme.TimerAmber, true, scrollAware: true);
            F.Flexible((RectTransform)slider.transform, flexWidth: 1); F.Preferred((RectTransform)slider.transform, height: 128);
            // Full-height touch target, thin track and a clear, readable thumb.
            var hit = slider.gameObject.AddComponent<Image>(); hit.color = Color.clear; hit.raycastTarget = true;
            foreach (string child in new[] { "Background", "Fill Area" })
            {
                var rt = (RectTransform)slider.transform.Find(child);
                rt.anchorMin = new Vector2(0, .5f); rt.anchorMax = new Vector2(1, .5f);
                rt.sizeDelta = new Vector2(0, 14); rt.anchoredPosition = Vector2.zero;
            }
            slider.handleRect.sizeDelta = new Vector2(52, 52);
            percent = Text(row.transform, "Percent", "100%", 27, 60); F.Preferred(percent, width: 88);
            percent.alignment = TextAlignmentOptions.Right;
            return slider;
        }
        static Toggle Toggle(Transform parent, string name, string title, string description)
        {
            var row = F.Box(parent, "Row_" + name, UguiTheme.RusticSurfaceDark, true, true);
            F.HLayout(row.gameObject, 14, new RectOffset(16, 16, 10, 10), TextAnchor.MiddleLeft); F.Preferred(row, height: 136);
            var copy = F.Container(row.transform, "Copy"); F.VLayout(copy.gameObject, 6); F.Flexible(copy, flexWidth: 1);
            Text(copy, "Label", title, 31, 48); Text(copy, "Hint", description, 24, 42);
            var target = F.Box(row.transform, name, Color.clear, false, true); F.Preferred(target, width: 144, height: 116);
            var toggle = target.gameObject.AddComponent<Toggle>(); toggle.targetGraphic = target; toggle.transition = Selectable.Transition.None;
            var track = F.Box(target.transform, "Track", UguiTheme.RusticSurface, true); F.AnchorCenter(track.rectTransform, 144, 64);
            var knob = F.CircleBox(track.transform, "Knob", UguiTheme.Parchment); F.AnchorCenter(knob.rectTransform, 54, 54, -34, 0);
            var animation = target.gameObject.AddComponent<ToggleSwitchView>(); animation.track = track; animation.knob = knob.rectTransform;
            target.gameObject.AddComponent<PlayClickSfxOnToggle>();
            row.gameObject.AddComponent<ToggleRowTarget>().toggle = toggle;
            return toggle;
        }
    }
}
