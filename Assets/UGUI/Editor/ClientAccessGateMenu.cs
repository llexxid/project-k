using UnityEditor;

namespace KingdomIdle.UGUI.Editor
{
    internal static class ClientAccessGateMenu
    {
        private const string Path = "KingdomIdle/Access/Skip Check in Editor";

        [MenuItem(Path)]
        private static void Toggle() => ClientAccessGate.SkipInEditor = !ClientAccessGate.SkipInEditor;

        [MenuItem(Path, true)]
        private static bool Validate()
        {
            Menu.SetChecked(Path, ClientAccessGate.SkipInEditor);
            return !EditorApplication.isPlayingOrWillChangePlaymode;
        }
    }
}
