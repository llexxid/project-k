using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class EquipmentTestGrantUtility
{
    public const string TestEquipmentNamePrefix = "TEST_";

    public static int GrantAllTestWeaponsToAllPlayers(IEnumerable<EquipmentData> testEquipments, bool allowDuplicates = false)
    {
        if (testEquipments == null) return 0;

        List<EquipmentData> weapons = testEquipments
            .Where(data => data != null
                           && data.slot == eEquipmentSlot.Weapon
                           && !string.IsNullOrEmpty(data.equipmentName)
                           && data.equipmentName.StartsWith(TestEquipmentNamePrefix))
            .ToList();

        if (weapons.Count == 0) return 0;

        Player[] players = Object.FindObjectsByType<Player>(FindObjectsSortMode.None);
        int grantedCount = 0;

        foreach (var weapon in weapons)
        {
            if (!allowDuplicates && HasEquipment(weapon)) continue;

            EquipmentManager.Instance.Inventory.Add(new EquipmentInstance(weapon));
            grantedCount++;
        }

        Debug.Log($"[EquipmentTestGrantUtility] Granted {grantedCount} test equipment instances to {players.Length} players.");
        return grantedCount;
    }

    public static int GrantAllTestWeaponsFromDatabase(EquipmentDatabase database, bool allowDuplicates = false)
    {
        return database == null
            ? 0
            : GrantAllTestWeaponsToAllPlayers(database.equipmentList, allowDuplicates);
    }

    private static bool HasEquipment(EquipmentData data)
    {
        return EquipmentManager.Instance.Inventory.Items.Any(instance => instance?.baseData == data);
    }
}
