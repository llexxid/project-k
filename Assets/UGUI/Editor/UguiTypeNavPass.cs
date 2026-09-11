using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace KingdomIdle.UGUI.Editor
{
    /// <summary>Refines the existing prefab hierarchy without regenerating screens or bindings.</summary>
    public static class UguiTypeNavPass
    {
        private const string Picto = "Assets/ExternalAssets/Layer Lab/GUI Pro-MinimalGame/Shared/Icons/PictoIcon/64/";
        private static Sprite Rounded => AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UGUI/Sprites/RoundedRect.png");

        internal static void ApplyTo(GameObject go)
        {
            // Outlines remain on combat overlays, where text sits directly over moving sprites.
            bool plainType = go.name != "Screen_Title" && go.name != "Item_DamageText" &&
                             go.name != "Overlay_DivineCutIn" && !go.name.StartsWith("Hud_");
            if (plainType)
            {
                foreach (var t in go.GetComponentsInChildren<TMP_Text>(true))
                {
                    if (t.font == null) continue;
                    t.fontSharedMaterial = t.font.material;
                    t.fontStyle &= ~FontStyles.Bold;
                    t.characterSpacing = 0;
                    t.raycastTarget = false;
                }
            }

            foreach (var card in go.GetComponentsInChildren<DungeonCardView>(true))
                foreach (var t in card.GetComponentsInChildren<TMP_Text>(true))
                    if (t.name == "Description") Type(t, 26, 64);
            foreach (var difficulty in go.GetComponentsInChildren<DungeonDifficultyRowView>(true))
                foreach (var t in difficulty.GetComponentsInChildren<TMP_Text>(true))
                    if (t.name == "PowerLabel") Type(t, 24, 44);

            switch (go.name)
            {
                case "Screen_Main": Main(go.GetComponent<MainScreenView>()); break;
                case "Item_NavTabButton": SecondaryTab(go.GetComponent<NavTabButtonView>()); break;
                case "Item_MageEquipSlot":
                    var slot = go.GetComponent<MageEquipSlotView>();
                    Flat(go.GetComponent<Image>(), UguiTheme.RusticSurfaceDark);
                    Type(slot.label,32);
                    Fill(slot.label.rectTransform,12);
                    slot.label.alignment=TextAlignmentOptions.Center;
                    break;
                case "Panel_Dungeon":
                    foreach (var t in go.GetComponentsInChildren<TMP_Text>(true))
                        if (t.name == "Hint") Type(t,26,48);
                    break;
                case "Body_Development":
                    Type(go.transform.Find("Desc")?.GetComponent<TMP_Text>(), 26, 64);
                    break;
                case "Overlay_Settings":
                    foreach (var t in go.GetComponentsInChildren<TMP_Text>(true))
                        if (t.text == "전체 음량") { Type(t, 28, 64); Width(t.transform, 144); }
                    go.GetComponent<SettingsModalView>().btnSaveClose.GetComponentInChildren<TMP_Text>().color=UguiTheme.RusticBarDeep;
                    break;
                case "Panel_MageTowerDetail":
                    var detail=go.GetComponent<MageTowerDetailPopupView>();
                    foreach(var b in new[]{detail.btnEnhance,detail.btnAwaken})
                        foreach(var t in b.GetComponentsInChildren<TMP_Text>(true)) t.color=UguiTheme.RusticBarDeep;
                    break;
                case "Item_SkillRow":
                    var skill = go.GetComponent<SkillRowView>();
                    Type(skill.typeLabel, 22, 36);
                    Type(skill.nameLabel, 28, 42);
                    Type(skill.detailLabel, 26, 64);
                    Width(skill.typeBadge.transform, 104);
                    Height(skill.typeBadge.transform, 42);
                    Flat(go.GetComponent<Image>(), UguiTheme.RusticSurface);
                    break;
                case "Panel_KACharacterSheet":
                    foreach (var t in go.GetComponentsInChildren<TMP_Text>(true))
                    {
                        if (t.name == "HpVal" || t.name == "Value") Type(t, 26, 40);
                        if (t.name == "HpVal")
                        {
                            Height(t.transform.parent, 44);
                            Height(t.transform.parent.parent, 48);
                            t.color = UguiTheme.RusticBarDeep;
                        }
                    }
                    break;
                case "Item_CurrencyLine":
                    foreach (var t in go.GetComponentsInChildren<TMP_Text>(true)) Type(t, 28);
                    Height(go.transform, 64);
                    break;
                case "Item_RatePill":
                    foreach (var t in go.GetComponentsInChildren<TMP_Text>(true)) Type(t, 24);
                    Height(go.transform, 48);
                    break;
                case "Item_DungeonCard":
                    Type(go.transform.Find("Description")?.GetComponent<TMP_Text>(), 26, 64);
                    break;
            }
        }

        private static void Main(MainScreenView v)
        {
            var bar = v.bottomBar;
            Flat(bar.GetComponent<Image>(), UguiPixelSkin.Opaque(UguiTheme.RusticBarDeep), false);
            var row = bar.GetComponent<HorizontalLayoutGroup>();
            row.padding = new RectOffset(24, 24, 12, 18);
            row.spacing = 8;
            row.childControlWidth = row.childControlHeight = true;
            row.childForceExpandWidth = row.childForceExpandHeight = true;
            foreach (Transform child in bar)
            {
                if (child.GetComponent<MainTabButtonView>() == null) child.gameObject.SetActive(false);
            }
            var trim = bar.Find("TrimGold").GetComponent<Image>();
            trim.gameObject.SetActive(true);
            Flat(trim, UguiTheme.Bronze, false);
            Pin(trim.rectTransform, new Vector2(.5f, 1), Vector2.zero, new Vector2(1080, 2));
            trim.rectTransform.anchorMin = new Vector2(0, 1);
            trim.rectTransform.anchorMax = Vector2.one;
            trim.rectTransform.sizeDelta = new Vector2(0, 2);
            Layout(trim.transform).ignoreLayout = true;

            // Alpha bounds differ inside the 64px files. These rects give each symbol a ~64px visible span.
            MainTab(v.tabDevelopment, "hammer_1.png", 72);
            MainTab(v.tabKingdomArmy, "headgear.png", 88);
            MainTab(v.tabDungeon, "dungeon.png", 74);
            MainTab(v.tabGacha, "chest.png", 80);

            var top = v.transform.Find("HudTop");
            top.Find("TrimShadow").gameObject.SetActive(false);
            var topTrim = top.Find("TrimBronze").GetComponent<Image>();
            topTrim.color = UguiTheme.Bronze;
            topTrim.rectTransform.sizeDelta = new Vector2(0, 2);
            topTrim.rectTransform.anchoredPosition = Vector2.zero;
            foreach (var b in top.GetComponentsInChildren<Button>(true))
            {
                if (b == v.btnProfile) continue;
                Flat(b.GetComponent<Image>(), UguiTheme.RusticSurfaceDark);
                foreach (var shadow in b.GetComponents<Shadow>()) Object.DestroyImmediate(shadow);
                var frame = b.transform.Find("Frame");
                if (frame != null) frame.gameObject.SetActive(false);
            }
            HeaderAction(v.btnReincarnation, "sandglass_1.png", "환생");
            HeaderAction(v.btnHamburger, "menu_1.png", "메뉴");

            var profile = v.btnProfile.transform;
            profile.GetComponent<Image>().color = UguiTheme.Bronze;
            Pin((RectTransform)profile.Find("Socket"), new Vector2(.5f,.5f), Vector2.zero, new Vector2(130,130));
            Pin((RectTransform)profile.Find("Socket/Icon"), new Vector2(.5f,.5f), Vector2.zero, new Vector2(64,64));
            var badge = (RectTransform)profile.Find("LevelBadge");
            Flat(badge.GetComponent<Image>(), UguiTheme.BronzeLight);
            Pin(badge, new Vector2(1,0), new Vector2(-28,14), new Vector2(72,38));
            Fill(v.lblProfileLevel.rectTransform, 2);
            Type(v.lblProfileLevel, 26);
            v.lblProfileLevel.color = UguiTheme.RusticBarDeep;
            v.lblProfileLevel.enableAutoSizing = true;
            v.lblProfileLevel.fontSizeMin = 20;
            v.lblProfileLevel.fontSizeMax = 26;

            foreach (var b in new[] {v.btnCurrency, v.btnAncientCoin})
            {
                var ring = b.transform.Find("IconRing");
                ring.GetComponent<Image>().color = Color.clear;
                ring.Find("Socket").GetComponent<Image>().color = Color.clear;
                Pin((RectTransform)ring.Find("Socket/Icon"), new Vector2(.5f,.5f), Vector2.zero, new Vector2(64,64));
                var value=b.transform.Find("Value").GetComponent<TMP_Text>();
                Type(value, 32);
                value.enableAutoSizing=true;
                value.fontSizeMin=22;
                value.fontSizeMax=32;
                value.textWrappingMode=TextWrappingModes.NoWrap;
                value.overflowMode=TextOverflowModes.Ellipsis;
            }

            Stage(v.waveHud);
            var goal = (RectTransform)v.transform.Find("GuideGoal");
            goal.anchoredPosition = new Vector2(0,-358);
        }

        private static void MainTab(MainTabButtonView tab, string iconFile, float opticalSize)
        {
            Flat(tab.background, Color.clear);
            tab.bgNormalSprite = null;
            tab.bgSelectedSprite = null;
            tab.transform.localScale = Vector3.one;
            Width(tab.transform, 0, 1);
            Height(tab.transform, 160);
            tab.button.transition = Selectable.Transition.ColorTint;
            tab.button.colors = UguiTheme.MakeColorBlock();
            foreach (var shadow in tab.GetComponents<Shadow>()) Object.DestroyImmediate(shadow);
            foreach (Transform child in tab.transform)
                if (child.name.StartsWith("Stud")) child.gameObject.SetActive(false);
            var inner = (RectTransform)tab.transform.Find("Inner");
            tab.content = inner;
            var old = inner.GetComponent<VerticalLayoutGroup>();
            if (old != null) Object.DestroyImmediate(old);
            Fill(inner, 0);
            var wrap = (RectTransform)inner.Find("IconWrap");
            Fill(wrap, 0);
            wrap.Find("Socket").gameObject.SetActive(false);
            tab.icon.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Picto + iconFile);
            tab.icon.preserveAspect = true;
            tab.icon.raycastTarget = false;
            tab.icon.color = UguiTheme.NavMuted;
            Pin(tab.icon.rectTransform, new Vector2(.5f,1), new Vector2(0,-52), Vector2.one * opticalSize);
            Pin(tab.label.rectTransform, new Vector2(.5f,0), new Vector2(0,35), new Vector2(220,44));
            Type(tab.label, UguiTheme.FontTabLabel);
            tab.label.color = UguiTheme.NavMuted;
            tab.label.alignment = TextAlignmentOptions.Center;
            tab.label.textWrappingMode = TextWrappingModes.NoWrap;
            Flat(tab.indicator, new Color(1,1,1,0));
            tab.indicator.raycastTarget = false;
            Pin(tab.indicator.rectTransform, new Vector2(.5f,1), new Vector2(0,-3), new Vector2(68,4));
            // Press feedback scales content, never the hit area or neighboring tabs.
            var press = tab.GetComponent<UIButtonPress>();
            if (press != null)
            {
                var so = new SerializedObject(press);
                so.FindProperty("target").objectReferenceValue = inner;
                so.FindProperty("pressedScale").floatValue = .96f;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static void SecondaryTab(NavTabButtonView tab)
        {
            var labelLayout = Layout(tab.label.transform);
            labelLayout.minWidth = 0;
            labelLayout.preferredWidth = -1;
            labelLayout.flexibleWidth = 0;
            var row = tab.transform.Find("Inner").GetComponent<HorizontalLayoutGroup>();
            row.childAlignment = TextAnchor.MiddleCenter;
            row.spacing = 14;
            var wrap = tab.icon.transform.parent;
            foreach (var image in wrap.GetComponentsInChildren<Image>(true))
                if (image != tab.icon) image.gameObject.SetActive(false);
            tab.icon.preserveAspect = true;
        }

        private static void HeaderAction(Button button, string file, string caption)
        {
            var image = button.transform.Find("Icon").GetComponent<Image>();
            image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Picto + file);
            image.color = UguiTheme.Parchment;
            image.preserveAspect = true;
            Pin(image.rectTransform, new Vector2(.5f,.5f), new Vector2(0,23), new Vector2(52,52));
            var label = button.transform.Find("Label")?.GetComponent<TMP_Text>();
            if (label == null)
            {
                var child = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
                child.transform.SetParent(button.transform, false);
                label = child.GetComponent<TMP_Text>();
                label.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/UGUI/Art/Font/Galmuri11 SDF.asset");
            }
            Type(label,26);
            label.text = caption;
            label.alignment = TextAlignmentOptions.Center;
            label.color = UguiTheme.Parchment;
            Pin(label.rectTransform, new Vector2(.5f,.5f), new Vector2(0,-39), new Vector2(132,38));
        }

        private static void Stage(WaveHudView v)
        {
            var area = (RectTransform)v.transform;
            area.sizeDelta = new Vector2(area.sizeDelta.x,164);
            var row = v.lblStage.transform.parent;
            Width(row, 984);
            Height(row, 144);
            var layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(24,20,0,0);
            layout.spacing = 12;
            row.Find("Frame").gameObject.SetActive(false);
            row.Find("StageIcon").gameObject.SetActive(false);
            Flat(row.GetComponent<Image>(), UguiTheme.RusticPanelDeep);
            Type(v.lblStage, 34, 76);
            Width(v.lblStage.transform, 0, 1);
            v.lblStage.alignment = TextAlignmentOptions.MidlineLeft;
            v.lblStage.enableAutoSizing = true;
            v.lblStage.fontSizeMin = 28;
            v.lblStage.fontSizeMax = 34;
            v.lblStage.textWrappingMode = TextWrappingModes.NoWrap;
            v.lblStage.overflowMode = TextOverflowModes.Ellipsis;
            // Keep the stage name stationary when the repeat affordance becomes visible.
            v.btnLoopIcon.transform.SetSiblingIndex(v.lblStage.transform.GetSiblingIndex()+1);
            Width(v.btnLoopIcon.transform,144);
            Height(v.btnLoopIcon.transform,144);
            Flat(v.btnLoopIcon.GetComponent<Image>(), UguiTheme.RusticSurface);
            HeaderAction(v.btnLoopIcon,"refresh.png","반복 중");
            Width(v.bossChallengeRoot.transform, 316);
            Height(v.bossChallengeRoot.transform, 144);
            var bossLabel = v.bossChallengeRoot.transform.Find("LblBossChain").GetComponent<TMP_Text>();
            bossLabel.text = "보스 자동";
            Type(bossLabel, 26, 64);
            Width(bossLabel.transform, 160);
            bossLabel.alignment = TextAlignmentOptions.Center;
            Width(v.tglBossChain.transform,144);
            Height(v.tglBossChain.transform,144);
            var root = v.tglBossChain.GetComponent<Image>();
            Flat(root, Color.clear, false);
            root.raycastTarget = true;
            v.tglBossChain.targetGraphic = root;
            v.tglBossChain.graphic = null;
            v.tglBossChain.transition = Selectable.Transition.None;
            if (v.tglBossChain.GetComponent<ToggleSwitchView>() == null)
            {
                foreach (Transform c in v.tglBossChain.transform) c.gameObject.SetActive(false);
                var track = NewImage(v.tglBossChain.transform, "SwitchTrack", new Vector2(144,64), Rounded);
                var knob = NewImage(track.transform,"Knob",new Vector2(54,54),AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UGUI/Sprites/Circle.png"));
                knob.color = UguiTheme.Parchment;
                var view = v.tglBossChain.gameObject.AddComponent<ToggleSwitchView>();
                view.track = track;
                view.knob = knob.rectTransform;
            }
            var hit = v.bossChallengeRoot.GetComponent<Image>() ?? v.bossChallengeRoot.AddComponent<Image>();
            Flat(hit, Color.clear, false);
            hit.raycastTarget = true;
            var target = v.bossChallengeRoot.GetComponent<ToggleRowTarget>() ?? v.bossChallengeRoot.AddComponent<ToggleRowTarget>();
            target.toggle = v.tglBossChain;
        }

        private static Image NewImage(Transform parent, string name, Vector2 size, Sprite sprite)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent,false);
            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.raycastTarget = false;
            Pin(image.rectTransform,new Vector2(.5f,.5f),Vector2.zero,size);
            return image;
        }
        private static LayoutElement Layout(Transform t) => t.GetComponent<LayoutElement>() ?? t.gameObject.AddComponent<LayoutElement>();
        private static void Width(Transform t, float width, float flex = 0)
        { var l=Layout(t); l.minWidth=width; l.preferredWidth=width; l.flexibleWidth=flex; }
        private static void Height(Transform t, float height)
        { var l=Layout(t); l.minHeight=height; l.preferredHeight=height; l.flexibleHeight=0; }
        private static void Type(TMP_Text t, float size, float height = 0)
        {
            if (t == null) return;
            t.fontSize=size; t.fontSizeMin=size; t.fontSizeMax=size; t.enableAutoSizing=false;
            t.fontStyle=FontStyles.Normal; t.raycastTarget=false;
            if(t.font!=null)t.fontSharedMaterial=t.font.material;
            if(height>0)Height(t.transform,height);
        }
        private static void Flat(Image image, Color color, bool rounded = true)
        { image.sprite=rounded?Rounded:null; image.type=rounded?Image.Type.Sliced:Image.Type.Simple; image.pixelsPerUnitMultiplier=1; image.color=color; }
        private static void Fill(RectTransform t, float inset)
        { t.anchorMin=Vector2.zero;t.anchorMax=Vector2.one;t.offsetMin=Vector2.one*inset;t.offsetMax=-Vector2.one*inset;t.localScale=Vector3.one; }
        private static void Pin(RectTransform t, Vector2 anchor, Vector2 position, Vector2 size)
        { t.anchorMin=t.anchorMax=anchor;t.pivot=new Vector2(.5f,.5f);t.anchoredPosition=position;t.sizeDelta=size;t.localScale=Vector3.one; }
    }
}
