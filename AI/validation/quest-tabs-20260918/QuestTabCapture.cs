using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using KingdomIdle.Balance;
using KingdomIdle.UGUI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>실제 프리팹을 격리된 PreviewScene에서 고정 예시로 촬영한다. 실제 계정/입력 검사가 아니다.</summary>
public static class QuestTabCapture
{
    private const string Prefabs = "Assets/UGUI/Prefabs/";
    private static readonly string[] Labels = { "가이드", "일일", "주간", "업적" };

    /// <summary>원본 씬·프리팹·계정을 변경하지 않고 지정 비율의 정적 PNG를 저장한다.</summary>
    public static object Capture(string phase, int width, int height, string category)
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("정적 캡처는 EditMode에서 실행하세요.");
        if (phase != "before" && phase != "after") throw new ArgumentException("phase는 before/after여야 합니다.");
        if (width < 240 || height < 240 || width > 4096 || height > 4096) throw new ArgumentOutOfRangeException("capture size");
        bool after = phase == "after";
        eQuestCategory selected = eQuestCategory.Guide;
        if (after && !Enum.TryParse(category, true, out selected)) throw new ArgumentException("category는 Guide/Daily/Weekly/Achievement입니다.");
        if ((uint)selected > 3) throw new ArgumentOutOfRangeException(nameof(category));
        string outputCategory = after ? selected.ToString().ToLowerInvariant() : "all";
        var preview = EditorSceneManager.NewPreviewScene();
        var temporary = new List<Object>();
        RenderTexture previous = RenderTexture.active;
        RenderTexture target = null;
        try
        {
            GameObject ui = InstantiatePrefab(Prefabs + "UGUI_UIRoot.prefab", preview, null);
            DisableGameScripts(ui);
            Canvas canvas = ui.GetComponent<Canvas>();
            var scaler = ui.GetComponent<CanvasScaler>();
            Vector2 reference = scaler != null ? scaler.referenceResolution : new Vector2(1080, 1920);
            float match = scaler != null ? scaler.matchWidthOrHeight : 0f;
            float scale = Mathf.Pow(2, Mathf.Lerp(Mathf.Log(width / reference.x, 2), Mathf.Log(height / reference.y, 2), match));
            Vector2 logicalSize = new Vector2(width / scale, height / scale);
            if (scaler != null) scaler.enabled = false;

            // Camera RT와 같은 종횡비의 WorldSpace canvas로 실제 CanvasScaler의 논리 크기를 재현한다.
            canvas.renderMode = RenderMode.WorldSpace;
            var rootRect = (RectTransform)ui.transform;
            rootRect.position = Vector3.zero;
            rootRect.rotation = Quaternion.identity;
            rootRect.localScale = Vector3.one;
            rootRect.pivot = new Vector2(.5f, .5f);
            rootRect.sizeDelta = logicalSize;
            Transform panelLayer = ui.GetComponentsInChildren<Transform>(true).First(x => x.name == "LayerPanels");
            GameObject panel = InstantiatePrefab(Prefabs + "Panels/Panel_Guide.prefab", preview, panelLayer);
            DisableGameScripts(panel);
            Stretch((RectTransform)panel.transform);
            var view = panel.GetComponent<GuidePanelView>();
            var content = Field<RectTransform>(view, "listContent");
            var scroll = Field<ScrollRect>(view, "scroll");
            var label = Field<TMP_Text>(view, "progressLabel");
            var fill = Field<UnityEngine.UI.Image>(view, "progressFill");
            var tabs = Field<RectTransform>(view, "tabBar");
            Transform goal = scroll.transform.parent.Find("CurrentQuest");
            if (!after && tabs != null) throw new InvalidOperationException("탭 적용 후 프리팹으로 before를 위조하지 않습니다. 적용 전 상태에서 실행하세요.");
            if (after && tabs == null) throw new InvalidOperationException("after 전에 ApplyQuestTabs로 실제 프리팹 참조를 적용하세요.");
            foreach (Transform child in content.Cast<Transform>().ToArray()) Object.DestroyImmediate(child.gameObject);
            if (fill != null) fill.transform.parent.gameObject.SetActive(false);
            if (label != null) { label.gameObject.SetActive(!after); label.text = "가이드 · 일일 · 주간 · 업적"; }
            if (goal != null)
            {
                goal.gameObject.SetActive(!after || selected == eQuestCategory.Guide);
                var card = goal.GetComponent<GuideGoalView>();
                card.body.SetActive(true);
                card.description.text = "왕국군 3인 1차 전직";
                card.progress.text = "1/3";
                card.stepLabel.text = "가이드 15";
                card.actionLabel.text = "이동  ›";
                card.progressFill.fillAmount = 1f / 3;
            }
            if (after) BuildTabs(tabs, selected, preview);

            // 현재 카탈로그의 실제 설명/보상을 쓰되, 진행도는 독립된 예시 값으로 고정한다.
            QuestCatalog catalog = QuestCatalog.Parse(File.ReadAllText("Assets/_Project/Resources/Balance/catalog.json"));
            List<QuestDefinition> rows = catalog.Definitions.Where(q =>
                (q.Category == eQuestCategory.Guide ? q.QuestId == 10015 :
                 q.Category == eQuestCategory.Achievement ? catalog.GetPredecessor(q.QuestId) == 0 : true) &&
                (!after || q.Category == selected)).ToList();
            foreach (QuestDefinition definition in rows) AddRow(definition, catalog, preview, content);

            // 복제한 폰트와 atlas만 글리프를 추가한다. 원본 폰트나 에셋을 저장하지 않는다.
            CloneFonts(ui, temporary);
            Canvas.ForceUpdateCanvases();
            var fitter = view.Sheet.GetComponent<SheetSizeFitter>();
            if (fitter != null)
            {
                float sheetHeight = Mathf.Clamp(((RectTransform)view.Sheet.parent).rect.height - UguiTheme.StageControlsBottom - 24, 380, fitter.preferredHeight);
                view.Sheet.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, sheetHeight);
            }
            for (int pass = 0; pass < 3; pass++)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(view.Sheet);
                LayoutRebuilder.ForceRebuildLayoutImmediate(content);
                Canvas.ForceUpdateCanvases();
            }
            scroll.StopMovement();
            scroll.verticalNormalizedPosition = 1;
            foreach (TMP_Text text in ui.GetComponentsInChildren<TMP_Text>(true)) text.ForceMeshUpdate(true);
            Canvas.ForceUpdateCanvases();

            var cameraGo = new GameObject("QuestCaptureCamera", typeof(Camera));
            SceneManager.MoveGameObjectToScene(cameraGo, preview);
            Camera camera = cameraGo.GetComponent<Camera>();
            camera.scene = preview;
            camera.orthographic = true;
            camera.orthographicSize = logicalSize.y / 2;
            camera.aspect = (float)width / height;
            camera.transform.position = new Vector3(0, 0, -1000);
            camera.nearClipPlane = 1;
            camera.farClipPlane = 2000;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.025f, .025f, .028f, 1);
            canvas.worldCamera = camera;
            target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            target.Create();
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            var pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
            temporary.Add(pixels);
            pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            pixels.Apply();
            string path = Path.GetFullPath($"AI/validation/quest-tabs-20260918/{phase}-{outputCategory}-{width}x{height}.png");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, pixels.EncodeToPNG());
            return new { path, phase, category = outputCategory, width, height, logicalWidth = logicalSize.x,
                logicalHeight = logicalSize.y, rowCount = rows.Count, guideVisible = goal != null && goal.gameObject.activeSelf,
                mode = "Static PreviewScene sample using actual prefabs; no account, gameplay, input or device validation.",
                fontAssetsSaved = false, source = Prefabs + "Panels/Panel_Guide.prefab" };
        }
        finally
        {
            RenderTexture.active = previous;
            EditorSceneManager.ClosePreviewScene(preview);
            foreach (Object item in temporary.Distinct()) if (item != null) Object.DestroyImmediate(item);
            if (target != null) { target.Release(); Object.DestroyImmediate(target); }
        }
    }

    private static T Field<T>(object target, string name) where T : class
        => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(target) as T;

    private static GameObject InstantiatePrefab(string path, Scene scene, Transform parent)
    {
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (asset == null) throw new FileNotFoundException(path);
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(asset, scene);
        if (parent != null) instance.transform.SetParent(parent, false);
        return instance;
    }

    private static void DisableGameScripts(GameObject root)
    {
        foreach (MonoBehaviour script in root.GetComponentsInChildren<MonoBehaviour>(true))
            if (script != null && !(script is UIBehaviour)) script.enabled = false;
    }

    private static void Stretch(RectTransform rect)
    { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }

    private static void BuildTabs(RectTransform tabs, eQuestCategory selected, Scene scene)
    {
        foreach (Transform old in tabs.Cast<Transform>().ToArray()) Object.DestroyImmediate(old.gameObject);
        for (int i = 0; i < 4; i++)
        {
            var instance = InstantiatePrefab(Prefabs + "Items/Item_NavTabButton.prefab", scene, tabs);
            DisableGameScripts(instance);
            var tab = instance.GetComponent<NavTabButtonView>();
            tab.SetLabel(Labels[i]);
            tab.SetIcon(null);
            tab.SetSelected(i == (int)selected, UguiTheme.AccentGold);
            var size = instance.GetComponent<LayoutElement>();
            size.minWidth = size.preferredWidth = 0;
            size.flexibleWidth = 1;
        }
    }

    private static void AddRow(QuestDefinition definition, QuestCatalog catalog, Scene scene, Transform parent)
    {
        GameObject instance = InstantiatePrefab(Prefabs + "Items/Item_GuideStepRow.prefab", scene, parent);
        DisableGameScripts(instance);
        var row = instance.GetComponent<GuideStepRowView>();
        long progress = definition.QuestId == 10015 ? 1 : definition.ObjectiveType == eQuestObjectiveType.MonsterKill ? 105 :
            definition.ObjectiveType == eQuestObjectiveType.BattleTime ? 238 : definition.ObjectiveType == eQuestObjectiveType.SkillCast ? 5 : 0;
        progress = Math.Min(progress, definition.RequiredCount);
        bool complete = progress >= definition.RequiredCount;
        string reward = string.Join(" · ", catalog.GetRewards(definition.RewardGroupId).Select(x => x.IsDynamicGold ? "안전 사냥 2분 골드" :
            (x.Currency == eCurrency.AncientCoin ? "주화" : x.Currency == eCurrency.ClassFragment ? "전직 파편" : x.Currency == eCurrency.ArcaneKnowledge ? "마법 지식" : x.Currency.ToString()) + " " + x.Amount));
        row.Set(Labels[(int)definition.Category] + " · " + (complete ? "보상 수령" : $"{progress}/{definition.RequiredCount}"), definition.Description, reward, false);
        row.checkButton.gameObject.SetActive(true);
        var touch = row.checkButton.GetComponent<LayoutElement>() ?? row.checkButton.gameObject.AddComponent<LayoutElement>();
        touch.minWidth = touch.preferredWidth = touch.minHeight = touch.preferredHeight = 84;
        row.checkButton.interactable = complete;
        if (row.checkLabel != null) { row.checkLabel.fontSize = 23; row.checkLabel.text = complete ? "받기" : ""; }
        if (row.checkIcon != null) row.checkIcon.gameObject.SetActive(false);
        if (row.hintLabel != null) row.hintLabel.fontSize = 23;
    }

    private static void CloneFonts(GameObject root, List<Object> temporary)
    {
        foreach (var group in root.GetComponentsInChildren<TMP_Text>(true).Where(x => x.font != null).GroupBy(x => x.font))
        {
            TMP_FontAsset original = group.Key;
            TMP_FontAsset font = Object.Instantiate(original);
            font.hideFlags = HideFlags.HideAndDontSave;
            temporary.Add(font);
            font.atlasTextures = original.atlasTextures.Select(x => Object.Instantiate(x)).ToArray();
            temporary.AddRange(font.atlasTextures);
            font.material = new Material(original.material);
            font.material.mainTexture = font.atlasTextures[0];
            temporary.Add(font.material);
            font.fallbackFontAssetTable = new List<TMP_FontAsset>();
            string characters = string.Concat(group.Select(x => x.text));
            if (font.atlasPopulationMode != AtlasPopulationMode.Static)
                font.TryAddCharacters(characters);
            // 렌더 중 전역 폰트로 전파되는 동적 갱신을 피하고 기존 복제 atlas로만 그린다.
            temporary.AddRange(font.atlasTextures);
            font.atlasPopulationMode = AtlasPopulationMode.Static;
            foreach (TMP_Text text in group) { text.font = font; text.fontSharedMaterial = font.material; }
        }
    }
}
