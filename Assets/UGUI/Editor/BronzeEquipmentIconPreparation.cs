using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>청동 원정대 장비 표시 아트만 연결한다. 아이템 코드와 게임 데이터는 수정하지 않는다.</summary>
public static class BronzeEquipmentIconPreparation
{
    private const string EquipmentRoot = "Assets/_Project/Scripts/Player/Equipment/Prefab/";
    private const string IconRoot = "Assets/_Project/Art/Icons/Equipment/";

    private readonly struct Entry
    {
        public readonly string Asset;
        public readonly string Name;
        public readonly string Key;
        public readonly int Rarity;
        public readonly int JobMask;
        public readonly int ItemId;

        public Entry(string asset, string name, string key, int rarity, int jobMask, int itemId)
        {
            Asset = EquipmentRoot + asset + ".asset";
            Name = name;
            Key = key;
            Rarity = rarity;
            JobMask = jobMask;
            ItemId = itemId;
        }
    }

    // Explicit asset paths preserve the existing catalog, including the inactive bow family.
    private static readonly Entry[] Entries =
    {
        new("Normal/Knight/GreatSword_Normal_1", "GreatSword_Normal_1", "Wooden_Great_Sword", 0, 4, 1),
        new("Normal/Knight/GreatSword_Normal_2", "GreatSword_Normal_2", "Basic_Great_Sword", 0, 4, 2),
        new("Normal/Knight/GreatSword_Normal_3", "GreatSword_Normal_3", "Basic_Wooden_Great_Sword", 0, 4, 3),
        new("Rare/Knight_Rare/GreatSword_Rare_1", "GreatSword_Rare_1", "Golden_Great_Sword", 1, 4, 1),
        new("Rare/Knight_Rare/GreatSword_Rare_2", "GreatSword_Rare_2", "Golden_Steal_Axe", 1, 4, 2),
        new("Epic/GreatSword_Epic_1", "GreatSword_Epic_1", "Excalibur", 2, 4, 1),
        new("Normal/Mage/Staff_Normal_1", "Staff_Normal_1", "Wooden_Stafff", 0, 1, 7),
        new("Normal/Mage/Staff_Normal_2", "Staff_Normal_2", "Stone_Staff", 0, 1, 8),
        new("Normal/Mage/Staff_Normal_3", "Staff_Normal_3", "Steal_Staff", 0, 1, 9),
        new("Rare/Mage_Rare/Staff_Rare_1", "Staff_Rare_1", "Golden_Staff", 1, 1, 5),
        new("Rare/Mage_Rare/Staff_Rare_2", "Staff_Rare_2", "Golden_Steal_Staff", 1, 1, 6),
        new("Epic/Staff_Epic_1", "Staff_Epic_1", "Air", 2, 1, 3),
    };

    [MenuItem("KingdomIdle/UI/Bronze Expedition/Apply Equipment Icons")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("장비 아이콘 연결은 편집 모드에서 실행하세요.");

        // Resolve every asset and stable identity before the first assignment.
        var pairs = ResolveAll(requireAssigned: false);
        int changed = 0;
        foreach (var pair in pairs)
        {
            if (pair.Data.icon == pair.Icon) continue;
            Undo.RecordObject(pair.Data, "청동 원정대 장비 아이콘 연결");
            pair.Data.icon = pair.Icon;
            EditorUtility.SetDirty(pair.Data);
            changed++;
        }
        if (changed > 0) AssetDatabase.SaveAssets();
        ValidateOrThrow();
        Debug.Log($"[BronzeEquipmentIconPreparation] 장비 아이콘 {Entries.Length}개 확인, 참조 {changed}개 연결. 아이템 코드·수치·활 카탈로그 보존.");
    }

    [MenuItem("KingdomIdle/UI/Bronze Expedition/Validate Equipment Icons")]
    public static void ValidateOrThrow()
    {
        ResolveAll(requireAssigned: true);
        Debug.Log("[BronzeEquipmentIconPreparation] 장비 12개: 저장 키·등급·직업·ID·아이콘 참조·48px 임포트 설정 확인.");
    }

    private static List<(EquipmentData Data, Sprite Icon)> ResolveAll(bool requireAssigned)
    {
        var pairs = new List<(EquipmentData, Sprite)>(Entries.Length);
        var codes = new HashSet<int>();
        foreach (var entry in Entries)
        {
            var data = AssetDatabase.LoadAssetAtPath<EquipmentData>(entry.Asset);
            if (data == null) throw new InvalidOperationException("장비 없음: " + entry.Asset);
            using var serialized = new SerializedObject(data);
            if (data.equipmentName != entry.Key || (int)data.rarity != entry.Rarity ||
                (int)data.slot != 0 || serialized.FindProperty("_jobMask").intValue != entry.JobMask ||
                serialized.FindProperty("_itemId").intValue != entry.ItemId || !codes.Add(data.itemCode))
                throw new InvalidOperationException("장비 식별 정보가 승인된 매핑과 다릅니다: " + entry.Asset);

            string iconPath = IconRoot + entry.Name + ".png";
            var icon = AssetDatabase.LoadAssetAtPath<Sprite>(iconPath);
            var importer = AssetImporter.GetAtPath(iconPath) as TextureImporter;
            if (icon == null || importer == null) throw new InvalidOperationException("아이콘 없음: " + iconPath);
            if (icon.rect.width != 48 || icon.rect.height != 48 || icon.pixelsPerUnit != 48 ||
                importer.spriteImportMode != SpriteImportMode.Single || importer.filterMode != FilterMode.Point ||
                importer.mipmapEnabled || importer.isReadable || importer.npotScale != TextureImporterNPOTScale.None ||
                !importer.alphaIsTransparency || importer.wrapMode != TextureWrapMode.Clamp)
                throw new InvalidOperationException("48px 도트 임포트 설정 불일치: " + iconPath);
            if (requireAssigned && data.icon != icon)
                throw new InvalidOperationException("아이콘 참조 불일치: " + entry.Asset);
            pairs.Add((data, icon));
        }
        return pairs;
    }
}
