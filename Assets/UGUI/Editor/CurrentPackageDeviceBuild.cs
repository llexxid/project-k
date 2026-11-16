using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace KingdomIdle.UGUI.Editor
{
    /// <summary>기존 설치본의 패키지와 서명을 유지하는 수동 실기기 검증 빌드.</summary>
    public static class CurrentPackageDeviceBuild
    {
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                throw new InvalidOperationException("Use the project's Android Editor in Edit Mode.");
            string output = Required("KINGDOM_DEVICE_OUTPUT");
            string keystore = Required("KINGDOM_ANDROID_KEYSTORE");
            string passwordFile = Required("KINGDOM_ANDROID_PASSWORD_FILE");
            string alias = Required("KINGDOM_ANDROID_KEY_ALIAS");
            output = Path.GetFullPath(output);
            string project = Path.GetFullPath(Path.Combine(Application.dataPath, "..")).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (output.StartsWith(project, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(output.TrimEnd(Path.DirectorySeparatorChar), project.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase) ||
                !File.Exists(keystore) || !File.Exists(passwordFile))
                throw new InvalidOperationException("Use an external output folder and existing signing inputs.");
            string password = File.ReadAllText(passwordFile).TrimEnd('\r', '\n');
            if (string.IsNullOrEmpty(password)) throw new InvalidOperationException("Signing input is empty.");
            MagePolishPreparation.BakeAuthoredGlyphs();
            BronzeArmyPreparation.Apply();
            Debug.Log(Newtonsoft.Json.JsonConvert.SerializeObject(BronzeArmyPreparation.Validate()));
            GachaPanelPrefabGens.PrepareBronzeInfoLinks();
            GachaPanelPrefabGens.ValidateBronzeUi();
            BronzeGachaResultPreparation.Prepare();
            BronzeGachaResultPreparation.Validate();
            KingdomIdle.EditorTools.Optimization.OptAtlases.CreateInBuildAtlases();
            string purpose = Environment.GetEnvironmentVariable("KINGDOM_DEVICE_PURPOSE") ?? "UI 개선 테스트";
            int code = checked(PlayerSettings.Android.bundleVersionCode + 1);
            string version = PlayerSettings.bundleVersion;
            string label = $"{purpose} {version} (b{code})";
            string fileName = $"KingdomIdle_{version}_b{code}_UIReview.apk";
            string apk = Path.Combine(output, fileName);
            if (File.Exists(apk)) throw new InvalidOperationException("The build output already exists.");
            Directory.CreateDirectory(output);
            string originalProduct = PlayerSettings.productName;
            string originalKey = PlayerSettings.Android.keystoreName, originalAlias = PlayerSettings.Android.keyaliasName;
            string originalKeyPass = PlayerSettings.Android.keystorePass, originalAliasPass = PlayerSettings.Android.keyaliasPass;
            bool originalCustom = PlayerSettings.Android.useCustomKeystore, originalBundle = EditorUserBuildSettings.buildAppBundle;
            bool originalExport = EditorUserBuildSettings.exportAsGoogleAndroidProject;
            try
            {
                PlayerSettings.Android.bundleVersionCode = code;
                PlayerSettings.productName = label;
                PlayerSettings.Android.useCustomKeystore = true;
                PlayerSettings.Android.keystoreName = keystore;
                PlayerSettings.Android.keyaliasName = alias;
                PlayerSettings.Android.keystorePass = password;
                PlayerSettings.Android.keyaliasPass = password;
                EditorUserBuildSettings.buildAppBundle = false;
                EditorUserBuildSettings.exportAsGoogleAndroidProject = false;
                AssetDatabase.SaveAssets();
                AssetDatabase.ReleaseCachedFileHandles();
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                    scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                    target = BuildTarget.Android, locationPathName = apk, options = BuildOptions.None,
                    extraScriptingDefines = Array.Empty<string>()
                });
                File.WriteAllText(Path.Combine(output, "build.json"), Newtonsoft.Json.JsonConvert.SerializeObject(new {
                    apk, version, versionCode = code, label,
                    package = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android),
                    backend = PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android).ToString(),
                    architectures = PlayerSettings.Android.targetArchitectures.ToString(),
                    diagnostics = false, forcedAccount = false, development = false,
                    result = report.summary.result.ToString(), buildReportBytes = report.summary.totalSize,
                    apkBytes = File.Exists(apk) ? new FileInfo(apk).Length : 0,
                    errors = report.summary.totalErrors, warnings = report.summary.totalWarnings,
                    duration = report.summary.totalTime.ToString()
                }, Newtonsoft.Json.Formatting.Indented));
                File.WriteAllLines(Path.Combine(output, "build-messages.txt"), report.steps.SelectMany(step =>
                    step.messages.Select(message => message.type + ": " + step.name + ": " + message.content)));
                if (report.summary.result != BuildResult.Succeeded || report.summary.totalErrors != 0)
                    throw new BuildFailedException("Device build failed. See the external build report.");
            }
            finally
            {
                PlayerSettings.Android.keystorePass = originalKeyPass;
                PlayerSettings.Android.keyaliasPass = originalAliasPass;
                PlayerSettings.Android.keystoreName = originalKey;
                PlayerSettings.Android.keyaliasName = originalAlias;
                PlayerSettings.Android.useCustomKeystore = originalCustom;
                PlayerSettings.productName = originalProduct;
                EditorUserBuildSettings.buildAppBundle = originalBundle;
                EditorUserBuildSettings.exportAsGoogleAndroidProject = originalExport;
                AssetDatabase.SaveAssets();
            }
        }

        private static string Required(string key) => Environment.GetEnvironmentVariable(key) is string value && !string.IsNullOrWhiteSpace(value)
            ? value : throw new InvalidOperationException("Missing build configuration: " + key);
    }
}
