using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Newtonsoft.Json;

namespace KingdomIdle.UGUI.Editor
{
    public static class SettingsEditorValidation
    {
        public static void BuildAndroid() { Run(); TitleLobbyDeviceBuild.Build(); }
        public static void Run()
        {
            const string directory = "Recordings/SettingsRevision/Editor"; Directory.CreateDirectory(directory);
            try
            {
                SettingsRevisionBuilder.Rebuild();
                var result = SettingsAcceptance.Run();
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/UGUI/Prefabs/Overlays/Overlay_Settings.prefab");
                var view = prefab.GetComponent<SettingsModalView>();
                var legacy = new HashSet<string> { "tglPush", "tglNightPush", "btnWithdraw", "btnSave" };
                var serialized = new SerializedObject(view); var field = serialized.GetIterator(); int count = 0;
                while (field.NextVisible(true)) if (field.propertyType == SerializedPropertyType.ObjectReference && !legacy.Contains(field.name))
                { count++; if (field.objectReferenceValue == null) throw new Exception("Settings reference missing: " + field.propertyPath); }
                if (view.numberButtons.Length != 3 || view.scroll == null || view.scroll.viewport.GetComponent<UnityEngine.UI.RectMask2D>() == null)
                    throw new Exception("Notation selector or masked scroll missing.");
                var labels = prefab.GetComponentsInChildren<TMPro.TMP_Text>(true);
                string glyphs = new string((string.Concat(labels.Select(t => t.text)) + "선택됨지수억조경해자양eKMBTQaQiSxSpOc0123456789변경즉시적용자동저장").Distinct().ToArray());
                var font = view.numberPreview.font;
                string needed = new string(glyphs.Where(c => !char.IsWhiteSpace(c) && !font.HasCharacter(c, true)).ToArray());
                if (needed.Length > 0 && !font.TryAddCharacters(needed, out string missing) && !string.IsNullOrWhiteSpace(missing)) throw new Exception("Missing settings glyphs: " + missing);
                EditorUtility.SetDirty(font); AssetDatabase.SaveAssets();
                result["settingsReferences"] = count; result["labels"] = labels.Length;
                File.WriteAllText(directory + "/validation.json", JsonConvert.SerializeObject(result, Formatting.Indented));
                Debug.Log("SETTINGS VALIDATION PASSED " + result["passed"]);
            }
            catch (Exception error) { File.WriteAllText(directory + "/failure.txt", error.ToString()); throw; }
        }
    }
}
