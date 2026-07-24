// 에디터 전용 유틸리티(MenuItem/AssetDatabase/SerializedObject 등 UnityEditor API 사용).
// 런타임 폴더에 있으므로 UNITY_EDITOR로 가드하지 않으면 플레이어 빌드에서 컴파일 실패한다.
#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class EquipmentTestAssetGenerator
{
    private const string TestFolderPath = "Assets/Scripts/Player/Equipment/Prefab/Test";

    private static readonly (string jobName, eJobFlag jobFlag)[] Jobs =
    {
        ("Archer", eJobFlag.Archer),
        ("Knight", eJobFlag.Knight),
        ("Mage", eJobFlag.Mage),
    };

    private static readonly (eEquipmentRarity rarity, int atk, int hp, int idOffset)[] Rarities =
    {
        (eEquipmentRarity.Normal, 10, 100, 0),
        (eEquipmentRarity.Rare, 30, 300, 100),
        (eEquipmentRarity.Epic, 80, 800, 200),
    };

    [MenuItem("MyTools/Equipment/Create Test Weapon Assets")]
    public static void CreateTestWeaponAssets()
    {
        EnsureFolder(TestFolderPath);

        List<EquipmentData> createdOrUpdated = new();
        int jobIndex = 0;

        foreach (var job in Jobs)
        {
            foreach (var rarity in Rarities)
            {
                string assetName = $"{EquipmentTestGrantUtility.TestEquipmentNamePrefix}{job.jobName}_{rarity.rarity}_Weapon";
                string assetPath = $"{TestFolderPath}/{assetName}.asset";

                EquipmentData data = AssetDatabase.LoadAssetAtPath<EquipmentData>(assetPath);
                if (data == null)
                {
                    data = ScriptableObject.CreateInstance<EquipmentData>();
                    AssetDatabase.CreateAsset(data, assetPath);
                }

                data.equipmentName = assetName;
                data.description = $"{job.jobName} {rarity.rarity} test weapon";
                data.icon = null;
                data.slot = eEquipmentSlot.Weapon;
                data.rarity = rarity.rarity;

                // Keep legacy fields mirrored so current unmodified stat code can still be tested.
                data.bonusAtk = rarity.atk;
                data.bonusMaxHP = rarity.hp;
                data.maxEnhancementLevel = 5 + ((int)rarity.rarity * 5);
                data.atkGrowthPerLevel = 0.1f;
                data.hpGrowthPerLevel = 0.1f;
                data.enhanceMaterialCount = 2;
                data.enhanceSuccessRates = new List<float> { 1f, 0.8f, 0.6f, 0.4f, 0.2f };

                data.MainOption = new List<EquipmentOption>
                {
                    new EquipmentOption { type = EquipmentStatType.AtkFlat, value = rarity.atk, isPercent = false },
                    new EquipmentOption { type = EquipmentStatType.HpFlat, value = rarity.hp, isPercent = false },
                };
                //data.AdditionalOption = new List<EquipmentOption>();
                data.ReinforceOption = new List<EquipmentOption>
                {
                    new EquipmentOption { type = EquipmentStatType.AtkFlat, value = Mathf.Max(1, rarity.atk / 10f), isPercent = false },
                    new EquipmentOption { type = EquipmentStatType.HpFlat, value = Mathf.Max(1, rarity.hp / 10f), isPercent = false },
                };

                SerializedObject serializedData = new(data);
                serializedData.FindProperty("_jobMask").intValue = (int)job.jobFlag;
                serializedData.FindProperty("_itemId").intValue = 1000 + rarity.idOffset + jobIndex;
                serializedData.ApplyModifiedPropertiesWithoutUndo();

                EditorUtility.SetDirty(data);
                createdOrUpdated.Add(data);
            }

            jobIndex++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[EquipmentTestAssetGenerator] Created or updated {createdOrUpdated.Count} test weapon assets in {TestFolderPath}.");
    }

    [MenuItem("MyTools/Equipment/Grant Test Weapons To Players")]
    public static void GrantTestWeaponsToPlayers()
    {
        if (!EditorApplication.isPlaying)
        {
            Debug.LogWarning("[EquipmentTestAssetGenerator] Enter Play Mode before granting equipment to players.");
            return;
        }

        EquipmentTestGrantUtility.GrantAllTestWeaponsToAllPlayers(LoadTestWeapons(), allowDuplicates: false);
    }

    [MenuItem("MyTools/Equipment/Grant Test Weapons To Players (Duplicates)")]
    public static void GrantDuplicateTestWeaponsToPlayers()
    {
        if (!EditorApplication.isPlaying)
        {
            Debug.LogWarning("[EquipmentTestAssetGenerator] Enter Play Mode before granting equipment to players.");
            return;
        }

        EquipmentTestGrantUtility.GrantAllTestWeaponsToAllPlayers(LoadTestWeapons(), allowDuplicates: true);
    }

    private static List<EquipmentData> LoadTestWeapons()
    {
        string[] guids = AssetDatabase.FindAssets("t:EquipmentData", new[] { TestFolderPath });
        return guids
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<EquipmentData>)
            .Where(data => data != null)
            .ToList();
    }

    private static void EnsureFolder(string folderPath)
    {
        string[] parts = folderPath.Split('/');
        string current = parts[0];

        for (int i = 1; i < parts.Length; i++)
        {
            string next = $"{current}/{parts[i]}";
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }

        Directory.CreateDirectory(folderPath);
    }
}
#endif
