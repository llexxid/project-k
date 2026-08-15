using UnityEditor;
using UnityEngine;

namespace KingdomIdle.Divine.EditorTools
{
    /// <summary>
    /// 신 스킬 기능 전체를 한 번에 재생성한다 (카드 SO → VFX → 생성 아트 배선 → UGUI 프리팹 → 배선 검증).
    /// 순서에 의존성이 있으므로 개별 메뉴를 따로 돌리지 말고 가급적 이것을 쓴다.
    /// 전부 멱등이라 반복 실행해도 안전하다.
    /// </summary>
    public static class DivineBuildAll
    {
        [MenuItem("KingdomIdle/Divine/Build All (cards + vfx + art + ui)", false, 0)]
        public static void BuildAll()
        {
            Debug.Log("[DivineBuild] 1/5 카드 SO + 레지스트리");
            DivineSkillAssetGen.GenerateAll();

            Debug.Log("[DivineBuild] 2/5 Astra VFX 프리팹 + 카드 연출 배선");
            DivineVfxGen.GenerateAstraVfx();

            Debug.Log("[DivineBuild] 3/6 생성 아트(아이콘/일러스트) 배선");
            DivineArtWire.WireAll();

            // 매니저 설치를 빼먹으면 UI 만 살아 있고 기능 전체가 죽은 코드가 된다 —
            // 파이프라인이 런타임 절반 없이 재생성되는 일이 없도록 여기서 항상 함께 설치한다 (멱등)
            Debug.Log("[DivineBuild] 4/6 bootstrap 씬에 DivineSkillManager 설치");
            DivineSkillAssetGen.InstallManager();

            Debug.Log("[DivineBuild] 5/6 UGUI 프리팹 + 카탈로그");
            UGUI.Editor.UguiGenMenu.GenerateAll();

            Debug.Log("[DivineBuild] 6/6 UGUI 배선 검증");
            UGUI.Editor.UguiGenMenu.CheckViewWiring();

            AssetDatabase.SaveAssets();
            Debug.Log("[DivineBuild] 완료.");
        }

        /// <summary>
        /// 배치용 부분 마무리 — 매니저 설치 + 신 스킬 HUD 프리팹만 재생성.
        /// 전체 Generate All 은 무관한 프리팹 전부를 다시 써서(내부 fileID 재번호) diff 를 오염시키므로,
        /// 신 스킬 관련 산출물만 갱신할 때는 이쪽을 쓴다.
        /// </summary>
        public static void FinalizeDivineOnly()
        {
            Debug.Log("[DivineBuild] bootstrap 씬에 DivineSkillManager 설치");
            DivineSkillAssetGen.InstallManager();

            Debug.Log("[DivineBuild] Hud_DivineSkill 프리팹 재생성");
            UGUI.Editor.HudGens.GenerateDivineSkillHudOnly();

            AssetDatabase.SaveAssets();
            Debug.Log("[DivineBuild] 부분 마무리 완료.");
        }
    }
}
