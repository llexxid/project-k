using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace KingdomIdle.UGUI.Editor
{
    /// <summary>Targeted migration of existing prefabs; keeps all view bindings and custom artwork.</summary>
    public static class UguiPolishPass
    {
        private const string Root = "Assets/UGUI/Prefabs/";
        private const string Kit = "Assets/UGUI/Art/LayerLab/GUI Pro-MinimalGame/Shared/Sprite_Common/";
        private static Sprite _panel, _frame;
        internal static void ApplyTo(GameObject go)
        {
            _panel = AssetDatabase.LoadAssetAtPath<Sprite>(Kit + "Frame/BasicFrame/BasicFrame_Rectangle_01~04_White_Bg.png");
            _frame = AssetDatabase.LoadAssetAtPath<Sprite>(Kit + "Frame/BasicFrame/BasicFrame_Rectangle_01~04_White_Border1.png");
            Polish(go);
            UguiTypeNavPass.ApplyTo(go);
        }
        [MenuItem("KingdomIdle/UGUI/Apply mobile usability polish")]
        public static void Apply()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
            _panel = AssetDatabase.LoadAssetAtPath<Sprite>(Kit + "Frame/BasicFrame/BasicFrame_Rectangle_01~04_White_Bg.png");
            _frame = AssetDatabase.LoadAssetAtPath<Sprite>(Kit + "Frame/BasicFrame/BasicFrame_Rectangle_01~04_White_Border1.png");
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { Root.TrimEnd('/') }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var go = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    Polish(go);
                    UguiTypeNavPass.ApplyTo(go);
                    PrefabUtility.SaveAsPrefabAsset(go, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(go); }
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[UI polish] Existing UI prefabs migrated.");
        }

        private static void Polish(GameObject go)
        {
            foreach (var text in go.GetComponentsInChildren<TMP_Text>(true)) text.raycastTarget = false;
            foreach (var image in go.GetComponentsInChildren<Image>(true))
            {
                if (image.name == "Sheen" || image.name == "InnerRim") image.gameObject.SetActive(false);
                if (image.name == "Sheet" || image.name == "Panel" || image.name == "Window")
                { image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UGUI/Sprites/RoundedRect.png"); image.type = Image.Type.Sliced; var color=UguiTheme.RusticPanelDeep;color.a=1;image.color=color; }
            }
            foreach (var banner in go.GetComponentsInChildren<RectTransform>(true).Where(t => t.name == "TitleBanner"))
            {
                Height(banner, 94);
                var art = banner.Find("Banner") as RectTransform;
                if (art == null) continue;
                Stretch(art, 0);
                var img = art.GetComponent<Image>();
                if (img != null) img.enabled = false;
                foreach (var t in art.GetComponentsInChildren<TMP_Text>(true))
                {
                    Stretch(t.rectTransform, 12);
                    t.alignment = TextAlignmentOptions.MidlineLeft;
                    t.fontSize = 40; t.color = UguiTheme.Parchment; t.fontStyle = FontStyles.Bold;
                    t.overflowMode = TextOverflowModes.Overflow;
                }
            }
            foreach (var button in go.GetComponentsInChildren<Button>(true))
            {
                string n = button.name;
                if (n == "BtnPanelClose" || n == "BtnClose") CloseButton(button);
            }
            foreach (var grid in go.GetComponentsInChildren<GridLayoutGroup>(true))
            {
                if (grid.constraintCount >= 4 || grid.name == "RewardGrid" || grid.name == "Grid")
                {
                    grid.spacing = new Vector2(16, 16);
                    if (grid.GetComponent<ResponsiveGrid>() == null) grid.gameObject.AddComponent<ResponsiveGrid>();
                }
            }
            var sheet = go.GetComponent<BottomSheetView>();
            if (sheet != null && sheet.Sheet != null)
            {
                if(sheet.Sheet.GetComponent<SheetSizeFitter>()==null)
                {var fitter=sheet.Sheet.gameObject.AddComponent<SheetSizeFitter>();fitter.preferredHeight=sheet.Sheet.sizeDelta.y;}
                var header = sheet.Sheet.Find("Header");
                if (header != null)
                {
                    Height(header, 116);
                    var layout = header.GetComponent<VerticalLayoutGroup>();
                    if (layout != null) layout.padding = new RectOffset(0, 140, 0, 0);
                }
                foreach (var row in sheet.Sheet.GetComponentsInChildren<HorizontalLayoutGroup>(true))
                    if (row.name.EndsWith("TabBar") || row.name == "NavBar" || row.name == "MemberTabs") Height(row.transform, 144);
            }
            if (go.name.StartsWith("Overlay_") || go.name.StartsWith("Popup_") || go.name == "Panel_MageTowerEquip" || go.name == "Panel_MageTowerDetail")
            {
                Stretch((RectTransform)go.transform, 0);
                var panel = go.transform.Find("Panel") as RectTransform;
                if (panel != null && panel.anchorMin.x == panel.anchorMax.x && panel.GetComponent<ModalSizeFitter>() == null)
                    panel.gameObject.AddComponent<ModalSizeFitter>();
            }
            switch (go.name)
            {
                case "UGUI_UIRoot":
                    go.GetComponent<CanvasScaler>().matchWidthOrHeight = 0;
                    break;
                case "Item_NavTabButton": Tabs(go); break;
                case "Item_GachaPullButton": PullButton(go); break;
                case "Item_EnhanceCard": EnhanceCard(go); break;
                case "Item_EquipCell": EquipmentCard(go); break;
                case "Item_GachaCard": GachaCard(go); break;
                case "Hud_Party": Party(go); break;
                case "Overlay_Settings": Settings(go); break;
                case "Screen_Main": Main(go); break;
                case "Item_JobCard":
                    var job=go.GetComponent<JobCardView>(); Text(job.nameLabel,32,66); Text(job.statLabel,26,48); Text(job.fragLabel,26,48); break;
                case "GachaTabContent":
                    foreach(var t in go.GetComponentsInChildren<TMP_Text>(true))
                    {
                        Text(t,28,t.name == "Desc" ? 90 : 52);
                        if(t.name == "Desc") {t.textWrappingMode=TextWrappingModes.Normal;t.overflowMode=TextOverflowModes.Overflow;}
                    }
                    break;
                case "Item_MageSkillCell":
                    var cell=go.GetComponent<MageSkillCellView>(); Text(cell.nameLabel,28,42);Text(cell.dmgLabel,25,66);Height(cell.icon.transform,96);break;
                case "Item_MageEquipSlot":
                    Height(go.transform,136);var slotWidth=Layout(go.transform);slotWidth.minWidth=144;slotWidth.preferredWidth=144;slotWidth.flexibleWidth=1;break;
                case "Item_DungeonDifficultyRow":Height(go.transform,144);break;
                case "Panel_MageTowerEquip":
                    var mage=go.GetComponent<MageTowerEquipPopupView>();mage.panelBox.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,1400);
                    var slots=mage.panelBox.Find("Body/SlotsCol");var sl=Layout(slots);sl.minWidth=160;sl.preferredWidth=160;sl.flexibleWidth=0;
                    foreach(var image in mage.panelBox.GetComponentsInChildren<Image>(true)) if(image.name=="TitleBar")image.color=UguiTheme.RusticSurface;
                    foreach(var t in mage.panelBox.GetComponentsInChildren<TMP_Text>(true)) if(t.text=="보유 스킬")Text(t,30,60);
                    break;
                case "Panel_MageTowerDetail": MageDetail(go);break;
                case "Popup_DungeonClear":
                    Height(go.transform.Find("Panel/ButtonRow"),144);
                    foreach(var b in go.transform.Find("Panel/ButtonRow").GetComponentsInChildren<Button>(true)){Height(b.transform,144);foreach(var t in b.GetComponentsInChildren<TMP_Text>())Text(t,30,64);}
                    var clearPanel=go.transform.Find("Panel");var clearFit=clearPanel.GetComponent<ContentSizeFitter>()??clearPanel.gameObject.AddComponent<ContentSizeFitter>();clearFit.verticalFit=ContentSizeFitter.FitMode.PreferredSize;
                    break;
                case "Popup_Reincarnation":
                    foreach(var b in go.GetComponentsInChildren<Button>(true))if(b.name!="Dim") {Height(b.transform,144);foreach(var t in b.GetComponentsInChildren<TMP_Text>())Text(t,32,64);}
                    Height(go.transform.Find("Panel/InfoCard"),320);Height(go.transform.Find("Panel/ButtonRow"),144);
                    ((RectTransform)go.transform.Find("Panel")).SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,960);
                    foreach(var image in go.GetComponentsInChildren<Image>(true)) if(image.name=="InfoCard")image.color=UguiTheme.RusticSurface;
                    break;
                case "Screen_Title":
                    foreach(var b in go.GetComponentsInChildren<Button>(true))if(b.name=="BtnLoginApple"||b.name=="BtnLoginGuest")b.gameObject.SetActive(false);
                    foreach(var t in go.GetComponentsInChildren<TMP_Text>(true))if(t.text=="PRESS ANYWHERE TO CONTINUE")t.text="화면을 터치해 시작";
                    foreach(var image in go.GetComponentsInChildren<Image>(true))if((image.name.Contains("Provider")||image.name=="Icon")&&image.sprite==null)image.gameObject.SetActive(false);
                    break;
                case "Popup_DungeonDifficulty":
                    var window=go.transform.Find("Window");
                    if(window.GetComponent<ModalSizeFitter>()==null)window.gameObject.AddComponent<ModalSizeFitter>();
                    var fit=window.GetComponent<ContentSizeFitter>()??window.gameObject.AddComponent<ContentSizeFitter>();fit.verticalFit=ContentSizeFitter.FitMode.PreferredSize;
                    Height(window.Find("PreviewCard"),240);Height(window.Find("LblDifficultySection"),52);Height(window.Find("Footer"),144);
                    foreach(var b in window.Find("Footer").GetComponentsInChildren<Button>(true))Height(b.transform,144);
                    Text(window.Find("LblDifficultySection").GetComponent<TMP_Text>(),32,52);
                    break;
            }
        }
        private static LayoutElement Layout(Transform t) => t.GetComponent<LayoutElement>() ?? t.gameObject.AddComponent<LayoutElement>();
        private static void Height(Transform t, float height)
        { var le = Layout(t); le.minHeight = height; le.preferredHeight = height; le.flexibleHeight = 0; }
        private static void Flexible(Transform t)
        { var le = Layout(t); le.minWidth = 0; le.preferredWidth = 0; le.flexibleWidth = 1; }
        private static void Stretch(RectTransform rt, float inset)
        { rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.one * inset; rt.offsetMax = -Vector2.one * inset; }
        private static void Text(TMP_Text t, float size, float height)
        {
            if (t == null) return;
            t.fontSize = size; t.fontSizeMin = size; t.color = UguiTheme.Parchment;
            if(t.font!=null)t.fontSharedMaterial=t.font.material;
            t.enableAutoSizing = false; t.overflowMode = TextOverflowModes.Ellipsis;
            t.textWrappingMode = TextWrappingModes.Normal;
            Height(t.transform, height); Flexible(t.transform);
        }
        private static void CloseButton(Button button)
        {
            var rt = (RectTransform)button.transform;
            rt.sizeDelta = new Vector2(144,144);
            Height(rt,144);var hit=Layout(rt);hit.minWidth=144;hit.preferredWidth=144;hit.flexibleWidth=0;
            if(rt.parent.GetComponent<HorizontalLayoutGroup>()!=null)Height(rt.parent,144);
            if (rt.anchorMin == Vector2.one) { rt.pivot = Vector2.one; rt.anchoredPosition = new Vector2(-4,-4); }
            var bg = button.GetComponent<Image>();
            if (bg != null) { bg.sprite = null; bg.color = new Color(0,0,0,0); }
            foreach (var img in button.GetComponentsInChildren<Image>(true)) if (img != bg) img.gameObject.SetActive(false);
            var label = button.GetComponentInChildren<TMP_Text>(true);
            if (label == null)
            {
                var child = new GameObject("CloseLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
                child.transform.SetParent(button.transform, false);
                label = child.GetComponent<TMP_Text>();
                label.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/UGUI/Art/Font/Galmuri11 SDF.asset");
            }
            Stretch(label.rectTransform, 24); label.text = "×"; label.fontSize = 58;
            label.color = UguiTheme.Parchment; label.alignment = TextAlignmentOptions.Center; label.raycastTarget = false;
            foreach (var text in button.GetComponentsInChildren<TMP_Text>(true)) text.gameObject.SetActive(text == label);
        }
        private static void Tabs(GameObject go)
        {
            Height(go.transform, 144); Flexible(go.transform);
            var inner = go.transform.Find("Inner");
            var old = inner.GetComponent<VerticalLayoutGroup>(); if (old != null) UnityEngine.Object.DestroyImmediate(old);
            var row = inner.GetComponent<HorizontalLayoutGroup>() ?? inner.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.padding = new RectOffset(18,18,12,12); row.spacing = 12; row.childAlignment = TextAnchor.MiddleCenter;
            row.childControlWidth = true; row.childControlHeight = true; row.childForceExpandHeight = false; row.childForceExpandWidth = false;
            var icon = inner.Find("IconWrap"); var il = Layout(icon); il.minWidth=48; il.preferredWidth=48; il.flexibleWidth=0; Height(icon,48);
            var label = inner.Find("Label").GetComponent<TMP_Text>(); Text(label,32,64); label.alignment=TextAlignmentOptions.Center;
            label.overflowMode=TextOverflowModes.Overflow;
            go.GetComponent<Image>().color = UguiTheme.RusticSurfaceDark;
            var frame=go.transform.Find("SelectedFrame").GetComponent<Image>(); frame.sprite=_frame; frame.color=UguiTheme.BronzeLight;
        }
        private static void PullButton(GameObject go)
        {
            Height(go.transform, 152); Flexible(go.transform);
            var row = go.GetComponent<VerticalLayoutGroup>(); row.padding=new RectOffset(20,20,14,14); row.spacing=4;
            go.transform.Find("IconWrap").gameObject.SetActive(false);
            Text(go.transform.Find("Title").GetComponent<TMP_Text>(),34,58);
            Text(go.transform.Find("Cost").GetComponent<TMP_Text>(),30,48);
        }
        private static void EnhanceCard(GameObject go)
        {
            go.GetComponent<Image>().color=UguiTheme.RusticSurfaceDark;
            var group=go.GetComponent<VerticalLayoutGroup>(); group.padding=new RectOffset(24,24,24,24); group.spacing=14;
            Height(go.transform.Find("Header"),58);
            Text(go.transform.Find("Header/Name").GetComponent<TMP_Text>(),36,58);
            Text(go.transform.Find("Header/Level").GetComponent<TMP_Text>(),32,58);
            Text(go.transform.Find("Bonus").GetComponent<TMP_Text>(),30,60);
            Height(go.transform.Find("ButtonRow"),152);
        }
        private static void EquipmentCard(GameObject go)
        {
            var v=go.GetComponent<EquipCellView>();
            Text(v.nameLabel,30,76); Text(v.subLabel,26,42); Text(v.stateLabel,24,40);
            Height(go.transform.Find("IconWrap"),94); v.icon.rectTransform.sizeDelta=new Vector2(88,88);
        }
        private static void GachaCard(GameObject go)
        {
            var v=go.GetComponent<GachaCardItemView>(); Text(v.nameLabel,30,72); Text(v.subLabel,26,78);
            var wrap=v.icon != null ? v.icon.transform.parent : null;
            if (wrap != null) { Height(wrap,88); v.icon.rectTransform.sizeDelta=new Vector2(88,88); }
        }
        private static void Party(GameObject go)
        {
            var rt=(RectTransform)go.transform; rt.anchorMin=new Vector2(0,0); rt.anchorMax=new Vector2(1,0); rt.pivot=new Vector2(.5f,0); rt.sizeDelta=new Vector2(-48,148);
            var fitter=go.GetComponent<ContentSizeFitter>(); if(fitter!=null) UnityEngine.Object.DestroyImmediate(fitter);
            var row=go.GetComponent<HorizontalLayoutGroup>(); row.spacing=14; row.childForceExpandWidth=true;
            foreach(var member in go.GetComponent<PartyHudView>().members)
            {
                var root=member.portrait.transform.parent; Flexible(root); Height(root,148);
                var hit=root.GetComponent<Button>()??root.gameObject.AddComponent<Button>();hit.targetGraphic=root.GetComponent<Image>();hit.transition=Selectable.Transition.None;
                if(hit.targetGraphic!=null)hit.targetGraphic.raycastTarget=true;
                var lg=root.GetComponent<HorizontalLayoutGroup>(); lg.spacing=10; lg.padding=new RectOffset(10,10,12,12);
                var portrait=Layout(member.portrait.transform); portrait.minWidth=108; portrait.preferredWidth=108; portrait.flexibleWidth=0; Height(member.portrait.transform,108);
                var col=root.Find("InfoCol"); Flexible(col); var hp=col.Find("HpBar"); Flexible(hp);
                var group=col.GetComponent<VerticalLayoutGroup>(); group.childForceExpandWidth=true;
            }
        }
        private static void MageDetail(GameObject go)
        {
            var view=go.GetComponent<MageTowerDetailPopupView>();
            if (view.scroll != null) return; // Current responsive generator already authors this layout.
            var panel=go.transform.Find("Panel");
            Text(view.titleLabel,38,72);
            Height(panel.Find("IconRow"),188);
            foreach(var t in new[]{view.lblBaseDmg,view.lblEffDmg,view.lblBaseCd,view.lblEffCd})Text(t,28,42);
            foreach(var t in new[]{view.lblEnhLevel,view.lblEnhCost,view.lblAwkLevel,view.lblAwkCost,view.lblResetRefund})Text(t,28,44);
            foreach(var section in panel.Cast<Transform>().Where(t=>t.name=="Section"))
            {section.GetComponent<Image>().color=UguiTheme.RusticSurfaceDark;var title=section.Find("Title")?.GetComponent<TMP_Text>();if(title!=null)Text(title,30,48);}
            foreach(var b in new[]{view.btnEnhance,view.btnAwaken,view.btnReset})
            {Height(b.transform,144);var bg=b.GetComponent<Image>();bg.color=b==view.btnReset?UguiTheme.BtnCancel:UguiTheme.BtnConfirm;foreach(var t in b.GetComponentsInChildren<TMP_Text>())Text(t,32,64);}
        }
        private static void Settings(GameObject go)
        {
            var view=go.GetComponent<SettingsModalView>();
            if (view.numberButtons != null && view.numberButtons.Length == 3) return;
            Stretch((RectTransform)go.transform,0);
            if(view.btnSave!=null)view.btnSave.gameObject.SetActive(false);
            if(view.btnWithdraw!=null)view.btnWithdraw.gameObject.SetActive(false);
            foreach(var t in new[]{view.tglPush,view.tglNightPush,view.tglHideItem}) if(t!=null)t.transform.parent.gameObject.SetActive(false);
            var panel=view.panel; var grid=panel.Find("ToggleGrid");
            var old=grid.GetComponent<HorizontalLayoutGroup>(); if(old!=null)UnityEngine.Object.DestroyImmediate(old);
            var list=grid.GetComponent<VerticalLayoutGroup>()??grid.gameObject.AddComponent<VerticalLayoutGroup>();
            var gridLayout=Layout(grid); gridLayout.minHeight=-1; gridLayout.preferredHeight=-1; gridLayout.flexibleHeight=0;
            list.spacing=14;list.childControlWidth=true;list.childControlHeight=true;list.childForceExpandWidth=true;list.childForceExpandHeight=false;
            foreach(var toggle in new[]{view.tglLowSpec,view.tglPowerSave,view.tglDamageText,view.tglScreenShake})
            {
                if (toggle == null) continue;
                var row=toggle.transform.parent; row.SetParent(grid,false); Height(row,136);
                var rowImage=row.GetComponent<Image>(); if(rowImage!=null)rowImage.color=UguiTheme.RusticSurfaceDark;
                var target=row.GetComponent<ToggleRowTarget>()??row.gameObject.AddComponent<ToggleRowTarget>();target.toggle=toggle;
                if(rowImage!=null)rowImage.raycastTarget=true;
                Text(row.GetComponentInChildren<TMP_Text>(),34,64);
                var le=Layout(toggle.transform);le.minWidth=144;le.preferredWidth=144;le.flexibleWidth=0;Height(toggle.transform,112);
                Switch(toggle);
            }
            foreach(var n in new[]{"ColL","ColR"}){var col=grid.Find(n);if(col!=null)col.gameObject.SetActive(false);}
            Text(view.lblServer,28,52); Text(view.lblVersion,28,52);
            foreach(var t in panel.GetComponentsInChildren<TMP_Text>(true)) if(t.text.Trim().StartsWith("문의:")) t.gameObject.SetActive(false);
            Height(panel.Find("VolumeRow"),136); Height(view.btnSaveClose.transform.parent,144); Height(view.btnSaveClose.transform,144);
            panel.Find("VolumeRow").GetComponent<Image>().color=UguiTheme.RusticSurfaceDark;
            Height(view.btnMute.transform,136); var ml=Layout(view.btnMute.transform);ml.minWidth=152;ml.preferredWidth=152;ml.flexibleWidth=0;
            Height(view.sldVolume.transform,136);
            foreach(var path in new[]{"Background","Fill Area"})
            {var rt=(RectTransform)view.sldVolume.transform.Find(path);rt.anchorMin=new Vector2(0,.5f);rt.anchorMax=new Vector2(1,.5f);rt.sizeDelta=new Vector2(0,14);rt.anchoredPosition=Vector2.zero;}
            view.sldVolume.handleRect.sizeDelta=new Vector2(52,52);
            Text(view.btnMute.GetComponentInChildren<TMP_Text>(),28,52);
            go.transform.Find("HintClose").gameObject.SetActive(false);
            view.btnSaveClose.GetComponentInChildren<TMP_Text>().text="완료";
            Text(view.btnSaveClose.GetComponentInChildren<TMP_Text>(),36,64);
        }
        private static void Main(GameObject go)
        {
            var profileLevel=go.GetComponent<MainScreenView>().lblProfileLevel;
            if(profileLevel!=null){profileLevel.enableAutoSizing=true;profileLevel.fontSizeMin=18;profileLevel.fontSizeMax=32;profileLevel.overflowMode=TextOverflowModes.Overflow;}
            var death=go.transform.Find("DeathPopup");
            var deathPanel=death.Find("Panel") as RectTransform;
            if(deathPanel==null)
            {
                var children=death.Cast<Transform>().ToArray();
                var oldLayout=death.GetComponent<VerticalLayoutGroup>();if(oldLayout!=null)UnityEngine.Object.DestroyImmediate(oldLayout);
                var dim=death.GetComponent<Image>()??death.gameObject.AddComponent<Image>();dim.color=new Color(0,0,0,.62f);dim.raycastTarget=true;
                deathPanel=Child(death,"Panel",typeof(Image),typeof(VerticalLayoutGroup),typeof(ContentSizeFitter),typeof(ModalSizeFitter));
                deathPanel.sizeDelta=new Vector2(980,500);
                deathPanel.GetComponent<Image>().sprite=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UGUI/Sprites/RoundedRect.png");deathPanel.GetComponent<Image>().type=Image.Type.Sliced;deathPanel.GetComponent<Image>().color=UguiTheme.RusticPanelDeep;
                var stack=deathPanel.GetComponent<VerticalLayoutGroup>();stack.padding=new RectOffset(36,36,32,32);stack.spacing=18;stack.childControlWidth=true;stack.childControlHeight=true;stack.childForceExpandWidth=true;stack.childForceExpandHeight=false;
                deathPanel.GetComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;
                foreach(var child in children)child.SetParent(deathPanel,false);
            }
            var deathTitle=deathPanel.Find("Title").GetComponent<TMP_Text>();deathTitle.text="도전 실패";Text(deathTitle,40,72);deathTitle.color=UguiTheme.Parchment;
            Text(deathPanel.Find("LblDeathMsg").GetComponent<TMP_Text>(),32,100);
            Height(deathPanel.Find("Buttons"),144);
            foreach(var b in deathPanel.Find("Buttons").GetComponentsInChildren<Button>(true))
            {
                Height(b.transform,144);Flexible(b.transform);b.GetComponent<Image>().color=b.name=="BtnDeathYes"?UguiTheme.LoginBtnBg:UguiTheme.RusticSurface;
                var label=b.GetComponentInChildren<TMP_Text>();label.text=b.name=="BtnDeathYes"?"다시 도전":"돌아가기";Text(label,34,72);
            }
            var menu=go.transform.Find("PopupHamburger") as RectTransform;
            menu.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,380);
            foreach(var button in menu.GetComponentsInChildren<Button>(true))
            {
                if(button.name=="BtnLoopIcon")continue;
                if(button.name=="BtnMenuNotice"||button.name=="BtnMenuMail"){button.gameObject.SetActive(false);continue;}
                string caption=button.name=="BtnMenuGuide"?"퀘스트 / 가이드":button.name=="BtnMenuInventory"?"가방":button.name=="BtnMenuSettings"?"설정":"";
                Height(button.transform,144);
                var buttonLayout=Layout(button.transform);buttonLayout.minWidth=0;buttonLayout.preferredWidth=-1;buttonLayout.flexibleWidth=1;
                var icon=button.transform.Find("Icon") as RectTransform;
                if(icon!=null){icon.anchorMin=icon.anchorMax=new Vector2(0,.5f);icon.anchoredPosition=new Vector2(52,0);icon.sizeDelta=new Vector2(64,64);}
                var label=button.transform.Find("MenuLabel")?.GetComponent<TMP_Text>();
                if(label==null)label=GoalText(button.transform,"MenuLabel",34,64);
                label.text=caption;Stretch(label.rectTransform,16);label.rectTransform.offsetMin=new Vector2(100,16);label.alignment=TextAlignmentOptions.MidlineLeft;
            }
            var name=go.transform.Find("HudTop/LeftWrap");
            if(name!=null)name.Find("LblNickname").gameObject.SetActive(false);
            var grain=go.transform.Find("HudTop/WoodGrain");if(grain!=null)grain.gameObject.SetActive(false);
            foreach(var button in go.transform.Find("HudTop").GetComponentsInChildren<Button>(true))
            {
                Height(button.transform,144);
                var le=Layout(button.transform);le.minWidth=144;le.preferredWidth=144;le.flexibleWidth=0;
                if(button.name=="BtnCurrency"||button.name=="AncientCoinChip")
                {le.minWidth=248;le.preferredWidth=248; var value=button.transform.Find("Value").GetComponent<TMP_Text>();Text(value,32,72);value.overflowMode=TextOverflowModes.Overflow;
                 var ring=button.transform.Find("IconRing") as RectTransform;if(ring!=null){ring.pivot=new Vector2(0,.5f);ring.anchoredPosition=new Vector2(10,0);ring.sizeDelta=new Vector2(64,64);}
                }
            }
            if(go.transform.Find("GuideGoal") == null) Goal(go.transform);
        }
        private static RectTransform Child(Transform parent,string name,params Type[] components)
        {
            var go=new GameObject(name,new[]{typeof(RectTransform)}.Concat(components).ToArray());go.transform.SetParent(parent,false);return (RectTransform)go.transform;
        }
        private static void Switch(Toggle toggle)
        {
            var root=toggle.GetComponent<Image>(); if(root==null)root=toggle.gameObject.AddComponent<Image>();root.sprite=null;root.color=Color.clear;root.raycastTarget=true;
            toggle.targetGraphic=root;toggle.graphic=null;toggle.transition=Selectable.Transition.None;
            var existing=toggle.GetComponent<ToggleSwitchView>();
            if(existing!=null)return;
            for(int i=0;i<toggle.transform.childCount;i++)toggle.transform.GetChild(i).gameObject.SetActive(false);
            var track=Child(toggle.transform,"SwitchTrack",typeof(Image));track.sizeDelta=new Vector2(144,64);
            var trackImage=track.GetComponent<Image>();trackImage.sprite=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UGUI/Sprites/RoundedRect.png");trackImage.type=Image.Type.Sliced;trackImage.raycastTarget=false;
            var knob=Child(track,"Knob",typeof(Image));knob.sizeDelta=new Vector2(54,54);
            var knobImage=knob.GetComponent<Image>();knobImage.sprite=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UGUI/Sprites/Circle.png");knobImage.color=UguiTheme.Parchment;knobImage.raycastTarget=false;
            var view=toggle.gameObject.AddComponent<ToggleSwitchView>();view.track=trackImage;view.knob=knob;
        }
        private static void Goal(Transform parent)
        {
            var root=Child(parent,"GuideGoal",typeof(GuideGoalView));root.anchorMin=new Vector2(.5f,1);root.anchorMax=root.anchorMin;root.pivot=new Vector2(.5f,1);root.sizeDelta=new Vector2(984,144);root.anchoredPosition=new Vector2(0,-282);
            var body=Child(root,"Body",typeof(Image),typeof(HorizontalLayoutGroup));Stretch(body,0);body.GetComponent<Image>().sprite=_panel;body.GetComponent<Image>().type=Image.Type.Sliced;body.GetComponent<Image>().color=UguiTheme.RusticPanelDeep;
            var row=body.GetComponent<HorizontalLayoutGroup>();row.padding=new RectOffset(24,12,10,10);row.spacing=18;row.childControlWidth=true;row.childControlHeight=true;row.childForceExpandWidth=false;row.childForceExpandHeight=true;
            var copy=Child(body,"Copy",typeof(VerticalLayoutGroup));Flexible(copy);var column=copy.GetComponent<VerticalLayoutGroup>();column.spacing=0;column.childControlWidth=true;column.childControlHeight=true;column.childForceExpandWidth=true;column.childForceExpandHeight=false;
            var description=GoalText(copy,"Description",32,62);var progress=GoalText(copy,"Progress",26,48);progress.color=UguiTheme.AccentGold;
            var action=Child(body,"Action",typeof(Image),typeof(Button),typeof(UIButtonPress));action.GetComponent<Image>().sprite=_panel;action.GetComponent<Image>().type=Image.Type.Sliced;action.GetComponent<Image>().color=UguiTheme.LoginBtnBg;
            var le=Layout(action);le.minWidth=210;le.preferredWidth=210;le.flexibleWidth=0;
            var label=GoalText(action,"Label",30,64);Stretch(label.rectTransform,10);label.alignment=TextAlignmentOptions.Center;
            var view=root.GetComponent<GuideGoalView>();view.body=body.gameObject;view.description=description;view.progress=progress;view.actionButton=action.GetComponent<Button>();view.actionLabel=label;
        }
        private static TMP_Text GoalText(Transform parent,string name,float size,float height)
        {
            var rt=Child(parent,name,typeof(TextMeshProUGUI));var t=rt.GetComponent<TMP_Text>();t.font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/UGUI/Art/Font/Galmuri11 SDF.asset");Text(t,size,height);t.alignment=TextAlignmentOptions.MidlineLeft;return t;
        }
    }
}
