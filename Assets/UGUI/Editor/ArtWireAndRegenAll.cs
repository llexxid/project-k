using UnityEditor;
using UnityEngine;

namespace KingdomIdle.UGUI.Editor
{
    /// <summary>
    /// 생성 아트 배선 + 카드/프리팹 재생성 + 프리뷰 캡처를 한 번에 도는 배치 진입점.
    /// 에디터가 닫힌 상태에서: Unity.exe -batchmode -quit -executeMethod
    ///   KingdomIdle.UGUI.Editor.ArtWireAndRegenAll.RunAll  (캡처가 있으므로 -nographics 금지)
    /// </summary>
    internal static class ArtWireAndRegenAll
    {
        [MenuItem("KingdomIdle/UGUI/Wire Art + Generate All + Capture", false, 30)]
        public static void RunAll()
        {
            // 1. 카드 SO 재생성 (concept 필드 반영) + 생성 아트 배선
            Divine.EditorTools.DivineSkillAssetGen.GenerateAll();
            Divine.EditorTools.DivineArtWire.WireAll();
            JobPortraitWire.WireAll();
            MageTower.EditorTools.MageTowerArtWire.WireAll();

            // 2. UGUI 프리팹 전체 재생성 (+카탈로그)
            UguiGenMenu.GenerateAll();

            // 3. 시각 검증 캡처
            UguiPreviewCapture.CaptureAll();

            Debug.Log("[Polish2] Wire + Regen + Capture 완료");
        }
    }
}
