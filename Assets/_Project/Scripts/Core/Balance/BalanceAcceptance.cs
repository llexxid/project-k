#if UNITY_EDITOR || LOBBY_DEVICE_QA
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using KingdomIdle.OfflineRewards;
using KingdomIdle.UGUI;
namespace KingdomIdle.Balance
{
    public static class BalanceAcceptance
    {
        public static Dictionary<string,object> Run()
        {
            var checks=new List<string>();
            void Check(bool result,string label) { if(!result) throw new InvalidOperationException("BALANCE ASSERT: "+label);checks.Add(label); }
            var dailyReincarnation = new ProgressionState { ReincarnationDay = LocalProgression.KstDay, ReincarnationsToday = 3, CycleStartedUtc = LocalProgression.UtcNow };
            Check(Reincarnation.ReincarnationService.Eligibility(dailyReincarnation) == Reincarnation.eReincarnationFailureReason.DailyLimit,
                "Daily reincarnation cap is explained before another boss or cooldown requirement");
            var legacyInventory = new ProgressionState { QuestSchemaVersion = QuestEconomy.SchemaVersion };
            EquipmentManager.ImportLegacy(legacyInventory, 123, 7, 65535);
            Check(legacyInventory.Equipment.Count == EquipmentManager.Capacity && legacyInventory.PendingEquipment.Count == 0 &&
                legacyInventory.LegacyEquipment.Single().Count == 65535 - EquipmentManager.Capacity, "Legacy 65535 stack preserves overflow without filling battle inbox");
            LocalProgression.Validate(legacyInventory);
            Check(!EquipmentManager.TakeLegacy(legacyInventory,123,7), "Full inventory cannot consume legacy reserve");
            var legacyCopy = legacyInventory.DeepClone(); legacyCopy.LegacyEquipment[0].Count--;
            Check(legacyCopy.LegacyEquipment[0].Count + 1 == legacyInventory.LegacyEquipment[0].Count, "Legacy reserve is isolated in transaction drafts");
            var legacyReload = JsonConvert.DeserializeObject<ProgressionState>(JsonConvert.SerializeObject(legacyInventory));
            legacyReload.Equipment.RemoveAt(0);
            Check(EquipmentManager.TakeLegacy(legacyReload,123,7) && legacyReload.Equipment.Last().Level == 7 &&
                legacyReload.Equipment.Count + legacyReload.LegacyEquipment.Sum(x=>x.Count) == 65534, "Legacy withdrawal after reload preserves quantity and enhancement");
            legacyReload.LegacyEquipment[0].Count = 1; legacyReload.Equipment.RemoveAt(0);
            Check(EquipmentManager.TakeLegacy(legacyReload,123,7) && legacyReload.LegacyEquipment.Count == 0 &&
                !EquipmentManager.TakeLegacy(legacyReload,123,7), "Final legacy item is consumed once");
            LocalProgression.Validate(legacyReload);
            Check(JsonConvert.DeserializeObject<ProgressionState>("{}").LegacyEquipment.Count == 0, "Old save defaults to empty legacy reserve");
            var crowded = new ProgressionState { QuestSchemaVersion = QuestEconomy.SchemaVersion };
            bool allGranted = true;
            for (int i = 0; i < 2400; i++)
                allGranted &= EquipmentManager.Grant(crowded, new EquipmentSave { Id = "crowded-" + i, Code = 123 + i % 3 }, true);
            Check(allGranted, "Every reward in a 2400-item session is preserved");
            Check(crowded.Equipment.Count == 300 && crowded.PendingEquipment.Count == 100 && crowded.LegacyEquipment.Count == 3 &&
                crowded.Equipment.Count + crowded.PendingEquipment.Count + crowded.LegacyEquipment.Sum(x => x.Count) == 2400,
                "Long-session drops remain bounded and preserve every reward without blocking battles");
            LocalProgression.Validate(JsonConvert.DeserializeObject<ProgressionState>(JsonConvert.SerializeObject(crowded)));
            Check(BalanceMath.GoldTotal(0,10)==696,"Gold levels 0→10 cost 696");
            Check(BalanceMath.GoldTotal(0,100)==619133,"Gold levels 0→100 cost 619133");
            Check(BalanceMath.GoldTotal(0,300)==466665042390L,"Gold levels 0→300 remain 64-bit");
            Check(BalanceMath.GoldCost(299)==30529488792L && BalanceMath.GoldCost(300)==null,"Gold cap distinct from zero cost");
            Check(BalanceMath.AffordableGoldLevels(0,695)==9 && BalanceMath.AffordableGoldLevels(0,696)==10,"Max purchase exact boundary");
            Check(BalanceMath.Stat(30,40,0,10,.10m,10,5)==105,"Shared final-stat example = 105");
            var stats = new PlayerStatus();
            stats.SetEquipmentBonus(40,2000000); stats.AddPassiveSelfBonus(5,10);
            stats.SetAura(.10m,.20m); stats.SetProgression(10,300,10,5);
            Check(stats.Atk == 112 && stats.AtkBreakdown().Final == stats.Atk,
                "Displayed attack combines equipment, passive, additive rates and gold growth");
            Check(stats.MaxHP > int.MaxValue && stats.MaxHP == (long)decimal.Round(2000210m * 1.268m * BalanceMath.GoldMultiplier(300), 0, MidpointRounding.AwayFromZero),
                "Large health and displayed breakdown preserve 64-bit values");
            int statChanges = 0; stats.OnStatsChanged += () => statChanges++;
            stats.SetEquipmentBonus(40,2000000); stats.SetProgression(10,300,10,5);
            Check(statChanges == 0, "Unchanged equipment and progression do not rebuild stat UI");
            Check(BalanceMath.Damage(105,.5m)==53 && BalanceMath.Damage(0,0)==1,"Damage half-up and minimum one");
            bool overflow=false;try{BalanceMath.Damage(long.MaxValue,2m);}catch(OverflowException){overflow=true;}Check(overflow,"Damage overflow rejected");
            Check(BalanceMath.WeaponAttack(15,1)==16 && BalanceMath.WeaponAttack(80,15)==200,"Weapon linear 10% base growth floor");
            Check(Enumerable.Range(0,100).Sum(i=>BalanceMath.MageCost(i).Value)==12427,"Mage E100 costs 12427 knowledge");
            Check(BalanceMath.MageDamage(120,10,4)==213,"Mage exponential enhance and additive awakening");
            Check(BalanceMath.MageInterval(10,10)==8 && BalanceMath.MageHits(3,8,false)==5 && BalanceMath.MageHits(10,8,true)==14,"Mage awakening cooldown and hit thresholds");
            Check(Enumerable.Range(1,199).Sum(i=>BalanceMath.NextExp(i).Value)==6129211,"Account level 200 total EXP");
            int level=1;long exp=0;BalanceMath.GainExperience(ref level,ref exp,204);Check(level==3 && exp==0,"EXP equality and multiple levels");
            BalanceMath.GainExperience(ref level,ref exp,6129211);Check(level==200 && exp==0,"Account cap discards excess EXP");
            long remainder=0,sum=0;for(int i=0;i<50;i++)sum+=BalanceMath.WithRemainder(1,1,ref remainder);Check(sum==51 && remainder==0,"Fractional income preserved across 50 payouts");
            var expected=new[]{(3735L,29L,215L,118L),(10657L,57L,503L,248L),(30404L,113L,1173L,522L)};
            for(int i=1;i<=3;i++){var e=BalanceMath.MainEnemy(i,11);Check((e.HP,e.Attack,e.Gold,e.Experience)==expected[i-1],$"Main boss {i} exact rewards/stats");}
            var normal=BalanceMath.MainEnemy(3,10);Check(normal.HP==1536 && normal.Attack==36 && normal.Gold==108 && normal.Experience==24,"Main 3-10 exact stats");
            Check(BalanceMath.RubyBoss(2,1).HP==3960,"Decimal ceil does not introduce a phantom hit point");
            Check(Enumerable.Range(1,5).Select(BalanceMath.RubyClear).SequenceEqual(new long[]{50,67,91,123,166}),"Ruby dungeon reward curve");
            Check(BalanceMath.Mimic(3).Gold*13==14976 && BalanceMath.Mimic(3).Gold*20==23040,"Gold dungeon partial and full rewards");
            var offline=OfflineRewardCalculator.CreatePlan(TimeSpan.FromHours(10),0x20003000A,30);
            Check(offline.estimatedKillCount==8640 && offline.appliedOfflineSeconds==28800,"Offline 8 hours, 60%, 8640 maximum");
            Check(OfflineRewardCalculator.CreatePlan(TimeSpan.FromHours(8),0x20003000B,30).estimatedKillCount==0,"Offline excludes boss");
            Check(OfflineRewardCalculator.CreatePlan(TimeSpan.FromHours(8),0x210010001,30).estimatedKillCount==0,"Offline excludes dungeon");
            Check(OfflineRewardCalculator.CreatePlan(TimeSpan.FromHours(8),0x200010001,15).estimatedKillCount==4320,"Early sample KPM cap");
            var distribution=new int[4];for(int i=0;i<1000000;i++)distribution[BalanceMath.EquipmentRoll(i,0)+1]++;
            Check(distribution.SequenceEqual(new[]{50000,700000,200000,50000}),"Exhaustive equipment probability partition 5/70/20/5");
            Check(BalanceMath.EquipmentRoll(999999,39)==2 && BalanceMath.EquipmentRoll(0,39)==2,"40th equipment draw replaces every outcome");
            Check(CombatPowerCalculator.CalculateCharacterPowerV1(30,200)*3==600,"Three starting spearmen CP = 600 at 1.2 multiplier / 1.2s interval");
            var defs=QuestEconomy.Definitions;
            Check(defs.Count(x=>x.Category==eQuestCategory.Guide)==28 && defs.Count(x=>x.Category==eQuestCategory.Achievement)==49,"Catalog 28 guide +49 active achievements");
            Check(defs.Count(x=>x.Category==eQuestCategory.Daily)==8 && defs.Count(x=>x.Category==eQuestCategory.Weekly)==7,"Catalog 8 daily +7 weekly");
            Check(!defs.Any(x=>x.QuestId==10026 || x.QuestId==10027) && defs.First(x=>x.QuestId==10025).NextQuestId==10028,"Removed divine objectives do not block guide");
            var quest=new ProgressionState();QuestEconomy.Before(quest);quest.MainClears.Add(0x20001000B);
            foreach(var pair in new[]{(eQuestObjectiveType.MonsterKill,200L),(eQuestObjectiveType.BossKill,1L),(eQuestObjectiveType.BattleTime,420L),(eQuestObjectiveType.SkillCast,5L),(eQuestObjectiveType.DungeonEnter,1L)})QuestEconomy.Count(quest,pair.Item1,0,pair.Item2);
            QuestEconomy.After(quest);Check(QuestEconomy.Progress(defs.First(x=>x.QuestId==20010),quest)==5,"Daily completion counts 5 achievements without claims or spending");
            quest.MageSkills[0]=new MageSave{Enhance=50};QuestEconomy.After(quest);quest.MageSkills[0].Enhance=0;QuestEconomy.After(quest);Check(QuestEconomy.Progress(defs.First(x=>x.QuestId==40802),quest)==50,"Mage highest total survives reset");
            string account="acceptance-"+Guid.NewGuid().ToString("N");LocalProgression.OpenTestAccount(account);
            Check(LocalProgression.Execute("test-credit",s=>{LocalProgression.Credit(s,eCurrency.Gold,100);return true;},"test-credit-id"),"Atomic credit accepted");
            long revision=LocalProgression.State.Revision;
            Check(!LocalProgression.Execute("test-credit",s=>{LocalProgression.Credit(s,eCurrency.Gold,100);return true;},"test-credit-id") && LocalProgression.Balance(eCurrency.Gold)==100 && LocalProgression.State.Revision==revision,"Duplicate transaction is a no-op");
            Check(!LocalProgression.Execute("test-negative",s=>LocalProgression.Spend(s,eCurrency.Gold,-1)) && LocalProgression.Balance(eCurrency.Gold)==100,"Negative spend rejected without mutation");
            Check(!LocalProgression.Execute("test-insufficient",s=>LocalProgression.Spend(s,eCurrency.Gold,101)),"Insufficient balance never goes negative");
            Check(!LocalProgression.TestFailedWrite(s=>{LocalProgression.Credit(s,eCurrency.Gold,50);return true;}) && LocalProgression.Balance(eCurrency.Gold)==100 && LocalProgression.State.Revision==revision,"Disk failure cannot publish or debit draft");
            LocalProgression.OpenTestAccount(account);Check(LocalProgression.Balance(eCurrency.Gold)==100 && LocalProgression.State.Claims.Contains("test-credit-id"),"Snapshot survives reopening with receipt");
            Check(LocalProgression.Execute("test-firstclear",s=>{s.MainClears.Add(0x200010001);return true;}),"Quest eligible setup");
            Check(QuestEconomy.Claim(10001) && LocalProgression.Balance(eCurrency.AncientCoin)==50 && !QuestEconomy.Claim(10001),"Guide payout exactly once");
            Check(LocalProgression.Execute("test-reinc",s=>{s.CycleBossStage=3;s.CycleStartedUtc=LocalProgression.UtcNow-601;s.PendingReincarnation=true;Reincarnation.ReincarnationService.CommitAtBoundary(s,"test-battle");return true;}),"Reincarnation atomic commit");
            Check(LocalProgression.State.ReincarnationLevel==15 && LocalProgression.State.CycleBossStage==0 && LocalProgression.State.MainStage==0x200010001 && LocalProgression.State.MainClears.Contains(0x200010001),"Reincarnation keeps permanent first-clear ledger");
            var reset=new ProgressionState{RubyGoldLevel=3,RubyExpLevel=2,RubyGoldSpent=65,RubyExpSpent=42,PendingRubyReset=1,BestRubyGoldLevel=3};
            RubyProgression.CommitReset(reset);
            Check(reset.RubyGoldLevel==0 && reset.RubyExpLevel==2 && reset.Wallet[eCurrency.Ruby]==52 && reset.BestRubyGoldLevel==3,"Ruby reset refunds 80% of selected actual spend and preserves best");
            reset.PendingRubyReset=2;RubyProgression.CommitReset(reset);
            Check(reset.RubyExpLevel==2 && reset.Wallet[eCurrency.Ruby]==52,"Second ruby reset same KST day does not refund");
            Check(QuestEconomy.DynamicGold(new ProgressionState{OfflineStage=0x200010001,OfflineKpm=3.25m,RubyGoldLevel=1})==66.3m,"Dynamic quest gold retains fractional income");
            LocalProgression.Execute("qa-ruby-setup",s=>{s.MainClears.Add(0x200020005);s.AccountLevel=200;s.Experience=0;s.Wallet[eCurrency.Ruby]=100;return true;});
            Check(!RubyProgression.Enhance(true) && LocalProgression.Balance(eCurrency.Ruby)==100,"EXP ruby purchase blocked at account cap without charge");
            Check(RubyProgression.Enhance(false) && LocalProgression.Balance(eCurrency.Ruby)==80 && LocalProgression.State.RubyGoldSpent==20,"Gold ruby purchase records exact expenditure");
            var manager=EquipmentManager.Instance;
            if(manager!=null)
            {
                var catalog=manager.GetByRarity(eEquipmentRarity.Normal).Concat(manager.GetByRarity(eEquipmentRarity.Rare)).Concat(manager.GetByRarity(eEquipmentRarity.Epic)).ToArray();
                Check(catalog.Length==18 && catalog.All(item=>new[]{"Knight","Archer","Mage"}.All(job=>item.IsAllowedForJob(job)==item.IsAllowedForJob("Elite_"+job))),"All 18 weapons preserve class-family eligibility after promotion");
                var weapon=manager.GetByRarity(eEquipmentRarity.Normal).First();
                LocalProgression.Execute("qa-materials",s=>{
                    s.Equipment.Clear();s.PendingEquipment.Clear();
                    foreach(string id in new[]{"target","free1","locked","enhanced"})s.Equipment.Add(new EquipmentSave{Id=id,Code=weapon.itemCode,Locked=id=="locked",Level=id=="enhanced"?1:0});
                    return true;
                });
                manager.RestoreEquipment();var target=manager.Inventory.Items.First(x=>x.instanceId=="target");
                Check(!manager.TryEnhance(target) && LocalProgression.State.Equipment.Count==4,"Enhancement with no stones preserves all owned items");
                LocalProgression.Execute("qa-stones",s=>{s.Wallet[eCurrency.EquipmentStone]=2;return true;});
                long beforeGold=LocalProgression.Balance(eCurrency.Gold);
                Check(manager.TryEnhance(target) && LocalProgression.State.Equipment.Count==4 && target.enhancementLevel==1 && LocalProgression.Balance(eCurrency.EquipmentStone)==0 && LocalProgression.Balance(eCurrency.Gold)==beforeGold,"Equipment enhancement consumes two stones, preserves weapons and gold");
                manager.SetLocked(target,true);Check(!manager.Dismantle(target),"Locked equipment cannot be dismantled");
                manager.SetLocked(target,false);long beforeKnowledge=LocalProgression.Balance(eCurrency.ArcaneKnowledge);
                Check(manager.Dismantle(target) && LocalProgression.Balance(eCurrency.EquipmentStone)==2 && LocalProgression.Balance(eCurrency.ArcaneKnowledge)==beforeKnowledge,"Dismantle pays base stone plus floor 80 percent of exact enhancement spend");
            }
            var mage=KingdomIdle.MageTower.MageTowerManager.Instance;
            if(mage!=null)
            {
                LocalProgression.Execute("qa-mage",s=>{s.MageSkills[0]=new MageSave{Fragments=3};s.Wallet[eCurrency.ArcaneKnowledge]=100;return true;});
                mage.NotifyCommitted();
                Check(mage.Enhance(0) && LocalProgression.State.MageSkills[0].Spent==10 && LocalProgression.Balance(eCurrency.ArcaneKnowledge)==90,"Mage enhancement persists exact cost");
                Check(mage.Awaken(0) && LocalProgression.State.MageSkills[0].Fragments==2,"First mage awakening costs one matching fragment");
                Check(mage.ResetEnhance(0) && LocalProgression.Balance(eCurrency.ArcaneKnowledge)==98 && LocalProgression.State.MageSkills[0].Awaken==1,"Mage reset refunds eight of ten knowledge while preserving awakening");
                Check(!mage.ResetEnhance(0) && LocalProgression.Balance(eCurrency.ArcaneKnowledge)==98,"Repeated mage reset cannot refund twice");
            }
            var timer=System.Diagnostics.Stopwatch.StartNew();for(int i=0;i<40;i++)Check(LocalProgression.Execute("test-durable-throughput",s=>{LocalProgression.Credit(s,eCurrency.Gold,1);return true;}),"Durable write "+i);timer.Stop();
            return new Dictionary<string,object>{{"balanceVersion",BalanceMath.Version},{"checks",checks},{"passed",checks.Count},{"averageDurableWriteMs",timer.Elapsed.TotalMilliseconds/40},{"snapshotBytes",new FileInfo(LocalProgression.SnapshotPath).Length}};
        }
    }
}
#endif
