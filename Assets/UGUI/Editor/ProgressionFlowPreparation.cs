using System;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace KingdomIdle.UGUI.Editor
{
    public static class ProgressionFlowPreparation
    {
        public static void Prepare()
        {
            F.Init();
            Edit("Screens/Screen_Main.prefab", ApplyMain);
            Edit("Popups/Popup_DungeonClear.prefab", ApplyResult);
        }

        internal static void ApplyMain(GameObject root)
        {
            var wave = root.GetComponent<MainScreenView>().waveHud;
            var area = (RectTransform)wave.transform;
            RemoveLayout(area);
            Pin(area, new Vector2(.5f, 0), new Vector2(0, 370), new Vector2(620, 86), new Vector2(.5f, 0));
            if (area.GetComponent<StageBadgeAnchor>() == null) area.gameObject.AddComponent<StageBadgeAnchor>();
            if (area.GetComponent<CanvasGroup>() == null) area.gameObject.AddComponent<CanvasGroup>();
            var row = (RectTransform)wave.lblStage.transform.parent;
            RemoveLayout(row);
            Pin(row, new Vector2(0, .5f), Vector2.zero, new Vector2(436, 78), new Vector2(0, .5f));
            F.Stretch(wave.lblStage.rectTransform);
            wave.lblStage.rectTransform.offsetMin = new Vector2(12, 8);
            wave.lblStage.rectTransform.offsetMax = new Vector2(-12, -8);
            wave.lblStage.fontSizeMax = wave.lblStage.fontSize = 30;
            wave.lblStage.fontSizeMin = 24;
            wave.lblStage.enableAutoSizing = true;
            // Keep the timer aligned with its own stage badge if an older prefab parented it to the HUD.
            wave.bossTimerBar.transform.SetParent(row, false);
            Pin((RectTransform)wave.bossTimerBar.transform, new Vector2(.5f, 0), new Vector2(0, 3), new Vector2(404, 5), new Vector2(.5f, 0));

            if (wave.bossChallengeRoot != null) Object.DestroyImmediate(wave.bossChallengeRoot);
            var touch = F.Box(area, "BossChallenge", new Color(0, 0, 0, .001f), false, true);
            Pin(touch.rectTransform, new Vector2(1, .5f), Vector2.zero, new Vector2(176, 86), new Vector2(1, .5f));
            var toggle = touch.gameObject.AddComponent<Toggle>();
            toggle.targetGraphic = touch;
            toggle.transition = Selectable.Transition.None;
            var box = F.Box(touch.transform, "CheckFrame", UguiTheme.Bronze, false);
            Pin(box.rectTransform, new Vector2(0, .5f), new Vector2(10, 0), new Vector2(38, 38), new Vector2(0, .5f));
            var inset = F.Box(box.transform, "Inset", UguiTheme.RusticPanelDeep, false);
            F.Stretch(inset.rectTransform); inset.rectTransform.offsetMin = Vector2.one * 3; inset.rectTransform.offsetMax = Vector2.one * -3;
            var check = F.Box(box.transform, "Check", UguiTheme.AccentGold, false);
            check.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UGUI/Art/LayerLab/GUI Pro-MinimalGame/Shared/Icons/PictoIcon/64/check.png");
            if (check.sprite == null) throw new InvalidOperationException("Boss checkbox sprite missing");
            check.preserveAspect = true;
            F.Stretch(check.rectTransform); check.rectTransform.offsetMin = Vector2.one * 5; check.rectTransform.offsetMax = Vector2.one * -5;
            toggle.graphic = check;
            toggle.isOn = true;
            var label = F.Text(touch.transform, "LblBossChain", "보스 자동", 24, UguiTheme.Parchment, TextAlignmentOptions.MidlineLeft);
            Pin(label.rectTransform, new Vector2(1, .5f), new Vector2(-2, 0), new Vector2(116, 64), new Vector2(1, .5f));
            label.textWrappingMode = TextWrappingModes.NoWrap;
            wave.bossChallengeRoot = touch.gameObject;
            wave.tglBossChain = toggle;

            var panel = wave.deathTimerFill.transform.parent.parent;
            AddTimer(wave.deathPopup, panel, wave.deathTimerFill);
        }

        internal static void ApplyResult(GameObject root)
        {
            var view = root.GetComponent<DungeonClearPopupView>();
            var panel = view.panel;
            var oldGuide = panel.Find("LblGuide");
            if (oldGuide != null) Object.DestroyImmediate(oldGuide.gameObject);
            var existing = panel.Find("ReturnTimerBar");
            var fill = existing != null ? existing.Find("Fill").GetComponent<Image>() :
                F.HFillBar(panel, "ReturnTimerBar", UguiTheme.RusticPanelDeep, UguiTheme.AccentGold, out _);
            AddTimer(root, panel, fill);
        }

        private static void AddTimer(GameObject root, Transform panel, Image fill)
        {
            var label = panel.Find("ReturnCountdown")?.GetComponent<TMP_Text>() ??
                F.Text(panel, "ReturnCountdown", "6초 후 전투에 복귀", 26, UguiTheme.TextSecondary, TextAlignmentOptions.Center);
            F.Preferred(label, height: 38);
            label.transform.SetAsLastSibling();
            var track = (RectTransform)fill.transform.parent;
            track.SetAsLastSibling();
            F.Preferred(track, height: 10);
            F.RectangularBar(fill);
            fill.color = UguiTheme.AccentGold;
            var timer = root.GetComponent<StageReturnTimerView>() ?? root.AddComponent<StageReturnTimerView>();
            timer.label = label; timer.fill = fill;
        }

        private static void RemoveLayout(Transform node)
        {
            foreach (var component in node.GetComponents<Component>())
                if (component is LayoutGroup || component is LayoutElement || component is ContentSizeFitter) Object.DestroyImmediate(component);
        }
        private static void Pin(RectTransform rt, Vector2 anchor, Vector2 position, Vector2 size, Vector2 pivot)
        { rt.anchorMin = rt.anchorMax = anchor; rt.pivot = pivot; rt.sizeDelta = size; rt.anchoredPosition = position; }
        private static void Edit(string relative, Action<GameObject> edit)
        {
            string path = "Assets/UGUI/Prefabs/" + relative;
            var root = PrefabUtility.LoadPrefabContents(path);
            try { edit(root); PrefabUtility.SaveAsPrefabAsset(root, path); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
