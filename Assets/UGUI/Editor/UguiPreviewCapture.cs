using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using KingdomIdle.UI;

namespace KingdomIdle.UGUI.Editor
{
    /// <summary>
    /// 생성된 UGUI 프리팹을 실제로 렌더링해 PNG로 저장하는 진단 도구.
    /// (플레이 없이 UI 외형을 확인하기 위한 용도 — Unity를 -batchmode 로 실행하되
    ///  -nographics 는 빼야 렌더링이 된다.)
    /// </summary>
    internal static class UguiPreviewCapture
    {
        private const int W = 1080;
        private const int H = 1920;
        private static readonly string OutDir = Path.Combine(Path.GetTempPath(), "ugui_preview");

        internal static void CaptureAll()
        {
            Directory.CreateDirectory(OutDir);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 렌더용 카메라
            var camGo = new GameObject("PreviewCam");
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.15f, 0.35f, 0.15f, 1f);   // 게임 배경 대용(초원 느낌)
            cam.transform.position = new Vector3(0, 0, -100);

            var catalog = AssetDatabase.LoadAssetAtPath<UIViewCatalog>(PrefabGenUtil.CatalogPath);
            if (catalog == null)
            {
                Debug.LogError("[Preview] 카탈로그 없음");
                return;
            }

            var rootPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabGenUtil.PrefabRoot}/UGUI_UIRoot.prefab");
            var rootGo = (GameObject)PrefabUtility.InstantiatePrefab(rootPrefab, scene);

            var canvas = rootGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 10f;

            // UIManager가 Awake를 못 도니 레이어를 직접 찾는다
            var layers = new Dictionary<string, RectTransform>();
            foreach (var rt in rootGo.GetComponentsInChildren<RectTransform>(true))
                layers[rt.name] = rt;

            var shots = new (string name, GameObject prefab, string layer)[]
            {
                ("01_title",       catalog.screenTitle,      "LayerScreens"),
                ("02_main",        catalog.screenMain,       "LayerScreens"),
                ("03_kingdomarmy", catalog.panelKingdomArmy, "LayerPanels"),
                ("04_settings",    catalog.overlaySettings,  "LayerOverlays"),
                ("05_gacharesult", catalog.popupGachaResult, "LayerOverlays"),
            };

            foreach (var s in shots)
            {
                if (s.prefab == null) continue;
                if (!layers.TryGetValue(s.layer, out var parent)) continue;

                var inst = (GameObject)PrefabUtility.InstantiatePrefab(s.prefab, scene);
                inst.transform.SetParent(parent, false);
                var irt = (RectTransform)inst.transform;
                irt.anchorMin = Vector2.zero; irt.anchorMax = Vector2.one;
                irt.offsetMin = Vector2.zero; irt.offsetMax = Vector2.zero;
                inst.SetActive(true);

                // 메인 화면일 땐 하단바/상단바가 보이도록 그대로 두고 캡처
                Canvas.ForceUpdateCanvases();
                LayoutRebuilder.ForceRebuildLayoutImmediate(irt);
                Canvas.ForceUpdateCanvases();

                Render(cam, Path.Combine(OutDir, s.name + ".png"));

                Object.DestroyImmediate(inst);
            }

            CaptureNavTabs(scene, cam, catalog, layers);
            CaptureGachaWidgets(scene, cam, catalog, layers);
            CaptureKingdomArmyWidgets(scene, cam, catalog, layers);

            Debug.Log($"[Preview] 캡처 완료: {OutDir}");
        }

        /// <summary>
        /// 탭/네비 버튼은 런타임에 생성되므로 프리팹 캡처엔 안 잡힌다.
        /// 실제 컨트롤러와 동일하게 아이콘·라벨·선택 상태를 넣어 샘플 바를 렌더링한다.
        /// </summary>
        private static void CaptureNavTabs(UnityEngine.SceneManagement.Scene scene, Camera cam,
            UIViewCatalog catalog, Dictionary<string, RectTransform> layers)
        {
            if (catalog == null || catalog.itemNavTabButton == null) return;
            if (!layers.TryGetValue("LayerPanels", out var parent)) return;

            var host = new GameObject("NavTabPreview", typeof(RectTransform));
            var hostRt = (RectTransform)host.transform;
            hostRt.SetParent(parent, false);
            hostRt.anchorMin = new Vector2(0f, 0.5f);
            hostRt.anchorMax = new Vector2(1f, 0.5f);
            hostRt.pivot = new Vector2(0.5f, 0.5f);
            hostRt.offsetMin = new Vector2(40f, -200f);
            hostRt.offsetMax = new Vector2(-40f, 200f);
            var col = host.AddComponent<VerticalLayoutGroup>();
            col.spacing = 24f;
            col.childControlWidth = true;
            col.childControlHeight = true;
            col.childForceExpandWidth = true;

            // 왕국군 네비 (종합/장비/스킬/전직) + 뽑기 탭 두 줄
            MakeBar(host.transform, catalog, 104f, new[]
            {
                ("종합", catalog.iconUser), ("장비", catalog.iconSword),
                ("스킬", catalog.iconBook), ("전직", catalog.iconStar),
            }, selectedIndex: 1, activeBg: UguiTheme.AccentBlue);

            MakeBar(host.transform, catalog, 104f, new[]
            {
                ("장비 뽑기", catalog.iconChest), ("마탑 스킬 뽑기", catalog.iconWand),
            }, selectedIndex: 0, activeBg: new Color(80f / 255f, 60f / 255f, 180f / 255f, 0.6f));

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(hostRt);
            Canvas.ForceUpdateCanvases();

            Render(cam, Path.Combine(OutDir, "06_navtabs.png"));
            Object.DestroyImmediate(host);
        }

        private static void MakeBar(Transform parent, UIViewCatalog catalog, float height,
            (string label, Sprite icon)[] items, int selectedIndex, Color activeBg)
        {
            var bar = new GameObject("Bar", typeof(RectTransform));
            bar.transform.SetParent(parent, false);
            var row = bar.AddComponent<HorizontalLayoutGroup>();
            row.spacing = 8f;
            row.childControlWidth = true;
            row.childControlHeight = true;
            row.childForceExpandWidth = true;
            var le = bar.AddComponent<LayoutElement>();
            le.preferredHeight = height;

            for (int i = 0; i < items.Length; i++)
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(catalog.itemNavTabButton);
                go.transform.SetParent(bar.transform, false);
                var v = go.GetComponent<NavTabButtonView>();
                if (v == null) continue;
                v.SetLabel(items[i].label);
                v.SetIcon(items[i].icon);
                v.SetSelected(i == selectedIndex, activeBg);
            }
        }

        /// <summary>뽑기 옵션 버튼 / 확률 알약 프리팹을 실제 데이터로 렌더링해 확인.</summary>
        private static void CaptureGachaWidgets(UnityEngine.SceneManagement.Scene scene, Camera cam,
            UIViewCatalog catalog, Dictionary<string, RectTransform> layers)
        {
            if (catalog == null || !layers.TryGetValue("LayerPanels", out var parent)) return;

            var host = new GameObject("GachaWidgetPreview", typeof(RectTransform));
            var hostRt = (RectTransform)host.transform;
            hostRt.SetParent(parent, false);
            hostRt.anchorMin = new Vector2(0f, 0.5f);
            hostRt.anchorMax = new Vector2(1f, 0.5f);
            hostRt.pivot = new Vector2(0.5f, 0.5f);
            hostRt.offsetMin = new Vector2(40f, -240f);
            hostRt.offsetMax = new Vector2(-40f, 240f);
            var col = host.AddComponent<VerticalLayoutGroup>();
            col.spacing = 24f; col.childControlWidth = true; col.childControlHeight = true; col.childForceExpandWidth = true;

            // 확률 알약 행
            if (catalog.itemRatePill != null)
            {
                var pillRow = new GameObject("Pills", typeof(RectTransform));
                pillRow.transform.SetParent(host.transform, false);
                var pr = pillRow.AddComponent<HorizontalLayoutGroup>();
                pr.spacing = 8f; pr.childControlWidth = false; pr.childControlHeight = false; pr.childForceExpandWidth = false; pr.childAlignment = TextAnchor.MiddleLeft;
                pillRow.AddComponent<LayoutElement>().preferredHeight = 56f;
                MakePill(pillRow.transform, catalog, "일반  70.0%", UguiTheme.RarityNormal);
                MakePill(pillRow.transform, catalog, "레어  25.0%", UguiTheme.RarityRare);
                MakePill(pillRow.transform, catalog, "에픽  5.0%", UguiTheme.RarityEpic);
            }

            // 뽑기 옵션 버튼 행
            if (catalog.itemGachaPullButton != null)
            {
                var btnRow = new GameObject("Pulls", typeof(RectTransform));
                btnRow.transform.SetParent(host.transform, false);
                var br = btnRow.AddComponent<HorizontalLayoutGroup>();
                br.spacing = 14f; br.childControlWidth = true; br.childControlHeight = true; br.childForceExpandWidth = true;
                btnRow.AddComponent<LayoutElement>().preferredHeight = 140f;
                MakePull(btnRow.transform, catalog, "1회 뽑기", "1,000 고대주화", true);
                MakePull(btnRow.transform, catalog, "10연 뽑기", "10,000 고대주화", false);
            }

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(hostRt);
            Canvas.ForceUpdateCanvases();
            Render(cam, Path.Combine(OutDir, "07_gachawidgets.png"));
            Object.DestroyImmediate(host);
        }

        private static void MakePill(Transform parent, UIViewCatalog catalog, string text, Color c)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(catalog.itemRatePill);
            go.transform.SetParent(parent, false);
            var v = go.GetComponent<RatePillView>();
            if (v != null) v.Set(text, c);
        }

        private static void MakePull(Transform parent, UIViewCatalog catalog, string title, string cost, bool afford)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(catalog.itemGachaPullButton);
            go.transform.SetParent(parent, false);
            var v = go.GetComponent<GachaPullButtonView>();
            if (v != null) v.Set(title, cost, afford, catalog.iconChest);
        }

        /// <summary>장비 셀 / 전직 카드 / 강화 카드 / 스킬 행 프리팹을 샘플 데이터로 렌더링.</summary>
        private static void CaptureKingdomArmyWidgets(UnityEngine.SceneManagement.Scene scene, Camera cam,
            UIViewCatalog catalog, Dictionary<string, RectTransform> layers)
        {
            if (catalog == null || !layers.TryGetValue("LayerPanels", out var parent)) return;

            var host = new GameObject("KaWidgetPreview", typeof(RectTransform));
            var hostRt = (RectTransform)host.transform;
            hostRt.SetParent(parent, false);
            hostRt.anchorMin = new Vector2(0f, 1f); hostRt.anchorMax = new Vector2(1f, 1f); hostRt.pivot = new Vector2(0.5f, 1f);
            hostRt.anchoredPosition = new Vector2(0f, -120f);
            hostRt.offsetMin = new Vector2(40f, hostRt.offsetMin.y);
            hostRt.sizeDelta = new Vector2(-80f, 1600f);
            var col = host.AddComponent<VerticalLayoutGroup>();
            col.spacing = 20f; col.childControlWidth = true; col.childControlHeight = true;
            col.childForceExpandWidth = true; col.childForceExpandHeight = false; col.padding = new RectOffset(0, 0, 0, 0);

            // 장비 셀 / 전직 카드 그리드
            if (catalog.itemEquipCell != null || catalog.itemJobCard != null)
            {
                var gridGo = new GameObject("Grid", typeof(RectTransform));
                gridGo.transform.SetParent(host.transform, false);
                var g = gridGo.AddComponent<GridLayoutGroup>();
                g.cellSize = new Vector2(180f, 240f); g.spacing = new Vector2(12f, 12f);
                g.constraint = GridLayoutGroup.Constraint.FixedColumnCount; g.constraintCount = 5;
                gridGo.AddComponent<LayoutElement>().preferredHeight = 500f;

                if (catalog.itemEquipCell != null)
                {
                    MakeEquipCell(gridGo.transform, catalog, "롱소드 +3", UguiTheme.RarityRare, "ATK +42  HP +10", UguiTheme.RarityRare, true, false, "장착 중");
                    MakeEquipCell(gridGo.transform, catalog, "고대 검 +1", UguiTheme.RarityEpic, "ATK +88", UguiTheme.RarityEpic, false, false, null);
                    MakeEquipCell(gridGo.transform, catalog, "낡은 단검", UguiTheme.RarityNormal, "ATK +5", UguiTheme.RarityNormal, false, true, null);
                }
                if (catalog.itemJobCard != null)
                {
                    MakeJobCard(gridGo.transform, catalog, "Knight", "현재", UguiTheme.AccentGoldStrong, "HP 320 / ATK 45", "무료 재전직", UguiTheme.SuccessGreenBright, null, new Color(1f, 230f/255f, 100f/255f, 0.12f), new Color(1f, 230f/255f, 100f/255f, 1f));
                    MakeJobCard(gridGo.transform, catalog, "Archer", "전직가능", UguiTheme.SuccessGreenBright, "HP 240 / ATK 60", "전직 파편 40/40", UguiTheme.SuccessGreenBright, null, new Color(1f,1f,1f,0.07f), null);
                }
            }

            // 강화 카드
            if (catalog.itemEnhanceCard != null)
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(catalog.itemEnhanceCard);
                go.transform.SetParent(host.transform, false);
                var v = go.GetComponent<EnhanceCardView>();
                if (v != null)
                {
                    v.Set("공격력", "Lv. 12", "현재 효과  +24%");
                    if (catalog.itemGachaPullButton != null)
                    {
                        for (int i = 0; i < 2; i++)
                        {
                            var b = (GameObject)PrefabUtility.InstantiatePrefab(catalog.itemGachaPullButton);
                            b.transform.SetParent(v.ButtonRow, false);
                            b.GetComponent<GachaPullButtonView>()?.Set(i == 0 ? "강화 x1" : "강화 x10", i == 0 ? "50 G" : "480 G", i == 0);
                        }
                    }
                }
            }

            // 스킬 행
            if (catalog.itemSkillRow != null)
            {
                MakeSkillRow(host.transform, catalog, "강타", "적에게 200% 피해", false);
                MakeSkillRow(host.transform, catalog, "인내", "받는 피해 15% 감소", true);
            }

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(hostRt);
            Canvas.ForceUpdateCanvases();
            Render(cam, Path.Combine(OutDir, "08_kawidgets.png"));
            Object.DestroyImmediate(host);
        }

        private static void MakeEquipCell(Transform parent, UIViewCatalog cat, string name, Color nameColor, string sub, Color rarity, bool equipped, bool dim, string state)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(cat.itemEquipCell);
            go.transform.SetParent(parent, false);
            go.GetComponent<EquipCellView>()?.Set(null, name, nameColor, sub, rarity, equipped, dim, state);
        }

        private static void MakeJobCard(Transform parent, UIViewCatalog cat, string jobName, string badge, Color badgeC, string stat, string frag, Color fragC, string prereq, Color bg, Color? frame)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(cat.itemJobCard);
            go.transform.SetParent(parent, false);
            var v = go.GetComponent<JobCardView>();
            if (v == null) return;
            // JobData 없이 이름/스탯만 세팅 (job=null 이면 이미지·이름은 View가 처리하므로 라벨 직접 지정)
            v.Set(null, bg, frame, badge, badgeC, stat, frag, fragC, prereq);
            if (v.nameLabel != null) v.nameLabel.text = jobName;
        }

        private static void MakeSkillRow(Transform parent, UIViewCatalog cat, string name, string detail, bool passive)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(cat.itemSkillRow);
            go.transform.SetParent(parent, false);
            go.GetComponent<SkillRowView>()?.Set(name, detail, passive);
        }

        private static void Render(Camera cam, string path)
        {
            var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            cam.Render();

            RenderTexture.active = rt;
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
            tex.Apply();

            File.WriteAllBytes(path, tex.EncodeToPNG());

            RenderTexture.active = null;
            cam.targetTexture = null;
            Object.DestroyImmediate(tex);
            rt.Release();
            Object.DestroyImmediate(rt);
        }
    }
}
