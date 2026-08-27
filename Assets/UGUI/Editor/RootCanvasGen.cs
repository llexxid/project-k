using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace KingdomIdle.UGUI.Editor
{
    /// <summary>
    /// UGUI_UIRoot 프리팹 생성: Canvas + BattleUIArea(데미지/HP바) + SafeArea 4레이어 + 매니저 일체.
    /// 팀원이 수동 스냅샷(UGUI_UIRootChanged)에 추가했던 HP바 시스템을 생성기에 편입했다 —
    /// 생성 프리팹의 수동 복사본은 금방 썩는다(마탑/신스킬 컨트롤러 누락 사고의 원인). 루트 확장은 반드시 여기서.
    /// </summary>
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

            // ── BattleUIArea — 전장 위 스크린스페이스 UI (데미지 텍스트 + 몬스터 HP바) ──
            // SafeArea보다 형제 순서상 먼저 = 모든 메뉴/HUD 뒤에 그려진다. 세이프에어리어 밖 전체
            // 화면 rect 를 쓴다 — 월드→스크린 좌표 변환(ScreenPointToLocalPointInRectangle)은
            // 전체 화면 기준이 정확하다. (팀원 HP바 구조 병합, 2026-08-26)
            var battleArea = F.Container(rootRt, "BattleUIArea");
            F.Stretch(battleArea);

            // 데미지 텍스트 레이어 — 중첩 Canvas로 리빌드 격리(레이캐스터 없음 = 입력 통과).
            var damageLayer = F.Container(battleArea, "DamageTextLayer");
            F.Stretch(damageLayer);
            damageLayer.gameObject.AddComponent<Canvas>();

            // HP바 레이어 — 몬스터 머리 위 체력바 풀 (HpBarManager 가 매 프레임 위치 갱신).
            var hpbarLayer = F.Container(battleArea, "HpbarLayer");
            F.Stretch(hpbarLayer);
            hpbarLayer.gameObject.AddComponent<Canvas>();
            var hpBarPool = hpbarLayer.gameObject.AddComponent<HpBarPool>();
            hpbarLayer.gameObject.AddComponent<HpBarManager>();

            // HpBarPool.hpBar 는 전역 네임스페이스의 private 필드 — SerializedObject 로 배선
            var hpBarPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Project/Prefabs/UI/HPBar.prefab");
            if (hpBarPrefab != null)
            {
                var poolSo = new SerializedObject(hpBarPool);
                poolSo.FindProperty("hpBar").objectReferenceValue = hpBarPrefab;
                poolSo.ApplyModifiedPropertiesWithoutUndo();
            }
            else
            {
                Debug.LogWarning("[UguiGen] HPBar.prefab 을 찾지 못했습니다 — HP바 풀이 비어 생성됩니다.");
            }

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
            // (좌측 마탑 스킬 슬롯 HUD 제거됨 — 마탑 진입/AUTO 토글은 MageTowerEnvController가 담당)
            go.AddComponent<MageTowerEnvController>();

            // ── 신 스킬(Divine) 시스템 비활성화 (2026-08-27, 기획 보류) ──
            // 게임에 영향 0 을 위해 UI 컨트롤러 3종을 루트에서 뺀다 (매 프레임 폴링/숨은 HUD 인스턴스 제거).
            // 코드·프리팹·SO·아트는 전부 보존 — 재활성화 절차:
            //   ① 아래 3줄 주석 해제 후 메뉴 "KingdomIdle/UGUI/Generate UI Root"
            //   ② 메뉴 "KingdomIdle/Divine/Install Manager Into Bootstrap" (bootstrap 에 매니저 재설치)
            //   ③ 메뉴 "KingdomIdle/Divine/Wire Gacha Table Into Scene" (신 뽑기 탭 복원)
            //   (햄버거 도감 버튼은 매니저 존재 여부로 자동 표시/숨김 — MainScreenController.BindMenus)
            // go.AddComponent<DivineSkillHudController>();
            // go.AddComponent<DivineSkillHudBridge>();
            // go.AddComponent<DivineCutInController>();   // 컷인 재생기 — Awake에서 DivinePresentation.CutInHandler에 등록

            return PrefabGenUtil.SavePrefab(go, $"{PrefabGenUtil.PrefabRoot}/UGUI_UIRoot.prefab");
        }
    }
}
