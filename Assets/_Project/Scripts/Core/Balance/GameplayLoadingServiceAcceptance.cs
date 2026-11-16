#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using KingdomIdle.Ads;
using KingdomIdle.Gacha;
using KingdomIdle.Mailbox.Testing;
using KingdomIdle.OfflineRewards;
using KingdomIdle.Rewards;
using PlayFab;
using Scripts.Core;
using Scripts.Core.Manager;
using Scripts.Core.Utils;
using UnityEngine;

namespace KingdomIdle.Balance
{
    public static class GameplayLoadingServiceAcceptance
    {
        private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic;
        private static FieldInfo Field(Type type, string name) => type.GetField(name, Fields);
        private static GameplayLoading.Scope Loading(object owner, string name) =>
            (GameplayLoading.Scope)Field(owner.GetType(), name).GetValue(owner);

        /// <summary>빈 씬에서 실행한다. 네트워크 전송은 큐에 보류하고 실제 서비스의 완료·복구 경로만 호출한다.</summary>
        public static object Run()
        {
            if (LocalProgression.IsReady || NetworkManager.Instance != null || UserManager.Instance != null ||
                EquipmentManager.Instance != null || KingdomIdle.UGUI.UIManager.Instance != null)
                throw new InvalidOperationException("빈 씬에서 계정과 게임 매니저 없이 실행해 주세요.");
            using var session = LocalProgression.BeginTestSession();
            var checks = new List<string>();
            void Check(bool valid, string message)
            { if (!valid) throw new InvalidOperationException("Gameplay loading service acceptance: " + message); checks.Add(message); }
            var owner = new GameObject("GameplayLoadingServiceAcceptance") { hideFlags = HideFlags.HideAndDontSave };
            owner.SetActive(false);
            var table = ScriptableObject.CreateInstance<GachaTableSO>();
            var shopState = new Dictionary<FieldInfo, object>();
            foreach (string name in new[] { "_server", "_serverOffset", "<Revision>k__BackingField", "_activeClaim", "_requestGeneration" })
            { var field = Field(typeof(ShopAdRewardService), name); shopState[field] = field.GetValue(null); }
            try
            {
                var mail = new MailboxAcceptance.Fixture(1) { HoldLists = true, HoldClaims = true };
                mail.Service.Open();
                Check(Loading(mail.Service, "_claimLoading") == null, "mail list loading does not acquire an operation overlay");
                mail.Lists.Dequeue()();
                Check(mail.Service.TryClaimAll().Status == RewardStatus.Pending, "mail claim is accepted");
                var mailLoading = Loading(mail.Service, "_claimLoading");
                Field(typeof(GameplayLoading.Scope), "_activeSince").SetValue(mailLoading, Time.realtimeSinceStartup);
                Check(mailLoading != null && !mailLoading.IsDisposed && !GameplayLoading.IsVisible,
                    "accepted claim owns a delayed scope without an immediate overlay");
                Field(typeof(GameplayLoading.Scope), "_activeSince").SetValue(mailLoading,
                    Time.realtimeSinceStartup - GameplayLoading.DelaySeconds - .01f);
                Check(GameplayLoading.IsVisible, "held claim becomes visible after the configured delay");
                mail.Claims.Dequeue()();
                Check(!mailLoading.IsDisposed && mail.Lists.Count == 1, "mail claim keeps its scope through required list refresh");
                mail.Lists.Dequeue()();
                Check(mailLoading.IsDisposed && !GameplayLoading.IsVisible, "confirmed mail refresh releases the overlay");
                mail.Service.Close();
                var recoveringMail = new MailboxAcceptance.Fixture(1) { CommitThenTimeout = true, HoldRecovery = true };
                recoveringMail.Service.Open();
                recoveringMail.Service.TryClaimAll();
                var recoveringMailLoading = Loading(recoveringMail.Service, "_claimLoading");
                Check(recoveringMailLoading != null && !recoveringMailLoading.IsDisposed && recoveringMail.Recoveries.Count == 1,
                    "uncertain mail claim retains ownership during state recovery");
                recoveringMail.Recoveries.Dequeue()();
                Check(recoveringMailLoading.IsDisposed, "mail state recovery and refreshed list release ownership");
                recoveringMail.Service.Close();
                var closedMail = new MailboxAcceptance.Fixture(1) { HoldClaims = true };
                closedMail.Service.Open(); closedMail.Service.TryClaimAll();
                var closedMailLoading = Loading(closedMail.Service, "_claimLoading");
                closedMail.HoldLists = true; closedMail.Claims.Dequeue()();
                closedMail.Service.Close();
                Check(closedMailLoading.IsDisposed, "closing cancels the post-claim list and releases ownership");
                closedMail.Service.Open();
                closedMail.Lists.Dequeue()(); closedMail.Lists.Dequeue()();
                Check(Loading(closedMail.Service, "_claimLoading") == null && closedMail.Service.CanClaim,
                    "reopening ignores the old list callback without reviving its claim overlay");
                closedMail.Service.Close();

                var network = owner.AddComponent<NetworkManager>();
                NetworkManager.Instance = network;
                UserManager.Instance = owner.AddComponent<UserManager>();
                typeof(NetworkManager).GetMethod("Init", Fields).Invoke(network, null);
                Field(typeof(NetworkManager), "_sessionGUID").SetValue(network, "fixture");
                Field(typeof(NetworkManager), "_context").SetValue(network, new PlayFabAuthenticationContext());
                typeof(NetworkManager).GetProperty("LoginState").SetValue(network, GameLoginState.Ready);
                var pending = (Queue<Action>)Field(typeof(NetworkManager), "_pendingCalls").GetValue(network);
                void Hold() => Field(typeof(NetworkManager), "_callInFlight").SetValue(network, true);
                void FailQuery()
                {
                    var queued = pending.Dequeue();
                    var failure = (Action<PlayFabError>)queued.Target.GetType().GetField("timeout", Fields | BindingFlags.Public).GetValue(queued.Target);
                    failure(NetworkManager.CreateError("Transport", "fixture state query failed"));
                }
                LocalProgression.OpenTestAccount("loading-service-" + Guid.NewGuid().ToString("N"));
                LocalProgression.Execute("loading-service-wallet", s => { s.Wallet[eCurrency.AncientCoin] = 100; s.EquipmentSchemaVersion = 2; return true; });
                var instanceMail = new MailboxAcceptance.Fixture(1, instanceInventory: true) { HoldClaims = true };
                instanceMail.Service.Open();
                Check(instanceMail.Service.CanClaim && instanceMail.Service.TryClaimAll().Status == RewardStatus.Pending,
                    "schema 2 mail fixture passes the real list protocol and accepts a claim");
                var instanceMailLoading = Loading(instanceMail.Service, "_claimLoading");
                instanceMail.Claims.Dequeue()();
                Check(instanceMail.Result.Ok && instanceMailLoading.IsDisposed && instanceMail.Service.CanClaim,
                    "schema 2 claim response and follow-up empty list pass the real protocol and release ownership");
                instanceMail.Service.Close();
                var timeout = NetworkManager.CreateError("Timeout", "fixture response lost");

                var equipment = owner.AddComponent<EquipmentManager>();
                EquipmentManager.Instance = equipment;
                var begin = typeof(EquipmentManager).GetMethod("Begin", Fields);
                var complete = typeof(EquipmentManager).GetMethod("Complete", Fields);
                foreach (var kind in new[] { EquipmentManager.ActionKind.Equip, EquipmentManager.ActionKind.Lock, EquipmentManager.ActionKind.Dismantle })
                {
                    var action = new EquipmentManager.ActionInfo { Kind = kind };
                    begin.Invoke(equipment, new object[] { action });
                    Check(action.Loading == null, kind + " does not acquire an overlay");
                    complete.Invoke(equipment, new object[] { action });
                }
                var enhance = new EquipmentManager.ActionInfo { Kind = EquipmentManager.ActionKind.Enhance };
                begin.Invoke(equipment, new object[] { enhance });
                Hold();
                typeof(EquipmentManager).GetMethod("RejectServerResult", Fields).Invoke(equipment, new object[] { enhance, "enhance", timeout });
                Check(enhance.Loading != null && !enhance.Loading.IsDisposed && pending.Count == 1,
                    "equipment enhancement retains ownership during uncertain-result recovery");
                FailQuery();
                Check(enhance.Loading.IsDisposed && !equipment.IsBusy, "equipment recovery failure releases the scope");

                var gacha = owner.AddComponent<GachaManager>();
                table.gachaType = eGachaType.Skill; table.isImplemented = true; table.costCurrency = eCurrency.AncientCoin;
                Hold();
                gacha.TryPull(table, 1, _ => { }, _ => { });
                var gachaLoading = Loading(gacha, "_loading");
                Check(gachaLoading != null && !gachaLoading.IsDisposed && pending.Count == 1, "mage gacha acquires ownership only after acceptance");
                pending.Clear();
                typeof(GachaManager).GetMethod("RejectServerResult", Fields).Invoke(gacha, new object[] { table, 1, timeout, (Action<string>)(_ => { }) });
                Check(!gachaLoading.IsDisposed && pending.Count == 1, "gacha recovery retains the same scope");
                FailQuery();
                Check(gachaLoading.IsDisposed && !gacha.IsPulling, "gacha recovery failure releases the scope");

                var offline = owner.AddComponent<OfflineRewardManager>();
                Hold();
                bool offlineCompleted = false;
                Action finishOffline = () => offlineCompleted = true;
                offline.TryClaim(eStage.Stage1_1, finishOffline);
                var offlineLoading = Loading(offline, "_claimLoading");
                Check(offlineLoading != null && !offlineLoading.IsDisposed && pending.Count == 1, "initial offline settlement acquires a scope");
                pending.Clear();
                typeof(OfflineRewardManager).GetMethod("RejectServerResult", Fields).Invoke(offline, new object[] { 0m, timeout, finishOffline });
                Check(!offlineLoading.IsDisposed && pending.Count == 1 && !offlineCompleted, "offline settlement keeps its scope through state recovery");
                FailQuery();
                Check(offlineLoading.IsDisposed && offlineCompleted, "offline recovery completion releases ownership");
                typeof(OfflineRewardManager).GetProperty("AdBonusBusy").SetValue(null, true);
                var adLoading = (GameplayLoading.Scope)Field(typeof(OfflineRewardManager), "_adBonusLoading").GetValue(null);
                typeof(OfflineRewardManager).GetProperty("AdBonusBusy").SetValue(null, false);
                Check(adLoading != null && !adLoading.IsDisposed, "offline ad ownership survives the busy flag until the payout completion");
                typeof(OfflineRewardManager).GetMethod("CompleteAdBonus", Fields).Invoke(null, new object[] { null, RewardStatus.SaveFailed, null });
                Check(adLoading.IsDisposed, "offline ad payout completion releases ownership");
                typeof(OfflineRewardManager).GetProperty("AdBonusBusy").SetValue(null, true);
                var oldAdResult = new OfflineRewardClaimResult("loading-old-ad", new OfflineRewardPlan(), 1, 1, 0, 1, 0, 0);
                var oldAdLoading = (GameplayLoading.Scope)Field(typeof(OfflineRewardManager), "_adBonusLoading").GetValue(null);
                network.BeginAuthentication();
                Check(!OfflineRewardManager.AdBonusBusy, "reauthentication invalidates the legacy ad busy flag before account data changes");
                Field(typeof(NetworkManager), "_sessionGUID").SetValue(network, "fixture");
                Field(typeof(NetworkManager), "_context").SetValue(network, new PlayFabAuthenticationContext());
                typeof(NetworkManager).GetProperty("LoginState").SetValue(network, GameLoginState.Ready);
                typeof(OfflineRewardManager).GetProperty("AdBonusBusy").SetValue(null, true);
                var destroyedAdLoading = (GameplayLoading.Scope)Field(typeof(OfflineRewardManager), "_adBonusLoading").GetValue(null);
                typeof(OfflineRewardManager).GetMethod("ApplyAdBonus", Fields).Invoke(null, new object[] { oldAdResult, null });
                typeof(OfflineRewardManager).GetMethod("RejectAdBonus", Fields).Invoke(null, new object[] { oldAdResult, timeout });
                Check(oldAdLoading.IsDisposed && !destroyedAdLoading.IsDisposed && OfflineRewardManager.AdBonusBusy,
                    "old ad success and failure cannot close the new account payout scope");
                typeof(OfflineRewardManager).GetMethod("OnDestroy", Fields).Invoke(offline, null);
                Check(destroyedAdLoading.IsDisposed && !OfflineRewardManager.AdBonusBusy,
                    "destroying the owner clears the ad UI flag even without a queued settlement bonus");

                ShopAdRewardService.Receive(new Protocol.Message.Dto.AdRewardStateDTO { Day = ShopAdRewardService.Today }, 0, "fixture");
                Check(ShopAdRewardService.Begin("rewarded.daily_equipment", out var claim) == RewardStatus.Success && claim.Loading == null,
                    "preparing a shop ad does not show a payout overlay");
                Check(ShopAdRewardService.FinishAd(claim, RewardedAdResult.Skipped).Status == RewardStatus.NothingAvailable && claim.Loading == null,
                    "cancelled ad does not acquire payout ownership");
                Hold();
                ShopAdRewardService.FinishAd(claim, RewardedAdResult.Completed);
                var shopLoading = claim.Loading;
                Check(shopLoading != null && !shopLoading.IsDisposed && pending.Count == 1, "completed shop ad acquires payout ownership");
                pending.Clear();
                typeof(ShopAdRewardService).GetMethod("RejectServerResult", Fields).Invoke(null, new object[] { claim, timeout });
                Check(!shopLoading.IsDisposed && pending.Count == 1, "shop ad retains payout ownership through state recovery");
                FailQuery();
                Check(shopLoading.IsDisposed && !ShopAdRewardService.IsBusy, "shop ad recovery failure releases ownership");
                return new { passed = true, count = checks.Count, checks };
            }
            finally
            {
                GameplayLoading.Reset();
                NetworkManager.Instance = null; UserManager.Instance = null; EquipmentManager.Instance = null;
                foreach (var saved in shopState) saved.Key.SetValue(null, saved.Value);
                UnityEngine.Object.DestroyImmediate(owner);
                UnityEngine.Object.DestroyImmediate(table);
            }
        }
    }
}
#endif
