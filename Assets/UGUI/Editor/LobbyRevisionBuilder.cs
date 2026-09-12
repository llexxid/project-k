using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace KingdomIdle.UGUI.Editor
{
    /// <summary>Targeted existing-prefab and audio wiring for the lobby revision.</summary>
    public static class LobbyRevisionBuilder
    {
        const string AudioRoot = "Assets/_Project/Audio/BGM_SunoAI/";
        public static void Apply()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit play mode first.");
            TitleLobbyBuilder.Rebuild();
            WireSettings();
            foreach (var path in Directory.GetFiles(AudioRoot, "*.mp3", SearchOption.AllDirectories))
            {
                var importer = (AudioImporter)AssetImporter.GetAtPath(path.Replace('\\', '/'));
                var settings = importer.defaultSampleSettings;
                settings.loadType = AudioClipLoadType.Streaming;
                settings.compressionFormat = AudioCompressionFormat.Vorbis;
                settings.quality = .62f;
                settings.sampleRateSetting = AudioSampleRateSetting.OverrideSampleRate;
                settings.sampleRateOverride = 44100;
                settings.preloadAudioData = false;
                importer.defaultSampleSettings = settings;
                importer.SetOverrideSampleSettings("Android", settings);
                importer.loadInBackground = true;
                importer.forceToMono = false;
                importer.SaveAndReimport();
            }
            var bootstrap = EditorSceneManager.OpenScene("Assets/_Project/Scenes/buildScenes/bootstrap.unity");
            var sfx = UnityEngine.Object.FindFirstObjectByType<Scripts.Core.SFXManager>();
            if (sfx == null) throw new InvalidOperationException("Bootstrap SFXManager missing.");
            var serialized = new SerializedObject(sfx);
            void Playlist(string field, string folder)
            {
                var paths = Directory.GetFiles(AudioRoot + folder, "*.mp3").OrderBy(p => p, StringComparer.Ordinal).ToArray();
                var clips = serialized.FindProperty(field); clips.arraySize = paths.Length;
                for (int i = 0; i < paths.Length; i++) clips.GetArrayElementAtIndex(i).objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(paths[i].Replace('\\', '/'));
            }
            Playlist("_lobbyMusic", "Lobby"); Playlist("_combatMusic", "InGame");
            serialized.FindProperty("_musicVolume").floatValue = .65f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(bootstrap); EditorSceneManager.SaveScene(bootstrap);
            var title = EditorSceneManager.OpenScene("Assets/_Project/Scenes/buildScenes/title.unity");
            foreach (var source in UnityEngine.Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None))
                if (source.gameObject.name == "BGM") { source.Stop(); source.playOnAwake = false; source.clip = null; }
            EditorSceneManager.MarkSceneDirty(title); EditorSceneManager.SaveScene(title);
            KingdomIdle.EditorTools.Optimization.OptAtlases.CreateInBuildAtlases();
            PrewarmLabels();
            AssetDatabase.SaveAssets();
            Debug.Log("[Lobby R2] Title, shared low-spec settings and six streamed Suno tracks wired.");
        }

        static void WireSettings()
        {
            const string path = "Assets/UGUI/Prefabs/Overlays/Overlay_Settings.prefab";
            var go = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var view = go.GetComponent<SettingsModalView>();
                if (view.tglLowSpec == null)
                {
                    var original = view.tglPowerSave.transform.parent;
                    var row = UnityEngine.Object.Instantiate(original.gameObject, original.parent, false);
                    row.name = "Row_LowSpec"; row.transform.SetAsFirstSibling();
                    view.tglLowSpec = row.GetComponentInChildren<Toggle>(true);
                    row.GetComponentInChildren<TMP_Text>(true).text = "저사양 모드";
                }
                foreach (var toggle in new[] { view.tglLowSpec, view.tglPowerSave, view.tglDamageText, view.tglScreenShake })
                {
                    var row = toggle.transform.parent;
                    var layout = row.GetComponent<LayoutElement>();
                    layout.minHeight = layout.preferredHeight = 136;
                }
                var grid = view.tglLowSpec.transform.parent.parent;
                var hint = grid.parent.Find("LowSpecHint");
                if (hint == null)
                {
                    var text = UnityEngine.Object.Instantiate(view.lblServer, grid.parent, false);
                    text.name = "LowSpecHint";
                    text.transform.SetSiblingIndex(grid.GetSiblingIndex() + 1);
                    text.text = "저사양: 로비·장식 연출과 데미지 애니메이션 간소화";
                    text.fontSize = 24; text.textWrappingMode = TextWrappingModes.Normal;
                    text.alignment = TextAlignmentOptions.Left;
                    var layout = text.GetComponent<LayoutElement>() ?? text.gameObject.AddComponent<LayoutElement>();
                    layout.minHeight = layout.preferredHeight = 64; layout.flexibleWidth = 1;
                }
                PrefabUtility.SaveAsPrefabAsset(go, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(go); }
        }

        static void PrewarmLabels()
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/UGUI/Art/Font/Galmuri11 SDF.asset");
            const string labels = "현재 언어: 한국어 저사양 모드 켬 끔 연출 간소화 로비·장식 연출과 데미지 애니메이션 간소화Current language: EnglishLow-spec mode onoff · reduced effects";
            if (font == null) return;
            string needed = new string(labels.Distinct().Where(c => !font.HasCharacter(c)).ToArray());
            if (needed.Length == 0) return;
            if (!font.TryAddCharacters(needed, out var missing))
                Debug.LogWarning("[Lobby R2] Font could not prewarm: " + missing);
            EditorUtility.SetDirty(font);
        }
    }
}
