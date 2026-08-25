using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace KingdomIdle.Divine.EditorTools
{
    /// <summary>
    /// 생성 아트(Assets/Generated/ComfyUI/DivineSkill/&lt;NameEng&gt;/)를 카드 SO 에 배선한다.
    ///
    /// 규약
    ///   &lt;NameEng&gt;_Icon.png       → icon              (HUD 슬롯 / 컬렉션 그리드)
    ///   &lt;NameEng&gt;_Standing.png   → illustration      (도감 상세 스탠딩)
    ///   &lt;NameEng&gt;_Cutscene.png   → cutInIllustration (궁극기 컷인)
    ///   Rings/Ring_&lt;Concept&gt;.png → buttonRingSprite  (컨셉 단위 공유)
    ///
    /// _Standing.png 이 없으면 예전 이름인 &lt;NameEng&gt;_Illustration.png 로 물러난다 —
    /// 도트 스탠딩/컷씬이 아직 없는 신(Ferrum 등)은 기존 NB2 일러스트를 그대로 쓴다.
    /// 아트가 없는 카드는 건너뛴다 — 8종 중 일부만 그려진 상태에서도 안전하게 반복 실행 가능.
    /// </summary>
    public static class DivineArtWire
    {
        private const string GenRoot = "Assets/Generated/ComfyUI/DivineSkill";
        private const string SoDir = "Assets/DivineSkill/SO";

        [MenuItem("KingdomIdle/Divine/Wire Generated Art")]
        public static void WireAll()
        {
            var cards = LoadAllCards();
            if (cards.Count == 0)
            {
                Debug.LogWarning("[DivineSkill] 카드 SO 를 찾지 못했습니다. 먼저 'Generate Cards + Registry' 를 실행하세요.");
                return;
            }

            int wired = 0, skipped = 0;

            foreach (var card in cards)
            {
                string key = string.IsNullOrEmpty(card.nameEng) ? card.name : card.nameEng;
                string dir = $"{GenRoot}/{key}";

                var icon = LoadSprite($"{dir}/{key}_Icon.png");
                // 도트 스탠딩이 있으면 그것을, 없으면 예전 NB2 일러스트를 도감 아트로 쓴다
                var illust = LoadSprite($"{dir}/{key}_Standing.png")
                          ?? LoadSprite($"{dir}/{key}_Illustration.png");
                var cutscene = LoadSprite($"{dir}/{key}_Cutscene.png");
                // 링 프레임은 컨셉 단위 공유 에셋 (루멘·아스트라가 Holy 링을 공유)
                var ring = LoadSprite($"{GenRoot}/Rings/Ring_{card.concept}.png");

                if (icon == null && illust == null && cutscene == null && ring == null)
                {
                    skipped++;
                    continue;
                }

                var so = new SerializedObject(card);
                if (icon != null) so.FindProperty("icon").objectReferenceValue = icon;
                if (illust != null) so.FindProperty("illustration").objectReferenceValue = illust;
                if (cutscene != null) so.FindProperty("cutInIllustration").objectReferenceValue = cutscene;
                if (ring != null) so.FindProperty("buttonRingSprite").objectReferenceValue = ring;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(card);
                wired++;

                Debug.Log($"[DivineSkill] {card.name}: icon={(icon != null ? "O" : "-")} standing={(illust != null ? "O" : "-")} cutscene={(cutscene != null ? "O" : "-")} ring={(ring != null ? "O" : "-")}");
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[DivineSkill] 생성 아트 배선 완료 — 배선 {wired}종 / 아트 없음 {skipped}종.");
        }

        private static Sprite LoadSprite(string path)
            => AssetDatabase.LoadAssetAtPath<Sprite>(path);

        private static List<DivineSkillSO> LoadAllCards()
        {
            var result = new List<DivineSkillSO>();
            foreach (string guid in AssetDatabase.FindAssets("t:DivineSkillSO", new[] { SoDir }))
            {
                var card = AssetDatabase.LoadAssetAtPath<DivineSkillSO>(AssetDatabase.GUIDToAssetPath(guid));
                if (card != null) result.Add(card);
            }
            return result;
        }
    }
}
