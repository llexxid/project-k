#if UNITY_EDITOR || LOBBY_DEVICE_QA
using System;
using System.Collections.Generic;
using System.Linq;
using KingdomIdle.Gacha;
using Reincarnation;
using UnityEngine;

namespace KingdomIdle.Balance
{
    public static class BalanceFixAcceptance
    {
        public static Dictionary<string, object> Run(EquipmentManager equipment, GachaManager gacha, GachaTableSO table)
        {
            using var session = LocalProgression.BeginTestSession();
            var random = UnityEngine.Random.state;
            try
            {
                UnityEngine.Random.InitState(220926);
                var checks = new List<string>();
                void Check(bool condition, string message)
                { if (!condition) throw new InvalidOperationException(message); checks.Add(message); }
                bool Write(Action<ProgressionState> action) => LocalProgression.Execute("balance-fix-check", s => { action(s); return true; });
                var catalog = Enumerable.Range(0, 3).SelectMany(i => equipment.GetByRarity((eEquipmentRarity)i)).ToArray();
                var bows = catalog.Where(x => !x.CanBeRewarded).ToArray();
                Check(catalog.Length == 18 && bows.Length == 6 && bows.All(x => equipment.GetData(x.itemCode) == x),
                    "All 18 legacy weapons still resolve, including six bows");
                var sizes = new[] { 6, 4, 2 };
                var rates = new[] { 70f, 20f, 5f };
                for (int tier = 0; tier < 3; tier++)
                {
                    var pool = equipment.GetRewardPool((eEquipmentRarity)tier);
                    var preview = table.rewards.Where(x => x.rewardType == eGachaRewardType.Equipment && (int)x.equipmentData.rarity == tier).ToArray();
                    Check(pool.Count == sizes[tier] && pool.All(x => x.CanBeRewarded), $"Tier {tier} contains only {sizes[tier]} supported weapons");
                    Check(preview.Select(x => x.equipmentData.itemCode).OrderBy(x => x).SequenceEqual(pool.Select(x => x.itemCode).OrderBy(x => x)) &&
                        Math.Abs(preview.Sum(x => x.weight) - rates[tier]) < .001f && preview.All(x => Math.Abs(x.weight - rates[tier] / sizes[tier]) < .001f),
                        $"Tier {tier} preview matches the real pool and original probability");
                }
                Check(Math.Abs(table.rewards.Sum(x => x.weight) - 100f) < .001f, "Preview totals 100 percent");
                var dropped = new HashSet<int>();
                for (int i = 0; i < 10000; i++)
                {
                    var item = equipment.RollFieldDrop(1);
                    if (item == null || !equipment.GetData(item.Code).CanBeRewarded) throw new InvalidOperationException("Invalid field reward");
                    dropped.Add(item.Code);
                }
                Check(dropped.Count == 12 && equipment.RollFieldDrop(0) == null, "10000 field rolls cover all 12 weapons and zero bows; zero drop rate grants nothing");

                string account = "balance-fix-" + Guid.NewGuid().ToString("N");
                LocalProgression.OpenTestAccount(account);
                Check(Write(s => {
                    s.Wallet[eCurrency.AncientCoin] = 50100;
                    foreach (var bow in bows) EquipmentManager.Grant(s, new EquipmentSave { Id = "old-bow-" + bow.itemCode, Code = bow.itemCode, Level = 2, Locked = true }, true);
                }), "Seed existing enhanced and locked bows");
                int draws = 0;
                for (int i = 0; i < 100; i++)
                {
                    string error = null;
                    gacha.TryPull(table, 10, rewards => {
                        if (rewards.Count != 10 || rewards.Any(x => x.rewardType == eGachaRewardType.Equipment && !x.equipmentData.CanBeRewarded))
                            throw new InvalidOperationException("Invalid gacha reward");
                        draws += rewards.Count;
                    }, message => error = message);
                    if (error != null) throw new InvalidOperationException(error);
                }
                Check(draws == 1000 && LocalProgression.Balance(eCurrency.AncientCoin) == 100,
                    "1000 real gacha draws exclude bows, charge exactly and continue through inventory overflow");
                Check(LocalProgression.State.Equipment.Count(x => x.Id.StartsWith("old-bow-") && x.Level == 2 && x.Locked) == 6,
                    "Owned bows retain quantity, enhancement and locks");
                Check(Write(s => s.EquipmentPity = 39), "Prepare 40th draw");
                bool guaranteed = false;
                gacha.TryPull(table, 1, r => guaranteed = r.Single().equipmentData?.rarity == eEquipmentRarity.Epic && r.Single().equipmentData.CanBeRewarded,
                    error => throw new InvalidOperationException(error));
                Check(guaranteed && LocalProgression.State.EquipmentPity == 0, "40th draw still guarantees a supported epic");
                long revision = LocalProgression.State.Revision;
                bool failed = false, succeeded = false;
                LocalProgression.TestFailedCommit(() => { gacha.TryPull(table, 1, _ => succeeded = true, _ => failed = true); return succeeded; });
                Check(failed && !succeeded && LocalProgression.Balance(eCurrency.AncientCoin) == 50 && LocalProgression.State.Revision == revision,
                    "Failed gacha save preserves wallet, pity and inventory");

                string permanent(ProgressionState s) => Newtonsoft.Json.JsonConvert.SerializeObject(new {
                    s.AccountLevel, s.Experience, s.Jobs, s.UnlockedJobs, s.Equipment, s.PendingEquipment, s.LegacyEquipment,
                    s.MageSkills, s.MageSlots, s.RubyGoldLevel, s.RubyExpLevel, s.RubyGoldSpent, s.RubyExpSpent, s.MainClears,
                    s.GoldDungeonClear, s.RubyDungeonClear, s.EquipmentPity,
                    Wallet = s.Wallet.Where(x => x.Key != eCurrency.Gold).ToArray()
                });
                Check(Write(s => {
                    s.AccountLevel = 20; s.Experience = 10; s.Jobs[0] = "Knight";
                    s.UnlockedJobs[0] = new HashSet<string> { "Spearman", "Knight" };
                    s.RubyGoldLevel = 3; s.RubyExpLevel = 2; s.RubyGoldSpent = 65; s.RubyExpSpent = 42;
                    s.MageSkills[0] = new MageSave { Enhance = 3, Awaken = 10, BloomEnabled = true, Fragments = 12, Spent = 32 };
                    s.MageSlots[0] = 0; s.MainClears.Add(0x20003000B); s.GoldDungeonClear = s.RubyDungeonClear = 2;
                    s.Wallet[eCurrency.Ruby] = 123; s.Wallet[eCurrency.EquipmentStone] = 456;
                }), "Prepare permanent progression");
                for (int cycle = 1; cycle <= 3; cycle++)
                {
                    Check(Write(s => {
                        s.AttackLevel = 30 + cycle; s.HealthLevel = 40 + cycle; s.Wallet[eCurrency.Gold] = 100000;
                        s.GoldRemainder = 999999; s.CycleBossStage = cycle + 2;
                        s.CycleStartedUtc = s.LastReincarnationUtc = LocalProgression.UtcNow - 601;
                        s.PendingReincarnation = true;
                    }), $"Prepare cycle {cycle}");
                    string before = permanent(LocalProgression.State);
                    long oldRevision = LocalProgression.State.Revision;
                    Check(!LocalProgression.TestFailedWrite(s => { ReincarnationService.CommitAtBoundary(s, "failed"); return true; }) &&
                        LocalProgression.State.Revision == oldRevision && LocalProgression.State.AttackLevel == 30 + cycle &&
                        LocalProgression.Balance(eCurrency.Gold) == 100000 && LocalProgression.State.PendingReincarnation,
                        $"Cycle {cycle}: failed save cannot reset growth or consume pending reincarnation");
                    int oldLevel = LocalProgression.State.ReincarnationLevel;
                    Check(Write(s => ReincarnationService.CommitAtBoundary(s, "cycle-" + cycle)), $"Commit cycle {cycle}");
                    var state = LocalProgression.State;
                    Check(state.AttackLevel == 0 && state.HealthLevel == 0 && state.Wallet[eCurrency.Gold] == 0 && state.GoldRemainder == 0 &&
                        state.MainStage == 0x200010001 && state.ReincarnationLevel - oldLevel == 5 * (cycle + 2) && !state.PendingReincarnation &&
                        permanent(state) == before, $"Cycle {cycle}: reset only temporary gold growth; preserve permanent ownership and grant increasing reward");
                    var json = Newtonsoft.Json.JsonConvert.SerializeObject(state);
                    ReincarnationService.CommitAtBoundary(state, "cycle-" + cycle);
                    Check(Newtonsoft.Json.JsonConvert.SerializeObject(state) == json, $"Cycle {cycle}: duplicate boundary does not pay or reset twice");
                    LocalProgression.OpenTestAccount(account);
                    Check(LocalProgression.State.AttackLevel == 0 && LocalProgression.State.HealthLevel == 0 && permanent(LocalProgression.State) == before,
                        $"Cycle {cycle}: reset and preserved growth survive reopening");
                }
                Check(LocalProgression.State.ReincarnationLevel == 60 && ReincarnationService.Eligibility(LocalProgression.State) == eReincarnationFailureReason.DailyLimit,
                    "Three cycles grant 15, 20, 25 levels and retain the daily limit");
                return new Dictionary<string, object> { ["passed"] = checks.Count, ["checks"] = checks };
            }
            finally { UnityEngine.Random.state = random; }
        }
    }
}
#endif
