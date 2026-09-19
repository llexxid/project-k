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
