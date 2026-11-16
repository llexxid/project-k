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
#if UNITY_EDITOR
        /// <summary>실제 복구 조회를 전송 전 큐에 가둬 입력 잠금·오류 완료를 검사한다. SDK Send는 실행하지 않는다.</summary>
        public static object RunFailureRecovery()
        {
            using var scope = LocalProgression.BeginTestSession();
            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var previousNetwork = Scripts.Core.Manager.NetworkManager.Instance;
            var previousUsers = Scripts.Core.UserManager.Instance;
            var owner = new GameObject("GachaFailureAcceptance") { hideFlags = HideFlags.HideAndDontSave };
            owner.SetActive(false);
            var table = ScriptableObject.CreateInstance<GachaTableSO>();
            var checks = new List<string>();
            void Check(bool valid, string label)
            { if (!valid) throw new InvalidOperationException("Gacha failure acceptance: " + label); checks.Add(label); }
            try
            {
                var network = owner.AddComponent<Scripts.Core.Manager.NetworkManager>();
                var manager = owner.AddComponent<GachaManager>();
                Scripts.Core.Manager.NetworkManager.Instance = network;
                Scripts.Core.UserManager.Instance = owner.AddComponent<Scripts.Core.UserManager>();
                typeof(Scripts.Core.Manager.NetworkManager).GetMethod("Init", flags).Invoke(network, null);
                typeof(Scripts.Core.Manager.NetworkManager).GetField("_sessionGUID", flags).SetValue(network, "fixture");
                typeof(Scripts.Core.Manager.NetworkManager).GetField("_context", flags).SetValue(network, new PlayFab.PlayFabAuthenticationContext());
                typeof(Scripts.Core.Manager.NetworkManager).GetProperty("LoginState").SetValue(network, Scripts.Core.Manager.GameLoginState.Ready);
                LocalProgression.OpenTestAccount("gacha-failure-" + Guid.NewGuid().ToString("N"));
                table.gachaType = eGachaType.Equipment;
                var pending = (Queue<Action>)typeof(Scripts.Core.Manager.NetworkManager).GetField("_pendingCalls", flags).GetValue(network);
                var inFlight = typeof(Scripts.Core.Manager.NetworkManager).GetField("_callInFlight", flags);
                var reject = typeof(GachaManager).GetMethod("RejectServerResult", flags);
                int generation = 0;
                void Arm()
                {
                    typeof(GachaManager).GetField("_pullNetwork", flags).SetValue(manager, network);
                    typeof(GachaManager).GetField("_pullNetworkGeneration", flags).SetValue(manager, network.AccountGeneration);
                    typeof(GachaManager).GetField("_pullAccountGeneration", flags).SetValue(manager, LocalProgression.AccountGeneration);
                    typeof(GachaManager).GetField("_pullGeneration", flags).SetValue(manager, ++generation);
                    typeof(GachaManager).GetProperty("IsPulling").SetValue(manager, true);
                    inFlight.SetValue(network, true);
                }
                Action<PlayFab.PlayFabError> TakeFailure()
                {
                    var queued = pending.Dequeue();
                    return (Action<PlayFab.PlayFabError>)queued.Target.GetType().GetField("timeout",
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic).GetValue(queued.Target);
                }
                string message = null;
                Action<string> complete = text => message = text;
                var errors = new[]
                {
                    new PlayFab.PlayFabError { Error = PlayFab.PlayFabErrorCode.CloudScriptAzureFunctionsExecutionTimeLimitExceeded },
                    new PlayFab.PlayFabError { Error = PlayFab.PlayFabErrorCode.CloudScriptExecutionTimeLimitExceeded },
                    new PlayFab.PlayFabError { Error = PlayFab.PlayFabErrorCode.ConnectionError },
                    Scripts.Core.Manager.NetworkManager.CreateError("Timeout", "fixture"),
                    Scripts.Core.Manager.NetworkManager.CreateError("Transport", "fixture"),
                    Scripts.Core.Manager.NetworkManager.CreateError("Function", "fixture"),
                    Scripts.Core.Manager.NetworkManager.CreateError("InvalidResponse", "fixture"),
                    Scripts.Core.Manager.NetworkManager.CreateError("ApplyFailed", "fixture")
                };
                foreach (var error in errors)
                {
                    Arm(); message = null;
                    reject.Invoke(manager, new object[] { table, 1, error, complete });
                    Check(manager.IsPulling && pending.Count == 1 && message == null,
                        "uncertain failure holds input while one state query waits before Send: " + error.Error + "/" + Scripts.Core.Manager.NetworkManager.GetFailureKind(error));
                    TakeFailure()(Scripts.Core.Manager.NetworkManager.CreateError("Transport", "fixture refresh failed"));
                    Check(!manager.IsPulling && pending.Count == 0 && message.Contains("확인하지 못"),
                        "failed state query unlocks with unconfirmed-result guidance");
                }
                Arm(); message = null;
                reject.Invoke(manager, new object[] { table, 1, errors[0], complete });
                var oldFailure = TakeFailure();
                network.BeginAuthentication();
                Check(!manager.IsPulling, "reauthentication invalidates the old account input lock");
                typeof(Scripts.Core.Manager.NetworkManager).GetProperty("LoginState").SetValue(network, Scripts.Core.Manager.GameLoginState.Ready);
                Arm(); oldFailure(Scripts.Core.Manager.NetworkManager.CreateError("Transport", "late old refresh"));
                Check(manager.IsPulling && message == null && pending.Count == 0,
                    "late old-account recovery cannot unlock the new pull or publish its message");
                Check((bool)inFlight.GetValue(network), "every state query stayed behind the fixture barrier without SDK Send");
                return new { passed = true, count = checks.Count, checks };
            }
            finally
            {
                Scripts.Core.Manager.NetworkManager.Instance = previousNetwork;
                Scripts.Core.UserManager.Instance = previousUsers;
                UnityEngine.Object.DestroyImmediate(owner);
                UnityEngine.Object.DestroyImmediate(table);
            }
        }
#endif

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
