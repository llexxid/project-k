using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace KingdomIdle.UGUI.Editor
{
    /// <summary>Installs alongside the game, with device diagnostics absent from normal builds.</summary>
    public static class TitleLobbyDeviceBuild
    {
        public static string Output => Environment.GetEnvironmentVariable("LOBBY_QA_OUTPUT") ?? "Recordings/LobbyRevision5/Device";
        internal static bool ResolvingDependencies { get; private set; }

        public static void Build()
        {
            Directory.CreateDirectory(Output);
            if (File.Exists(Output + "/build.txt")) File.Delete(Output + "/build.txt");
            if (File.Exists(Output + "/build-failure.txt")) File.Delete(Output + "/build-failure.txt");
            string identifier = PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.Android);
            string product = PlayerSettings.productName;
            bool keystore = PlayerSettings.Android.useCustomKeystore;
            bool bundle = EditorUserBuildSettings.buildAppBundle;
            var importMode = EditorSettings.refreshImportMode;
            int importWorkers = EditorUserSettings.desiredImportWorkerCount;
            var architectures = PlayerSettings.Android.targetArchitectures;
            const string templatePath = "Assets/Plugins/Android/mainTemplate.gradle";
            byte[] templateBefore = File.ReadAllBytes(templatePath);
            var resolverSettings = new Google.ProjectSettings("GooglePlayServices.");
            var resolverLocation = resolverSettings.UseProjectSettings ? Google.SettingsLocation.Project : Google.SettingsLocation.System;
            const string resolveOnBuildKey = "GooglePlayServices.AutoResolveOnBuild";
            bool resolveOnBuild = resolverSettings.GetBool(resolveOnBuildKey, true, resolverLocation);
            bool resolverPolicyChanged = false;
            try
            {
                PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, identifier + ".lobbyqa");
                PlayerSettings.productName = product + " Lobby QA";
                PlayerSettings.Android.useCustomKeystore = false;
                PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
                EditorUserBuildSettings.buildAppBundle = false;
                // Resolve the temporary package/ABI before Unity maps Gradle inputs for scene builds.
                // Resolving from EDM's scene callback can otherwise hit Windows error 1224.
                AssetDatabase.SaveAssets();
                LobbyQaResolverFileHandles.ReleaseCount = 0;
                ResolvingDependencies = true;
                try
                {
                    // Keep resolver imports in this process so its cached file handles
                    // can actually be released; import workers can retain their own mappings.
                    EditorSettings.refreshImportMode = AssetDatabase.RefreshImportMode.InProcess;
                    // An idle worker can retain a mapped Gradle input from a previous build.
                    // Ask Unity to close its workers before touching that file.
                    EditorUserSettings.desiredImportWorkerCount = 0;
                    AssetDatabase.ForceToDesiredWorkerCount();
                    AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                    AssetDatabase.ReleaseCachedFileHandles();
                    RemapResolverSettings();
                    PrepareArm64Exclusion(templatePath);
                    if (!GooglePlayServices.PlayServicesResolver.ResolveSync(true))
                        throw new InvalidOperationException("Android dependencies could not be resolved before the lobby QA build.");
                    // EDM can emit the old AGP spelling. Unity 6000.3 migrates this same
                    // token interactively; normalize it here so unattended QA does not stop.
                    AssetDatabase.ReleaseCachedFileHandles();
                    ReplaceTemplate(templatePath, System.Text.Encoding.UTF8.GetBytes(File.ReadAllText(templatePath).Replace("packagingOptions {", "packaging {")));
                    // ResolveSync above is mandatory and checked. EDM's second resolve in
                    // PostProcessScene rewrites its XML after Unity maps it on Windows.
                    // Suppress only that redundant pass for this QA build, then restore.
                    resolverPolicyChanged = true;
                    resolverSettings.SetBool(resolveOnBuildKey, false, resolverLocation);
                }
                finally
                {
                    Debug.Log("[Lobby QA] Import cache releases: " + LobbyQaResolverFileHandles.ReleaseCount);
                    ResolvingDependencies = false;
                }
                var options = new BuildPlayerOptions
                {
                    scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                    locationPathName = Output + "/KingdomIdle-LobbyQA.apk",
                    target = BuildTarget.Android,
                    options = BuildOptions.Development | BuildOptions.DetailedBuildReport,
                    extraScriptingDefines = new[] { "LOBBY_DEVICE_QA" }
                };
                BuildReport report = BuildPipeline.BuildPlayer(options);
                string summary = $"Result: {report.summary.result}\nBytes: {report.summary.totalSize}\nDuration: {report.summary.totalTime}\nErrors: {report.summary.totalErrors}\nWarnings: {report.summary.totalWarnings}\nPackage: {identifier}.lobbyqa\nBackend: {PlayerSettings.GetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Android)}\n";
                File.WriteAllText(Output + "/build.txt", summary);
                File.WriteAllLines(Output + "/build-messages.txt", report.steps.SelectMany(step => step.messages.Select(message => $"{message.type}: {step.name}: {message.content}")));
                Debug.Log("[Lobby Device Build] " + summary);
                if (report.summary.result != BuildResult.Succeeded || report.summary.totalErrors > 0)
                    throw new InvalidOperationException("Lobby device build failed. See build report and Editor log.");
            }
            catch (Exception exception)
            {
                File.WriteAllText(Output + "/build-failure.txt", exception.ToString());
                Debug.LogException(exception);
                throw;
            }
            finally
            {
                PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, identifier);
                PlayerSettings.productName = product;
                PlayerSettings.Android.useCustomKeystore = keystore;
                PlayerSettings.Android.targetArchitectures = architectures;
                EditorUserBuildSettings.buildAppBundle = bundle;
                AssetDatabase.SaveAssets();
                AssetDatabase.ReleaseCachedFileHandles();
                try
                {
                    ReplaceTemplate(templatePath, templateBefore);
                }
                finally
                {
                    try
                    {
                        if (resolverPolicyChanged)
                        {
                            RemapResolverSettings();
                            resolverSettings.SetBool(resolveOnBuildKey, resolveOnBuild, resolverLocation);
                        }
                    }
                    finally
                    {
                        EditorSettings.refreshImportMode = importMode;
                        EditorUserSettings.desiredImportWorkerCount = importWorkers;
                    }
                }
            }
        }

        static void PrepareArm64Exclusion(string path)
        {
            // This QA player is ARM64-only. Match EDM's ABI exclusion before it starts
            // importing local Maven plugins, when this template is still safe to write.
            // The full resolver still validates every dependency and the resulting template.
            string text = File.ReadAllText(path);
            int start = text.IndexOf("// Android Resolver Exclusions Start", StringComparison.Ordinal);
            int end = text.IndexOf("// Android Resolver Exclusions End", StringComparison.Ordinal);
            if (start < 0 || end <= start) return;
            string block = text.Substring(start, end - start);
            const string arm = "      exclude ('/lib/armeabi/*' + '*')";
            const string armV7 = "      exclude ('/lib/armeabi-v7a/*' + '*')";
            if (!block.Contains(arm) || block.Contains(armV7)) return;
            string newline = text.Contains("\r\n") ? "\r\n" : "\n";
            block = block.Replace(arm, arm + newline + armV7);
            ReplaceTemplate(path, System.Text.Encoding.UTF8.GetBytes(text.Substring(0, start) + block + text.Substring(end)));
        }

        static void RemapResolverSettings()
        {
            const string path = "ProjectSettings/GvhProjectSettings.xml";
            if (!File.Exists(path)) return;
            AssetDatabase.ReleaseCachedFileHandles();
            // Preserve every setting while detaching stale read mappings from this pathname.
            ReplaceFile(path, File.ReadAllBytes(path));
        }

        static void ReplaceTemplate(string path, byte[] bytes)
        {
            if (File.ReadAllBytes(path).SequenceEqual(bytes)) return;
            ReplaceFile(path, bytes);
        }

        static void ReplaceFile(string path, byte[] bytes)
        {
            string temporary = "Library/LobbyQa-" + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllBytes(temporary, bytes);
                try { File.Replace(temporary, path, null); }
                catch (IOException) when (File.Exists(temporary))
                {
                    // Windows ReplaceFile can fail to merge metadata on a mapped XML.
                    // Replace the directory entry without truncating the existing file.
#if UNITY_EDITOR_WIN
                    if (!MoveFileEx(Path.GetFullPath(temporary), Path.GetFullPath(path), 1u | 8u))
                        throw new System.ComponentModel.Win32Exception(System.Runtime.InteropServices.Marshal.GetLastWin32Error());
#else
                    throw;
#endif
                }
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
#if UNITY_EDITOR_WIN
        [System.Runtime.InteropServices.DllImport("kernel32.dll", EntryPoint="MoveFileExW", CharSet=System.Runtime.InteropServices.CharSet.Unicode, SetLastError=true)]
        [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
        static extern bool MoveFileEx(string source, string destination, uint flags);
#endif
    }

}
