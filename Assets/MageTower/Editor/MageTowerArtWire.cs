using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace KingdomIdle.MageTower.EditorTools
{
    /// <summary>
    /// 생성 아이콘(Assets/Generated/ComfyUI/MageTower/&lt;Key&gt;/&lt;Key&gt;_Icon.png)을 스킬 SO 에 배선한다.
    /// Key = nameEng 에서 공백 제거 ("Ice Spike" → "IceSpike") — SO 폴더 관례와 일치.
    /// 아이콘이 없는 스킬은 건너뛴다 (DivineArtWire 와 동일한 멱등 패턴).
    /// </summary>
    public static class MageTowerArtWire
    {
        private const string GenRoot = "Assets/Generated/ComfyUI/MageTower";
        private const string SoDir = "Assets/MageTower/SO";

        [MenuItem("KingdomIdle/MageTower/Wire Generated Icons")]
        public static void WireAll()
        {
            var skills = LoadAllSkills();
            if (skills.Count == 0)
            {
                Debug.LogWarning("[MageTower] 스킬 SO 를 찾지 못했습니다.");
                return;
            }

            int wired = 0, skipped = 0;

            foreach (var skill in skills)
            {
                string key = (string.IsNullOrEmpty(skill.nameEng) ? skill.name : skill.nameEng).Replace(" ", "");
                var icon = AssetDatabase.LoadAssetAtPath<Sprite>($"{GenRoot}/{key}/{key}_Icon.png");
                if (icon == null)
                {
                    skipped++;
                    continue;
                }

                var so = new SerializedObject(skill);
                so.FindProperty("icon").objectReferenceValue = icon;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(skill);
                wired++;
                Debug.Log($"[MageTower] {skill.name}: icon O ({key})");
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[MageTower] 아이콘 배선 완료 — 배선 {wired}종 / 아이콘 없음 {skipped}종.");
        }

        private static List<MageTowerSkillSO> LoadAllSkills()
        {
            var result = new List<MageTowerSkillSO>();
            foreach (string guid in AssetDatabase.FindAssets("t:MageTowerSkillSO", new[] { SoDir }))
            {
                var so = AssetDatabase.LoadAssetAtPath<MageTowerSkillSO>(AssetDatabase.GUIDToAssetPath(guid));
                if (so != null) result.Add(so);
            }
            return result;
        }
    }
}
