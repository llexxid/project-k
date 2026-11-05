#if UNITY_EDITOR || LOBBY_DEVICE_QA
using System;
using System.Collections.Generic;
using KingdomIdle.Gacha;
using KingdomIdle.UGUI;
using Newtonsoft.Json;
using UnityEngine;

namespace KingdomIdle.Balance
{
    public static class GachaTicketAcceptance
    {
        public static object Run()
        {
            var checks = new List<string>();
            void Check(bool valid, string name)
            { if (!valid) throw new InvalidOperationException("Gacha ticket acceptance: " + name); checks.Add(name); }
            using var scope = LocalProgression.BeginTestSession();
            var previousEquipment = EquipmentManager.Instance;
            var equipmentObject = new GameObject("GachaTicketAcceptance_Equipment");
            var gachaObject = new GameObject("GachaTicketAcceptance_Gacha");
            equipmentObject.SetActive(false);
            gachaObject.SetActive(false);
            var equipmentTable = ScriptableObject.CreateInstance<GachaTableSO>();
            var skillTable = ScriptableObject.CreateInstance<GachaTableSO>();
            try
            {
                EquipmentManager.Instance = equipmentObject.AddComponent<EquipmentManager>();
                var manager = gachaObject.AddComponent<GachaManager>();
                equipmentTable.gachaType = eGachaType.Equipment;
                skillTable.gachaType = eGachaType.Skill;
                foreach (var table in new[] { equipmentTable, skillTable })
                {
                    table.isImplemented = true;
                    table.costCurrency = eCurrency.AncientCoin;
                    table.costAmount = 50;
                }
                LocalProgression.OpenTestAccount("gacha-ticket-" + Guid.NewGuid().ToString("N"));
                void Wallet(long coins, long equipment, long mage)
                {
                    if (!LocalProgression.Execute("gacha-ticket-fixture", s =>
                    {
                        s.Wallet[eCurrency.AncientCoin] = coins;
                        s.Wallet[eCurrency.EquipmentTicket] = equipment;
                        s.Wallet[eCurrency.MageTicket] = mage;
                        return true;
                    })) throw new InvalidOperationException(LocalProgression.LastError);
                }
                foreach (var table in new[] { equipmentTable, skillTable })
                {
                    string kind = table.gachaType.ToString();
                    void Quote(int count, long coins, long ownTickets, long otherTickets, int ticketCost, int coinCost, bool allowed)
                    {
                        Wallet(coins, table == equipmentTable ? ownTickets : otherTickets, table == skillTable ? ownTickets : otherTickets);
                        string before = JsonConvert.SerializeObject(LocalProgression.State);
                        Check(manager.GetTicketCost(table, count) == ticketCost && manager.GetTotalCost(table, count) == coinCost &&
                            manager.CanPullMulti(table, count) == allowed && (count != 1 || manager.CanPull(table) == allowed),
                            $"{kind} x{count}: {ownTickets} tickets, {coins} coins => {ticketCost} tickets + {coinCost} coins, allowed={allowed}");
                        if (!allowed)
                            Check(manager.PullFailure(table, count) == $"고대주화가 {coinCost - coins:N0}개 부족합니다.", kind + " exact shortfall");
                        Check(JsonConvert.SerializeObject(LocalProgression.State) == before, kind + " quoting never consumes currency");
                    }
                    Quote(1, 50, 0, 0, 0, 50, true);
                    Quote(10, 500, 0, 0, 0, 500, true);
                    Quote(1, 0, 1, 0, 1, 0, true);
                    Quote(10, 350, 3, 0, 3, 350, true);
                    Check(RewardPresentation.GachaCost(manager, table, 10) == "뽑기권 3장 + 350 주화", kind + " mixed cost label");
                    Quote(10, 349, 3, 0, 3, 350, false);
                    Quote(10, 0, 10, 0, 10, 0, true);
                    Check(RewardPresentation.GachaCost(manager, table, 10) == "뽑기권 10장", kind + " tickets cover the full cost");
                    Quote(10, 0, long.MaxValue, 0, 10, 0, true);
                    Quote(1, 49, 0, 10, 0, 50, false);
                    Quote(10, 499, 0, 10, 0, 500, false);
                    Quote(10, 500, 0, 10, 0, 500, true);
                    Check(RewardPresentation.GachaCost(manager, table, 10) == "500 고대주화", kind + " other tickets cannot replace coins");
                    Check(!manager.CanPullMulti(table, 0) && !manager.CanPullMulti(table, 2) && manager.GetTotalCost(table, 2) == -1,
                        kind + " unsupported counts remain rejected");
                }
                Check(!manager.CanPullMulti(null, 1) && manager.GetTotalCost(null, 1) == -1, "missing table rejected");
                return new { passed = true, count = checks.Count, checks };
            }
            finally
            {
                EquipmentManager.Instance = previousEquipment;
                UnityEngine.Object.DestroyImmediate(gachaObject);
                UnityEngine.Object.DestroyImmediate(equipmentObject);
                UnityEngine.Object.DestroyImmediate(equipmentTable);
                UnityEngine.Object.DestroyImmediate(skillTable);
            }
        }
    }
}
#endif
