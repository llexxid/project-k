using UnityEngine;
using UnityEngine.UI;

namespace KingdomIdle.UGUI.Editor
{
    /// <summary>UGUI_UIRoot 프리팹 생성: Canvas + 4레이어 + 매니저 컴포넌트 일체.</summary>
    internal static class RootCanvasGen
    {
        internal static GameObject Generate(UIViewCatalog catalog)
        {
            var rootRt = F.Root("UGUI_UIRoot");
            var go = rootRt.gameObject;

            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // QuestCanvas(main.unity, order 0)와의 정렬 모호성 제거 — 의도적으로 위에 배치
            canvas.sortingOrder = 10;

            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(UguiTheme.RefWidth, UguiTheme.RefHeight);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = UguiTheme.MatchWidthOrHeight;

            go.AddComponent<GraphicRaycaster>();

            var audio = go.AddComponent<AudioSource>();
            audio.playOnAwake = false;

            // ── SafeArea + 4레이어 (Graphic 없음 = 레이캐스트 통과) ──
            var safeArea = F.Container(rootRt, "SafeArea");
            F.Stretch(safeArea);
            safeArea.gameObject.AddComponent<SafeAreaFitter>();

            var layerScreens = F.Container(safeArea, "LayerScreens");
            F.Stretch(layerScreens);
            var layerPanels = F.Container(safeArea, "LayerPanels");
            F.Stretch(layerPanels);
            var layerPopups = F.Container(safeArea, "LayerPopups");
            F.Stretch(layerPopups);
            var layerOverlays = F.Container(safeArea, "LayerOverlays");
            F.Stretch(layerOverlays);

            // 데미지 텍스트 레이어 — 화면 레이어 최하단(게임 위, 다른 UI 아래)에 배치해
            // 패널/HUD를 가리지 않게 한다. 중첩 Canvas로 리빌드 격리(레이캐스터 없음 = 입력 통과).
            var damageLayer = F.Container(layerScreens, "DamageTextLayer");
            F.Stretch(damageLayer);
            damageLayer.gameObject.AddComponent<Canvas>();
            damageLayer.SetAsFirstSibling();

            // ── 매니저 컴포넌트 ──
            var uiMgr = go.AddComponent<UIManager>();
            uiMgr.catalog = catalog;
            uiMgr.layerScreens = layerScreens;
            uiMgr.layerPanels = layerPanels;
            uiMgr.layerPopups = layerPopups;
            uiMgr.layerOverlays = layerOverlays;

            var dmgMgr = go.AddComponent<DamageTextManager>();
            dmgMgr.layer = damageLayer;

            go.AddComponent<PartyHudController>();
            go.AddComponent<MageTowerHudController>();
            go.AddComponent<MageTowerHudBridge>();

            return PrefabGenUtil.SavePrefab(go, $"{PrefabGenUtil.PrefabRoot}/UGUI_UIRoot.prefab");
        }
    }
}
