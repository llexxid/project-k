using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace KingdomIdle.UGUI.Editor
{
    /// <summary>
    /// UGUI 마이그레이션 생성기 메뉴.
    /// "Generate All" → 모든 프리팹 + 카탈로그, "Switch to UGUI" → bootstrap 씬 전환.
    /// 배치 모드 진입점: KingdomIdle.UGUI.Editor.UguiGenMenu.GenerateAllAndRewire
    /// </summary>
    internal static class UguiGenMenu
    {
        [MenuItem("KingdomIdle/UGUI/Generate All (prefabs + catalog)", false, 0)]
        internal static void GenerateAll()
        {
            F.Init();

            if (F.Font == null)
                Debug.LogWarning("[UguiGen] Galmuri11 SDF 폰트를 찾지 못했습니다. TMP 기본 폰트로 생성됩니다.");

            var catalog = PrefabGenUtil.GetOrCreateCatalog();

            // 공용 에셋(폰트/SFX/픽셀 키트)을 먼저 배선 — 팩토리가 카탈로그를 참조한다
            CatalogGen.AssignSharedAssets(catalog);
            F.Catalog = catalog;

            // 루트 캔버스 (카탈로그 참조 포함)
            RootCanvasGen.Generate(catalog);

            // 화면
            ScreenGens.GenerateTitle();
            ScreenGens.GenerateMain();

            // 패널
            PanelGens.GeneratePlaceholder();
            PanelGens.GenerateGuide();
            PanelGens.GenerateGacha();
            PanelGens.GenerateKingdomArmy();
            PanelGens.GenerateDevelopment();
            PanelGens.GenerateInventory();

            // 오버레이/팝업
            OverlayGens.GenerateLoading();
            OverlayGens.GenerateToast();
            OverlayGens.GenerateSettings();
            OverlayGens.GenerateGachaResult();
            ProfilePopupPrefabGens.GenerateProfilePopup();

            // HUD
            HudGens.GeneratePartyHud();
            HudGens.GenerateMageTowerHud();
            HudGens.GenerateMainActionsHud();
            HudGens.GenerateDamageTextItem(CatalogGen.GetOrCreateDamageOutlineMaterial());

            // 아이템
            ItemGens.GenerateNavTabButton();
            ItemGens.GenerateGachaCard();
            ItemGens.GenerateCurrencyLine();
            ItemGens.GenerateGachaPullButton();
            ItemGens.GenerateRatePill();
            ItemGens.GenerateActionButton();
            ItemGens.GenerateEquipCell();
            ItemGens.GenerateJobCard();
            ItemGens.GenerateEnhanceCard();
            ItemGens.GenerateSkillRow();

            // 런타임 코드생성 → 프리팹 전환
            PopupGens.GenerateMageEquipSlot();
            PopupGens.GenerateMageSkillCell();
            PopupGens.GenerateMageTowerEquipPopup();
            MageTowerDetailPopupPrefabGens.GenerateMageTowerDetailPopup();
            GuidePanelPrefabGens.GenerateAll();
            DevelopmentPanelPrefabGens.GenerateAll();
            GachaPanelPrefabGens.GenerateAll();
            InventoryPanelPrefabGens.GenerateAll();
            KingdomArmyPanelPrefabGens.GenerateAll();
            DungeonFeaturePrefabGens.GenerateAll();

            // 프리팹 참조 배선 (프리팹 생성 후)
            CatalogGen.AssignPrefabs(catalog);

            // 기존 UGUI 가이드 퀘스트 팝업 픽셀 리스킨 (실패해도 전체 생성은 계속)
            try { QuestUIReskin.Reskin(); }
            catch (System.Exception ex) { Debug.LogError($"[UguiGen] 가이드 퀘스트 리스킨 실패: {ex}"); }

            AssetDatabase.Refresh();

            // 메뉴 실행 시에도 항상 검증 — 카탈로그 빈 필드/missing script를 바로 드러낸다
            CheckViewWiring();
            Debug.Log("[UguiGen] Generate All: OK");
        }

        [MenuItem("KingdomIdle/UGUI/Bootstrap/Switch to UGUI", false, 20)]
        internal static void SwitchToUgui()
        {
            BootstrapRewireGen.SwitchToUgui();
        }

        [MenuItem("KingdomIdle/UGUI/Bootstrap/Switch back to UITK", false, 21)]
        internal static void SwitchBackToUitk()
        {
            BootstrapRewireGen.SwitchBackToUitk();
        }

        [MenuItem("KingdomIdle/UGUI/Bootstrap/Remove UITK Root (final cleanup)", false, 22)]
        internal static void RemoveUitkRoot()
        {
            if (!EditorUtility.DisplayDialog("UITK 루트 제거",
                    "bootstrap.unity에서 UITK_UIRoot를 완전히 제거합니다.\n(UI Toolkit 스크립트 삭제 직전에 실행하세요)",
                    "제거", "취소"))
                return;
            BootstrapRewireGen.RemoveUitkRoot();
        }

        [MenuItem("KingdomIdle/UGUI/Validate/Check view wiring", false, 40)]
        internal static void CheckViewWiring()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<UIViewCatalog>(PrefabGenUtil.CatalogPath);
            if (catalog == null)
            {
                Debug.LogError("[UguiGen] 카탈로그가 없습니다. Generate All을 먼저 실행하세요.");
                return;
            }

            int errors = 0;

            // 카탈로그 자체의 빈 필드 검사
            foreach (var field in typeof(UIViewCatalog).GetFields(BindingFlags.Instance | BindingFlags.Public))
            {
                if (!typeof(Object).IsAssignableFrom(field.FieldType)) continue;
                if (field.GetValue(catalog) as Object == null)
                {
                    Debug.LogError($"[UguiGen] 카탈로그 필드 비어있음: {field.Name}");
                    errors++;
                }
            }

            // 각 프리팹의 View 직렬화 필드 검사
            errors += CheckPrefabViews(catalog.screenTitle);
            errors += CheckPrefabViews(catalog.screenMain);
            errors += CheckPrefabViews(catalog.panelPlaceholder);
            errors += CheckPrefabViews(catalog.panelGuide);
            errors += CheckPrefabViews(catalog.panelGacha);
            errors += CheckPrefabViews(catalog.panelKingdomArmy);
            errors += CheckPrefabViews(catalog.panelDevelopment);
            errors += CheckPrefabViews(catalog.panelInventory);
            errors += CheckPrefabViews(catalog.panelDungeon);
            errors += CheckPrefabViews(catalog.popupGachaResult);
            errors += CheckPrefabViews(catalog.popupDungeonClear);
            errors += CheckPrefabViews(catalog.popupReincarnation);
            errors += CheckPrefabViews(catalog.overlayLoading);
            errors += CheckPrefabViews(catalog.overlayToast);
            errors += CheckPrefabViews(catalog.overlaySettings);
            errors += CheckPrefabViews(catalog.hudParty);
            errors += CheckPrefabViews(catalog.hudMageTower);
            errors += CheckPrefabViews(catalog.hudMainActions);

            if (errors == 0)
                Debug.Log("[UguiGen] View 배선 검사: 통과");
            else
                Debug.LogError($"[UguiGen] View 배선 검사: {errors}건 문제 발견");
        }

        private static int CheckPrefabViews(GameObject prefab)
        {
            if (prefab == null) return 0;

            int errors = 0;
            var behaviours = prefab.GetComponentsInChildren<MonoBehaviour>(true);
            foreach (var mb in behaviours)
            {
                // 클래스명 ≠ 파일명 등으로 MonoScript 참조가 끊긴 경우 — 치명적 오류
                if (mb == null)
                {
                    Debug.LogError($"[UguiGen] {prefab.name}에 missing script가 있습니다! (View 클래스가 파일명과 일치하는지 확인)");
                    errors++;
                    continue;
                }
                var type = mb.GetType();
                if (type.Namespace == null || !type.Namespace.StartsWith("KingdomIdle.UGUI")) continue;

                foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
                {
                    if (field.GetCustomAttribute<SerializeField>() == null && !field.IsPublic) continue;
                    if (!typeof(Object).IsAssignableFrom(field.FieldType)) continue;

                    if (field.GetValue(mb) as Object == null)
                    {
                        Debug.LogWarning($"[UguiGen] {prefab.name}/{type.Name}.{field.Name} 필드가 비어있습니다.");
                        errors++;
                    }
                }
            }
            return errors;
        }

        /// <summary>배치 모드 진입점: 전체 생성 + bootstrap 전환 + 배선 검증.</summary>
        internal static void GenerateAllAndRewire()
        {
            GenerateAll();
            BootstrapRewireGen.SwitchToUgui();
            Debug.Log("[UguiGen] GenerateAllAndRewire: OK");
        }

        /// <summary>배치 모드 진입점: 생성만 (씬 전환 없음, 컴파일 검증용).</summary>
        internal static void GenerateAllOnly()
        {
            GenerateAll();
            Debug.Log("[UguiGen] GenerateAllOnly: OK");
        }
    }
}
