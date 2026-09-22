using System;
using System.IO;
using KingdomIdle.Balance;
using KingdomIdle.Gacha;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace KingdomIdle.UGUI.Editor
{
    public static class BalanceFixValidation
    {
        public static void Run()
        {
            const string output = "Recordings/BalanceFix20260922/Editor";
            Directory.CreateDirectory(output);
            var root = new GameObject("BalanceFixAcceptance");
            var previous = EquipmentManager.Instance;
            try
            {
                var equipment = root.AddComponent<EquipmentManager>();
                EquipmentManager.Instance = equipment;
                var serialized = new SerializedObject(equipment);
                serialized.FindProperty("_database").objectReferenceValue = AssetDatabase.LoadAssetAtPath<EquipmentDatabase>("Assets/_Project/Scripts/Player/Equipment/Prefab/Equipment.asset");
                serialized.ApplyModifiedPropertiesWithoutUndo();
                var gacha = root.AddComponent<GachaManager>();
                var table = AssetDatabase.LoadAssetAtPath<GachaTableSO>("Assets/Gacha/SO/GachaTable_Equipment.asset");
                var report = BalanceFixAcceptance.Run(equipment, gacha, table);
                var firstClear = Scripts.Core.StageCatalogRules.Get(Scripts.Core.eStageType.Main, 1, 3).Encounter.FirstClear;
                var reward = equipment.GetByRarity(eEquipmentRarity.Normal).Find(x => x.IsAllowedForJob(firstClear.WeaponJob) && x.bonusAtk == firstClear.WeaponAttack);
                if (firstClear.WeaponJob != "Mage" || firstClear.WeaponAttack != 10 || reward == null || !reward.CanBeRewarded)
                    throw new InvalidOperationException("Stage 1-3 must grant the same-power staff instead of a bow.");
                report["firstClearWeapon"] = reward.equipmentName;
                File.WriteAllText(output + "/acceptance.json", JsonConvert.SerializeObject(report, Formatting.Indented));
                using (LocalProgression.BeginTestSession()) BalanceEditorValidation.Run();
                Debug.Log("BALANCE FIX ACCEPTANCE PASSED " + report["passed"]);
            }
            catch (Exception e)
            {
                File.WriteAllText(output + "/failure.txt", e.ToString());
                throw;
            }
            finally { UnityEngine.Object.DestroyImmediate(root); EquipmentManager.Instance = previous; }
        }

        public static void BuildManual()
        {
            Run();
            TitleLobbyDeviceBuild.BuildForManualTesting();
        }

        public static void RegenerateAndBuildManual()
        {
            Scripts.Core.Parser.StageDataGenerator.Generate();
            BuildManual();
        }
    }
}
