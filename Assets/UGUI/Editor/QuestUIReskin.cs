using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;

namespace KingdomIdle.UGUI.Editor
{
    /// <summary>
    /// 메인 씬의 기존 UGUI 가이드 퀘스트 팝업(GuideQuestPannel.prefab)을 픽셀 테마로 리스킨한다.
    /// 구조/스크립트 참조는 건드리지 않고 Image 스프라이트·색상, 텍스트 색만 조정한다.
    /// GenerateAll에서 try/catch로 격리 호출되므로 실패해도 전체 생성은 계속된다.
    /// </summary>
    internal static class QuestUIReskin
    {
        private const string PrefabPath = "Assets/_Project/Prefabs/QuestUI/GuideQuestPannel.prefab";

        internal static void Reskin()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                Debug.LogWarning($"[UguiGen] 가이드 퀘스트 프리팹 없음 — 리스킨 생략: {PrefabPath}");
                return;
            }

            var card = UguiGenAssets.KitCard;
            var font = UguiGenAssets.Font;

            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var images = root.GetComponentsInChildren<Image>(true);
                foreach (var img in images)
                {
                    if (img == null) continue;
                    string n = img.gameObject.name;

                    if (n == "GuideQuestPannel" || n == "Frame")
                    {
                        // 루트/프레임 = 어두운 픽셀 카드 배경
                        if (card != null)
                        {
                            img.sprite = card;
                            img.type = Image.Type.Sliced;
                            img.pixelsPerUnitMultiplier = 0.25f;
                        }
                        img.color = new Color(0.07f, 0.07f, 0.11f, 0.95f);
                    }
                    else
                    {
                        // 내부 서브 패널(Context/Progress/Reward)의 흰 UI 스프라이트 배경 제거
                        img.color = new Color(1f, 1f, 1f, 0f);
                    }
                }

                var texts = root.GetComponentsInChildren<TextMeshProUGUI>(true);
                foreach (var t in texts)
                {
                    if (t == null) continue;
                    if (font != null) t.font = font;
                    // 진행도는 골드, 제목/기타는 흰색
                    t.color = t.gameObject.name.Contains("Progress")
                        ? new Color(1f, 0.86f, 0.4f, 1f)
                        : new Color(1f, 1f, 1f, 0.95f);
                }

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log("[UguiGen] 가이드 퀘스트 팝업 픽셀 리스킨 완료");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
