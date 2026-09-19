using System;
using System.IO;
using KingdomIdle.MageTower;
using UnityEditor;
using UnityEngine;

namespace KingdomIdle.UGUI.Editor
{
    public static class FoundationRevisionPreparation
    {
        public static void Prepare()
        {
            KingdomIdle.EditorTools.CombatPreparation.Prepare();
            MageSkillAssetPreparation.Build();
            PrepareFlow();
            EquipmentFeaturePreparation.Prepare();
            Edit("Popups/Popup_Profile.prefab", root => F.RectangularBar(root.GetComponent<ProfilePopupView>().xpFill));
            Edit("Huds/Hud_Party.prefab", root => {
                foreach (var member in root.GetComponent<PartyHudView>().members) F.RectangularBar(member.hpFill);
            });
            Edit("Panels/Panel_KACharacterSheet.prefab", root => F.RectangularBar(root.GetComponent<KACharacterSheetView>().hpFill));
            PlayerSettings.bundleVersion = "0.13.0";
            AssetDatabase.SaveAssets();
            CombatPresentationPreparation.Validate();
            Debug.Log("FOUNDATION COMBAT PREPARATION PASSED");
        }

        public static void PrepareFlow()
        {
            Scripts.Core.Parser.StageDataGenerator.Generate();
            ProgressionFlowPreparation.Prepare();
            MagePolishPreparation.BakeAuthoredGlyphs();
            AssetDatabase.SaveAssets();
            var stageReport = Scripts.Core.StageProgressionAcceptance.Run();
            Directory.CreateDirectory("Recordings/FoundationRevision/Editor");
            File.WriteAllText("Recordings/FoundationRevision/Editor/stage-acceptance.json", Newtonsoft.Json.JsonConvert.SerializeObject(stageReport, Newtonsoft.Json.Formatting.Indented));
            Debug.Log("FOUNDATION STAGE PREPARATION PASSED");
        }

        public static void PrepareVisuals()
        {
            KingdomIdle.EditorTools.CombatPreparation.Prepare();
            MageSkillAssetPreparation.Build();
            ProgressionFlowPreparation.Prepare();
            KingdomIdle.EditorTools.Optimization.OptAtlases.CreateInBuildAtlases();
            AssetDatabase.SaveAssets();
            CombatPresentationPreparation.Validate();
            Debug.Log("FOUNDATION VISUAL PREPARATION PASSED");
        }

        private static void Edit(string relative, Action<GameObject> action)
        {
            string path = "Assets/UGUI/Prefabs/" + relative;
            var root = PrefabUtility.LoadPrefabContents(path);
            try { action(root); PrefabUtility.SaveAsPrefabAsset(root, path); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        public static void BuildDevice() { Prepare(); TitleLobbyDeviceBuild.Build(); }
        public static void BuildFinal() { Prepare(); TitleLobbyDeviceBuild.BuildForManualTesting(); }
    }
}
