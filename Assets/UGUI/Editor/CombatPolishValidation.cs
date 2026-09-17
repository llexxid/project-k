using System.IO;
using UnityEditor;

namespace KingdomIdle.UGUI.Editor
{
    /// <summary>Focused visual rebuild and validation; does not rewrite stage or UI layouts.</summary>
    public static class CombatPolishValidation
    {
        public static void BuildDevice()
        {
            MageSkillAssetPreparation.Build();
            KingdomIdle.EditorTools.Optimization.OptAtlases.CreateInBuildAtlases();
            MagePolishPreparation.Validate();
            BalanceEditorValidation.Run();
            AssetDatabase.SaveAssets();
            Directory.CreateDirectory(TitleLobbyDeviceBuild.Output);
            File.Copy("Recordings/BalanceRevision/Editor/validation.json", TitleLobbyDeviceBuild.Output + "/editor-validation.json", true);
            TitleLobbyDeviceBuild.Build();
        }
    }
}
