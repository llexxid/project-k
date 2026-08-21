using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace KingdomIdle.UGUI.Editor
{
    /// <summary>
    /// 생성 초상화(Assets/Generated/ComfyUI/Portraits/&lt;JobAsset&gt;_Portrait.png)를
    /// JobData.portraitSprite 에 배선한다. Key = JobData 에셋 파일명 (Spearman, Elite_Knight, ...).
    /// 초상화가 없는 직업은 건너뛴다 — jobSprite 폴백(JobData.Portrait)이 있어 안전하다.
    /// </summary>
    internal static class JobPortraitWire
    {
        private const string GenRoot = "Assets/Generated/ComfyUI/Portraits";
        private const string SoDir = "Assets/_Project/Scripts/Player/Job/SO";

        [MenuItem("KingdomIdle/UGUI/Wire Job Portraits", false, 7)]
        public static void WireAll()
        {
            var jobs = LoadAllJobs();
            if (jobs.Count == 0)
            {
                Debug.LogWarning("[JobPortrait] JobData 에셋을 찾지 못했습니다.");
                return;
            }

            int wired = 0, skipped = 0;

            foreach (var job in jobs)
            {
                string key = Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(job));
                var portrait = AssetDatabase.LoadAssetAtPath<Sprite>($"{GenRoot}/{key}_Portrait.png");
                if (portrait == null)
                {
                    skipped++;
                    continue;
                }

                var so = new SerializedObject(job);
                so.FindProperty("portraitSprite").objectReferenceValue = portrait;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(job);
                wired++;
                Debug.Log($"[JobPortrait] {key}: portrait O");
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[JobPortrait] 초상화 배선 완료 — 배선 {wired}종 / 초상화 없음 {skipped}종.");
        }

        private static List<JobData> LoadAllJobs()
        {
            var result = new List<JobData>();
            foreach (string guid in AssetDatabase.FindAssets("t:JobData", new[] { SoDir }))
            {
                var job = AssetDatabase.LoadAssetAtPath<JobData>(AssetDatabase.GUIDToAssetPath(guid));
                if (job != null) result.Add(job);
            }
            return result;
        }
    }
}
