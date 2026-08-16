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
            Debug.Log("[DivineBuild] 1/9 카드 SO + 레지스트리");
            DivineSkillAssetGen.GenerateAll();

            Debug.Log("[DivineBuild] 2/9 Astra VFX 프리팹 + 카드 연출 배선");
            DivineVfxGen.GenerateAstraVfx();

            Debug.Log("[DivineBuild] 3/9 나머지 7카드 VFX 프리팹 + 연출 배선");
            DivineVfxGen7.GenerateAllCardVfx();

            Debug.Log("[DivineBuild] 4/9 생성 아트(아이콘/일러스트) 배선");
            DivineArtWire.WireAll();

            // 매니저 설치를 빼먹으면 UI 만 살아 있고 기능 전체가 죽은 코드가 된다 —
            // 파이프라인이 런타임 절반 없이 재생성되는 일이 없도록 여기서 항상 함께 설치한다 (멱등)
            Debug.Log("[DivineBuild] 5/9 bootstrap 씬에 DivineSkillManager 설치");
            DivineSkillAssetGen.InstallManager();

            // 뽑기 테이블은 아트 배선(4/9) 뒤에 생성해야 카드 아이콘이 엔트리에 따라온다
            Debug.Log("[DivineBuild] 6/9 신 스킬 뽑기 테이블 생성");
            DivineGachaGen.GenerateTable();

            Debug.Log("[DivineBuild] 7/9 bootstrap 씬 GachaManager 에 뽑기 테이블 배선");
            DivineGachaGen.WireTableIntoScene();

            Debug.Log("[DivineBuild] 8/9 UGUI 프리팹 + 카탈로그");
            UGUI.Editor.UguiGenMenu.GenerateAll();

            Debug.Log("[DivineBuild] 9/9 UGUI 배선 검증");
            UGUI.Editor.UguiGenMenu.CheckViewWiring();

            AssetDatabase.SaveAssets();
            Debug.Log("[DivineBuild] 완료.");
        }

        /// <summary>배치용 — 마탑 환경 오브젝트 프리팹만 재생성 (배치 위치 조정 등 반복 작업용).</summary>
        public static void RegenMageTowerEnvOnly()
        {
            UGUI.Editor.UguiGenMenu.RegenMageTowerEnv();
            AssetDatabase.SaveAssets();
            Debug.Log("[DivineBuild] Hud_MageTowerEnv 재생성 완료.");
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
