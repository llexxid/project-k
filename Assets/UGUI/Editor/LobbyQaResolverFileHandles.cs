using UnityEditor;

namespace KingdomIdle.UGUI.Editor
{
    /// <summary>Release Windows file mappings only while preparing an opt-in lobby QA build.</summary>
    public sealed class LobbyQaResolverFileHandles : AssetPostprocessor
    {
        internal static int ReleaseCount;
        void OnPreprocessAsset() { Release(); }
        static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom) { Release(); }
        static void Release()
        {
            if (!TitleLobbyDeviceBuild.ResolvingDependencies) return;
            AssetDatabase.ReleaseCachedFileHandles();
            ReleaseCount++;
        }
    }
}
